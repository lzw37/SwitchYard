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

    public sealed class StationRouteEndRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OriginalID { get; set; }

        public string? ID { get; set; }

        public string? BindingNodeID { get; set; }

        public string? Type { get; set; }

        public string? SegmentTag { get; set; }

        public string? SidingTag { get; set; }
    }

    public sealed class StationRouteRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OriginalID { get; set; }

        public string? ID { get; set; }

        public string? Type { get; set; }

        public string? Description { get; set; }

        public string? NodeList { get; set; }

        public string? LinkList { get; set; }

        public string? SwitchList { get; set; }

        public string? CellList { get; set; }

        public string? SignalList { get; set; }

        public string? AllowanceTags { get; set; }

        public string? ForbiddenTags { get; set; }

        public string? StartNodeID { get; set; }

        public string? EndNodeID { get; set; }
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

        public List<string> SwitchIds { get; set; } = new();

        public List<string> CellIds { get; set; } = new();

        public List<string> SignalIds { get; set; } = new();

        public List<StationNodeRow> Nodes { get; set; } = new();

        public List<StationLinkRow> Links { get; set; } = new();

        public List<StationSwitchRow> Switches { get; set; } = new();

        public List<StationCellRow> Cells { get; set; } = new();

        public List<StationSignalRow> Signals { get; set; } = new();
    }
}
