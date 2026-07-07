using System.Globalization;

namespace SwitchYard.Capacity
{
    public sealed class IntegerIdAllocator
    {
        private readonly HashSet<int> _usedIDs = new();
        private int _nextID;

        public int Allocate(string? preferredID)
        {
            if (int.TryParse(preferredID, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                parsed >= 0 &&
                _usedIDs.Add(parsed))
            {
                _nextID = Math.Max(_nextID, parsed + 1);
                return parsed;
            }

            while (_usedIDs.Contains(_nextID))
            {
                _nextID++;
            }

            var allocated = _nextID;
            _usedIDs.Add(allocated);
            _nextID++;
            return allocated;
        }
    }
}
