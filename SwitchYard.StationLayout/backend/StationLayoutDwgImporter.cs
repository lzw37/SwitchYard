using System.Text.RegularExpressions;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;

namespace SwitchYard.StationLayout;

internal static partial class StationLayoutDwgImporter
{
    public const long DefaultMaximumFileSize = 20L * 1024 * 1024;

    public static StationLayoutDwgResult Import(
        Stream stream,
        string fileName,
        string? layerName,
        long maximumFileSize = DefaultMaximumFileSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new StationLayoutValidationException("The DWG stream is not readable.");
        }

        if (stream.CanSeek && stream.Length == 0)
        {
            throw new StationLayoutValidationException("Please upload a non-empty DWG file.");
        }

        if (stream.CanSeek && stream.Length > maximumFileSize)
        {
            throw new StationLayoutValidationException(
                $"File size exceeds the {maximumFileSize / (1024 * 1024)} MB limit.");
        }

        if (!string.Equals(Path.GetExtension(fileName), ".dwg", StringComparison.OrdinalIgnoreCase))
        {
            throw new StationLayoutValidationException("Only DWG files are supported.");
        }

        var normalizedLayer = string.IsNullOrWhiteSpace(layerName) ? "0" : layerName.Trim();
        if (!LayerNameRegex().IsMatch(normalizedLayer))
        {
            throw new StationLayoutValidationException("Invalid layer name.");
        }

        try
        {
            var document = DwgReader.Read(stream, new DwgReaderConfiguration
            {
                Failsafe = false
            }, notification: null);
            if (!document.Layers.Any(layer =>
                    string.Equals(layer.Name, normalizedLayer, StringComparison.OrdinalIgnoreCase)))
            {
                throw new StationLayoutValidationException(
                    "The specified layer was not found in the DWG file.");
            }

            var segments = new AutoCadLayerLineExtractor().Extract(document, normalizedLayer);
            if (segments.Count == 0)
            {
                throw new StationLayoutValidationException(
                    $"No line segments were extracted from layer '{normalizedLayer}'.");
            }

            return new StationLayoutDwgResult(
                "OK",
                segments.Count,
                BuildDocument(segments));
        }
        catch (StationLayoutException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StationLayoutValidationException("Failed to extract DWG file.", exception);
        }
    }

    private static StationLayoutDocument BuildDocument(IReadOnlyList<LineSegment> segments)
    {
        const double canvasWidth = 1920;
        const double canvasHeight = 1080;
        const double padding = 80;

        var minX = segments.Min(segment => Math.Min(segment.StartX, segment.EndX));
        var maxX = segments.Max(segment => Math.Max(segment.StartX, segment.EndX));
        var minY = segments.Min(segment => Math.Min(segment.StartY, segment.EndY));
        var maxY = segments.Max(segment => Math.Max(segment.StartY, segment.EndY));
        var sourceWidth = Math.Max(maxX - minX, 1);
        var sourceHeight = Math.Max(maxY - minY, 1);
        var scale = Math.Min(
            (canvasWidth - padding * 2) / sourceWidth,
            (canvasHeight - padding * 2) / sourceHeight);

        double MapX(double value) => Math.Round(padding + (value - minX) * scale, 3);
        double MapY(double value) => Math.Round(padding + (maxY - value) * scale, 3);

        return new StationLayoutDocument
        {
            Metadata = new StationLayoutMetadata
            {
                LatestElementID = segments.Count,
                GridSettings = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    showGrid = true,
                    spacing = 20,
                    originX = 0,
                    originY = 0
                })
            },
            Tracks = segments.Select((segment, index) => new StationLayoutTrack
            {
                ID = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                X1 = MapX(segment.StartX),
                X2 = MapX(segment.EndX),
                Y1 = MapY(segment.StartY),
                Y2 = MapY(segment.EndY),
                FromNodeID = string.Empty,
                ToNodeID = string.Empty
            }).ToList()
        };
    }

    [GeneratedRegex("^[^\\x00-\\x1F<>:\"/\\\\|?*]{1,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex LayerNameRegex();

    private readonly record struct LineSegment(
        double StartX,
        double EndX,
        double StartY,
        double EndY);

    private sealed class AutoCadLayerLineExtractor
    {
        public List<LineSegment> Extract(CadDocument document, string layerName)
        {
            var segments = new List<LineSegment>();
            foreach (var entity in document.Entities)
            {
                ExtractEntity(entity, layerName, inheritedLayer: null, segments);
            }

            return segments;
        }

        private static string EffectiveLayer(Entity entity, string? inheritedLayer)
        {
            var currentLayer = entity.Layer?.Name ?? string.Empty;
            return string.Equals(currentLayer, "0", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(inheritedLayer)
                ? inheritedLayer
                : currentLayer;
        }

        private static void ExtractEntity(
            Entity entity,
            string targetLayer,
            string? inheritedLayer,
            List<LineSegment> segments)
        {
            var effectiveLayer = EffectiveLayer(entity, inheritedLayer);
            if (entity is Insert insert)
            {
                foreach (var exploded in insert.Explode())
                {
                    ExtractEntity(exploded, targetLayer, effectiveLayer, segments);
                }

                return;
            }

            if (!string.Equals(effectiveLayer, targetLayer, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            switch (entity)
            {
                case Line line:
                    Add(segments, line.StartPoint, line.EndPoint);
                    break;
                case LwPolyline polyline:
                    AddPolyline(
                        segments,
                        polyline.Vertices.Select(vertex =>
                            new XYZ(vertex.Location.X, vertex.Location.Y, polyline.Elevation)),
                        polyline.IsClosed);
                    break;
                case Polyline2D polyline:
                    AddPolyline(segments, polyline.Vertices.Select(vertex => vertex.Location), polyline.IsClosed);
                    break;
                case Polyline3D polyline:
                    AddPolyline(segments, polyline.Vertices.Select(vertex => vertex.Location), polyline.IsClosed);
                    break;
                case Arc arc:
                    arc.GetEndVertices(out var arcStart, out var arcEnd);
                    Add(segments, arcStart, arcEnd);
                    break;
                case Ellipse ellipse when !ellipse.IsFullEllipse:
                    ellipse.GetEndVertices(out var ellipseStart, out var ellipseEnd);
                    Add(segments, ellipseStart, ellipseEnd);
                    break;
                case Spline spline:
                    AddSpline(segments, spline);
                    break;
            }
        }

        private static void AddPolyline(
            List<LineSegment> segments,
            IEnumerable<XYZ> vertices,
            bool isClosed)
        {
            var points = vertices.ToList();
            if (points.Count < 2)
            {
                return;
            }

            var count = isClosed ? points.Count : points.Count - 1;
            for (var index = 0; index < count; index++)
            {
                Add(segments, points[index], points[(index + 1) % points.Count]);
            }
        }

        private static void AddSpline(List<LineSegment> segments, Spline spline)
        {
            if (spline.FitPoints.Count >= 2)
            {
                Add(segments, spline.FitPoints.First(), spline.FitPoints.Last());
                return;
            }

            if (spline.ControlPoints.Count >= 2)
            {
                Add(segments, spline.ControlPoints.First(), spline.ControlPoints.Last());
                return;
            }

            if (spline.TryPolygonalVertexes(2, out var points) && points.Count >= 2)
            {
                Add(segments, points.First(), points.Last());
            }
        }

        private static void Add(List<LineSegment> segments, XYZ start, XYZ end)
        {
            segments.Add(new LineSegment(start.X, end.X, start.Y, end.Y));
        }
    }
}
