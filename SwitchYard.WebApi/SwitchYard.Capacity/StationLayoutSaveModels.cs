using System.Text.Json.Serialization;

namespace SwitchYard.Capacity
{
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

        [JsonPropertyName("cellCount")]
        public int CellCount { get; set; }

        [JsonPropertyName("annotationCount")]
        public int AnnotationCount { get; set; }
    }
}
