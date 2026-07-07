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
}
