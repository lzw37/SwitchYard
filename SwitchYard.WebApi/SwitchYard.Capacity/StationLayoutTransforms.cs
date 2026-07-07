namespace SwitchYard.Capacity
{
    public sealed class StationLayoutCoordinateTransform
    {
        public static readonly StationLayoutCoordinateTransform Identity = new(0, 0, 1, 0, false);

        private readonly double _minX;
        private readonly double _minY;
        private readonly double _scale;
        private readonly double _padding;
        private readonly bool _applied;

        public StationLayoutCoordinateTransform(double minX, double minY, double scale, double padding, bool applied)
        {
            _minX = minX;
            _minY = minY;
            _scale = scale;
            _padding = padding;
            _applied = applied;
        }

        public (double x, double y) MapPoint(double x, double y)
        {
            return (
                Math.Round(_padding + (x - _minX) * _scale, 3),
                Math.Round(_padding + (y - _minY) * _scale, 3));
        }

        public double MapLength(double length)
        {
            return Math.Round(length * _scale, 3);
        }

        public object ToMetadata()
        {
            return new
            {
                applied = _applied,
                minX = _minX,
                minY = _minY,
                scale = _scale,
                padding = _padding
            };
        }
    }

    public sealed class StationLayoutPersistenceTransform
    {
        public static readonly StationLayoutPersistenceTransform Identity = new(0, 0, 1, 0, false);

        private readonly double _minX;
        private readonly double _minY;
        private readonly double _scale;
        private readonly double _padding;
        private readonly bool _applied;

        private StationLayoutPersistenceTransform(double minX, double minY, double scale, double padding, bool applied)
        {
            _minX = minX;
            _minY = minY;
            _scale = scale;
            _padding = padding;
            _applied = applied;
        }

        public static StationLayoutPersistenceTransform FromMetadata(StationLayoutJsonCoordinateTransform? metadata)
        {
            if (metadata == null ||
                !metadata.Applied ||
                !double.IsFinite(metadata.Scale) ||
                metadata.Scale <= 0)
            {
                return Identity;
            }

            return new StationLayoutPersistenceTransform(
                metadata.MinX,
                metadata.MinY,
                metadata.Scale,
                metadata.Padding,
                true);
        }

        public (double x, double y) UnmapPoint(double x, double y)
        {
            if (!_applied)
            {
                return (Math.Round(x, 3), Math.Round(y, 3));
            }

            return (
                Math.Round(((x - _padding) / _scale) + _minX, 6),
                Math.Round(((y - _padding) / _scale) + _minY, 6));
        }

        public double UnmapLength(double length)
        {
            if (!_applied)
            {
                return Math.Round(length, 3);
            }

            return Math.Round(length / _scale, 6);
        }
    }
}
