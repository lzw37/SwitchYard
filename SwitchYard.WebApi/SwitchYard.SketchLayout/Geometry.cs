namespace SwitchYard.SketchLayout;

public static class Geometry
{
    public static Point Port(ConnectionPort port, IReadOnlyDictionary<string, Facility> facilities)
    {
        var f = facilities[port.FacilityId];
        if (f.Kind == "circle") return f.Center;
        var a = f.Vertices[port.Index];
        var b = f.Vertices[(port.Index + 1) % f.Vertices.Count];
        return port.Kind == "vertex" ? a : new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }
    private sealed record FilletCorner(Point Vertex, Point Before, Point After, double Angle)
    {
        public double Trim { get; set; }
    }
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    public static List<Point> Flatten(LogisticsLink link, IReadOnlyDictionary<string, Facility> facilities, bool rounded = true)
    {
        const double epsilon = 1e-7, radiusDefault = 100, fitRatio = .98;
        var raw = new List<Point> { Port(link.From, facilities) };
        raw.AddRange(link.Anchors);
        raw.Add(Port(link.To, facilities));
        var points = raw.Where((p, i) => i == 0 || Distance(p, raw[i - 1]) > epsilon).ToList();
        if (!rounded || points.Count <= 2) return points;
        // Match StationLayout's tangent-circle construction and the canvas linkGeometry.ts.
        var corners = new FilletCorner?[points.Count];
        for (var i = 1; i < points.Count - 1; i++)
        {
            var vertex = points[i]; var a = points[i - 1]; var b = points[i + 1];
            var beforeLength = Distance(vertex, a); var afterLength = Distance(vertex, b);
            var before = new Point((a.X - vertex.X) / beforeLength, (a.Y - vertex.Y) / beforeLength);
            var after = new Point((b.X - vertex.X) / afterLength, (b.Y - vertex.Y) / afterLength);
            var angle = Math.Acos(Math.Clamp(before.X * after.X + before.Y * after.Y, -1, 1));
            if (angle < 1e-4 || Math.PI - angle < 1e-4) continue;
            corners[i] = new(vertex, before, after, angle)
            {
                Trim = Math.Min(radiusDefault / Math.Tan(angle / 2), Math.Min(beforeLength * fitRatio, afterLength * fitRatio))
            };
        }
        for (var i = 1; i < points.Count - 2; i++)
        {
            var a = corners[i]; var b = corners[i + 1];
            if (a == null || b == null) continue;
            var available = Distance(points[i], points[i + 1]) * fitRatio;
            if (a.Trim + b.Trim <= available) continue;
            var scale = available / (a.Trim + b.Trim);
            a.Trim *= scale; b.Trim *= scale;
        }
        var result = new List<Point> { points[0] };
        void Append(Point p) { if (Distance(result[^1], p) > epsilon) result.Add(p); }
        for (var i = 1; i < points.Count - 1; i++)
        {
            var c = corners[i];
            if (c == null) { Append(points[i]); continue; }
            var radius = c.Trim * Math.Tan(c.Angle / 2);
            var start = new Point(c.Vertex.X + c.Before.X * c.Trim, c.Vertex.Y + c.Before.Y * c.Trim);
            var end = new Point(c.Vertex.X + c.After.X * c.Trim, c.Vertex.Y + c.After.Y * c.Trim);
            var bx = c.Before.X + c.After.X; var by = c.Before.Y + c.After.Y;
            var bisectorLength = Math.Sqrt(bx * bx + by * by);
            var centerDistance = radius / Math.Sin(c.Angle / 2);
            var center = new Point(c.Vertex.X + bx / bisectorLength * centerDistance, c.Vertex.Y + by / bisectorLength * centerDistance);
            var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            var sweep = (Math.PI - c.Angle) * (c.Before.X * c.After.Y - c.Before.Y * c.After.X < 0 ? 1 : -1);
            Append(start);
            for (var j = 1; j <= 24; j++)
            {
                var angle = startAngle + sweep * j / 24;
                Append(j == 24 ? end : new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)));
            }
        }
        Append(points[^1]);
        return result;
    }
    private static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static bool On(Point a, Point b, Point p) => Math.Abs(Cross(a, b, p)) < 1e-7 && p.X >= Math.Min(a.X, b.X) - 1e-7 && p.X <= Math.Max(a.X, b.X) + 1e-7 && p.Y >= Math.Min(a.Y, b.Y) - 1e-7 && p.Y <= Math.Max(a.Y, b.Y) + 1e-7;
    public static bool Intersects(Point a, Point b, Point c, Point d)
    {
        var x = Cross(a, b, c); var y = Cross(a, b, d); var z = Cross(c, d, a); var w = Cross(c, d, b);
        return (x * y < 0 && z * w < 0) || On(a, b, c) || On(a, b, d) || On(c, d, a) || On(c, d, b);
    }
    public static bool SimplePolygon(List<Point> p)
    {
        var area = 0d;
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < 1e-6) return false;
            area += Cross(p[0], a, b);
            for (var j = i + 1; j < p.Count; j++)
                if (j != i + 1 && !(i == 0 && j == p.Count - 1) && Intersects(a, b, p[j], p[(j + 1) % p.Count])) return false;
        }
        return Math.Abs(area) > 1e-4;
    }
    public static bool PathsIntersect(List<Point> a, List<Point> b)
    {
        if (a.Max(p => p.X) < b.Min(p => p.X) || b.Max(p => p.X) < a.Min(p => p.X) || a.Max(p => p.Y) < b.Min(p => p.Y) || b.Max(p => p.Y) < a.Min(p => p.Y)) return false;
        for (var i = 1; i < a.Count; i++) for (var j = 1; j < b.Count; j++)
        {
            if (!Intersects(a[i - 1], a[i], b[j - 1], b[j])) continue;
            // A sole shared terminal belongs to the facility, not a crossing resource.
            var shared = new[] { a[0], a[^1] }.Where(p => p == b[0] || p == b[^1]).ToList();
            if (shared.Any(p => (p == a[i - 1] || p == a[i]) && (p == b[j - 1] || p == b[j]) &&
                !On(a[i - 1], a[i], b[j - 1] == p ? b[j] : b[j - 1]) && !On(b[j - 1], b[j], a[i - 1] == p ? a[i] : a[i - 1]))) continue;
            return true;
        }
        return false;
    }
}
