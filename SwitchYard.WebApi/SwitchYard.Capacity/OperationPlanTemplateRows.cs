namespace SwitchYard.Capacity
{
    public sealed class TrainTemplateRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public int? Number { get; set; }
    }

    public sealed class MovementTemplateRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? MovementID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? MinDuration { get; set; }
    }

    public sealed class TrainTemplateRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OriginalTrainTemplateID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public int? Number { get; set; }
    }

    public sealed class MovementTemplateRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? OriginalMovementID { get; set; }

        public string? MovementID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? MinDuration { get; set; }
    }

    public sealed class TrainRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? ID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? TrainNumber { get; set; }

        public string? Name { get; set; }

        public string? TrainType { get; set; }
    }

    public sealed class MovementRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? TrainID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? MovementID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? MinDuration { get; set; }

        public string? EarliestStartTime { get; set; }

        public string? LatestEndTime { get; set; }

        public string? Route { get; set; }

        public string? Tag { get; set; }
    }

    public sealed class GenerateTrainOperationPlanRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? StartTime { get; set; }

        public string? EndTime { get; set; }
    }

    public sealed class TrainOperationPlanResponse
    {
        public List<TrainRow> Trains { get; set; } = new();

        public List<MovementRow> Movements { get; set; } = new();
    }

    public sealed class OperationBottleneckSummaryCategoryRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? CategoryID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationBottleneckSummaryCategorySaveRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public List<OperationBottleneckSummaryCategoryRow> Categories { get; set; } = new();
    }
}
