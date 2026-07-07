using System.Text.Json.Serialization;

namespace SwitchYard.Capacity
{
    public sealed class StationLayoutNodeSaveContext
    {
        public IntegerIdAllocator Allocator { get; set; } = new();

        public StationLayoutPersistenceTransform Transform { get; set; } = StationLayoutPersistenceTransform.Identity;

        public List<StationLayoutNodeSaveEntry> Nodes { get; } = new();

        public Dictionary<string, int> NodeIDBySourceID { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> NodeIDByPointKey { get; } = new(StringComparer.Ordinal);
    }

    public sealed class StationLayoutNodeSaveEntry
    {
        public string SourceID { get; set; } = string.Empty;

        public int ID { get; set; }

        public double DisplayX { get; set; }

        public double DisplayY { get; set; }

        public double DatabaseX { get; set; }

        public double DatabaseY { get; set; }
    }

    public sealed class StationLayoutLinkSaveContext
    {
        public List<StationLayoutLinkSaveEntry> Links { get; } = new();

        public Dictionary<string, int> LinkIDBySourceID { get; } = new(StringComparer.Ordinal);
    }

    public sealed class StationLayoutLinkSaveEntry
    {
        public string SourceID { get; set; } = string.Empty;

        public int ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ArrowDirection { get; set; }

        public string? ArrowType { get; set; }

        public int FromNodeID { get; set; }

        public int ToNodeID { get; set; }
    }

    public sealed class StationLayoutSwitchSaveResult
    {
        public int SwitchCount { get; set; }

        public int SwitchBranchVectorCount { get; set; }
    }

    public sealed class StationLayoutSaveResult
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("instanceID")]
        public string InstanceID { get; set; } = string.Empty;

        [JsonPropertyName("stationSchemeID")]
        public string StationSchemeID { get; set; } = string.Empty;

        [JsonPropertyName("nodeCount")]
        public int NodeCount { get; set; }

        [JsonPropertyName("linkCount")]
        public int LinkCount { get; set; }

        [JsonPropertyName("curveCount")]
        public int CurveCount { get; set; }

        [JsonPropertyName("signalCount")]
        public int SignalCount { get; set; }

        [JsonPropertyName("insulationJointCount")]
        public int InsulationJointCount { get; set; }

        [JsonPropertyName("bufferStopCount")]
        public int BufferStopCount { get; set; }

        [JsonPropertyName("platformCount")]
        public int PlatformCount { get; set; }

        [JsonPropertyName("switchCount")]
        public int SwitchCount { get; set; }

        [JsonPropertyName("switchBranchVectorCount")]
        public int SwitchBranchVectorCount { get; set; }

        [JsonPropertyName("annotationCount")]
        public int AnnotationCount { get; set; }
    }
}
