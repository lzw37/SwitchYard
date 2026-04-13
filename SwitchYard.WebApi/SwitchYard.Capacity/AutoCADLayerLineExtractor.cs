using System.Globalization;
using System.Text;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;

namespace SwitchYard.Capacity
{
    public class AutoCADLayerLineExtractor
    {
        public readonly record struct LineSegmentRecord(
        int LineId,
        double StartX,
        double EndX,
        double StartY,
        double EndY);

        public List<LineSegmentRecord> ExtractFile(CadDocument document, string layerName)
        {
            bool layerExists = document.Layers.Any(layer =>
                string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase));
            if (!layerExists)
            {
                PrintAvailableLayers(document);
                return null;
            }

            List<LineSegmentRecord> segments = new();
            foreach (Entity entity in document.Entities)
            {
                ExtractEntity(entity, layerName, inheritedLayer: null, segments);
            }

            return segments;
        }

        private string GetEffectiveLayer(Entity entity, string? inheritedLayer)
        {
            string currentLayer = entity.Layer?.Name ?? string.Empty;
            if (string.Equals(currentLayer, "0", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(inheritedLayer))
            {
                return inheritedLayer;
            }

            return currentLayer;
        }

        private void ExtractEntity(Entity entity, string targetLayerName, string? inheritedLayer, List<LineSegmentRecord> segments)
        {
            string effectiveLayer = GetEffectiveLayer(entity, inheritedLayer);

            if (entity is Insert insert)
            {
                foreach (Entity explodedEntity in insert.Explode())
                {
                    ExtractEntity(explodedEntity, targetLayerName, effectiveLayer, segments);
                }

                return;
            }

            if (!string.Equals(effectiveLayer, targetLayerName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            switch (entity)
            {
                case Line line:
                    AddSegment(segments, line.StartPoint, line.EndPoint);
                    break;

                case LwPolyline polyline:
                    AddLwPolylineSegments(segments, polyline);
                    break;

                case Polyline2D polyline:
                    AddPolylineSegments(segments, polyline.Vertices.Select(vertex => vertex.Location), polyline.IsClosed);
                    break;

                case Polyline3D polyline:
                    AddPolylineSegments(segments, polyline.Vertices.Select(vertex => vertex.Location), polyline.IsClosed);
                    break;

                case Arc arc:
                    arc.GetEndVertices(out XYZ arcStart, out XYZ arcEnd);
                    AddSegment(segments, arcStart, arcEnd);
                    break;

                case Ellipse ellipse when !ellipse.IsFullEllipse:
                    ellipse.GetEndVertices(out XYZ ellipseStart, out XYZ ellipseEnd);
                    AddSegment(segments, ellipseStart, ellipseEnd);
                    break;

                case Spline spline:
                    AddSplineAsSingleSegment(segments, spline);
                    break;
            }
        }

        private void AddLwPolylineSegments(List<LineSegmentRecord> segments, LwPolyline polyline)
        {
            List<XYZ> points = polyline.Vertices
                .Select(vertex => new XYZ(vertex.Location.X, vertex.Location.Y, polyline.Elevation))
                .ToList();
            AddPolylineSegments(segments, points, polyline.IsClosed);
        }

        private void AddPolylineSegments(List<LineSegmentRecord> segments, IEnumerable<XYZ> vertices, bool isClosed)
        {
            List<XYZ> points = vertices.ToList();
            if (points.Count < 2)
            {
                return;
            }

            int segmentCount = isClosed ? points.Count : points.Count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                AddSegment(segments, points[i], points[(i + 1) % points.Count]);
            }
        }

        private void AddSplineAsSingleSegment(List<LineSegmentRecord> segments, Spline spline)
        {
            if (spline.FitPoints.Count >= 2)
            {
                AddSegment(segments, spline.FitPoints.First(), spline.FitPoints.Last());
                return;
            }

            if (spline.ControlPoints.Count >= 2)
            {
                AddSegment(segments, spline.ControlPoints.First(), spline.ControlPoints.Last());
                return;
            }

            if (spline.TryPolygonalVertexes(2, out List<XYZ>? points) && points.Count >= 2)
            {
                AddSegment(segments, points.First(), points.Last());
            }
        }

        private void AddSegment(List<LineSegmentRecord> segments, XYZ start, XYZ end)
        {
            segments.Add(new LineSegmentRecord(segments.Count + 1, start.X, end.X, start.Y, end.Y));
        }

        private void PrintAvailableLayers(CadDocument document)
        {
            Console.Error.WriteLine("Available layers:");
            foreach (string name in document.Layers.Select(layer => layer.Name).Order(StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"  {name}");
            }
        }
    }
}
