namespace SwitchYard.Capacity
{
    public sealed class StationLayoutSaveRequest
    {
        public string Json { get; set; } = string.Empty;

        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }
    }

    public sealed class StationSchemeRequest
    {
        public string InstanceID { get; set; } = string.Empty;

        public string? Name { get; set; }
    }

    public sealed class StationSchemeUpdateRequest
    {
        public string InstanceID { get; set; } = string.Empty;

        public string OriginalID { get; set; } = string.Empty;

        public string? Name { get; set; }
    }

    public sealed class StationRouteSearchRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public int StartNodeId { get; set; }

        public int EndNodeId { get; set; }
    }

    public sealed class StationRouteSearchResponse
    {
        public string InstanceID { get; set; } = string.Empty;

        public string StationSchemeID { get; set; } = string.Empty;

        public int StartNodeId { get; set; }

        public int EndNodeId { get; set; }

        public List<StationRouteSearchResult> Routes { get; set; } = new();
    }

    public sealed class StationRouteSearchResult
    {
        public string Direction { get; set; } = string.Empty;

        public List<int> NodeIds { get; set; } = new();

        public List<int> LinkIds { get; set; } = new();

        public List<StationNodeRow> Nodes { get; set; } = new();

        public List<StationLinkRow> Links { get; set; } = new();
    }
}
