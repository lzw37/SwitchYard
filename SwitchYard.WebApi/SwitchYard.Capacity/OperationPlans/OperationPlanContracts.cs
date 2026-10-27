using System.Text.Json;

namespace SwitchYard.Capacity
{
    public sealed class OperationPlanRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OriginalOperationPlanID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationPlanCopyRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? SourceOperationPlanID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class TrainTemplateRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? OriginalTrainTemplateID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public int? Number { get; set; }

        public int? IsFixedOperation { get; set; }
    }

    public sealed class MovementTemplateRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? OriginalMovementID { get; set; }

        public string? MovementID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? MinDuration { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class MovementBatchEditItem
    {
        public MovementRow? Original { get; set; }
        public MovementRow? Updated { get; set; }
    }

    public sealed class CreateTrainWithMovementsRequest
    {
        public TrainRow? Train { get; set; }

        public List<MovementRow> Movements { get; set; } = new();
    }

    public sealed class CreateTrainWithMovementsResponse
    {
        public TrainRow Train { get; set; } = new();

        public List<MovementRow> Movements { get; set; } = new();
    }

    public sealed class MovementBatchEditRequest
    {
        public List<MovementBatchEditItem> Items { get; set; } = new();
    }

    public sealed class MovementOrderItem
    {
        public string? MovementID { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class MovementTemplateOrderRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? TrainTemplateID { get; set; }

        public List<MovementOrderItem> Items { get; set; } = new();
    }

    public sealed class MovementOrderRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? TrainID { get; set; }

        public List<MovementOrderItem> Items { get; set; } = new();
    }

    public sealed class GenerateTrainOperationPlanRequest
    {
        public Dictionary<string, string>? TemplateParameterSetIDs { get; set; }

        public string? CalculationParameterSetID { get; set; }

        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? StartTime { get; set; }

        public string? EndTime { get; set; }
    }

    public sealed class TrainOperationPlanResponse
    {
        public List<TrainRow> Trains { get; set; } = new();

        public List<MovementRow> Movements { get; set; } = new();

        public List<JsonElement> ProcessConstraints { get; set; } = new();

        public List<string> GeneratedTrainIDs { get; set; } = new();

        public List<string> Warnings { get; set; } = new();
    }

    public sealed class OperationBottleneckSummaryCategorySaveRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public List<OperationBottleneckSummaryCategoryRow> Categories { get; set; } = new();
    }

    public sealed class OperationAnalysisCellSnapshotRow
    {
        public string? ID { get; set; }

        public string? Name { get; set; }
    }

    public sealed class OperationOccupationTimeTableSnapshotRow
    {
        public string? RowType { get; set; }

        public string? Sequence { get; set; }

        public string? RouteID { get; set; }

        public string? RouteName { get; set; }

        public string? OperationCount { get; set; }

        public Dictionary<string, double> CellDurations { get; set; } = new();

        public Dictionary<string, double> InterruptCellDurations { get; set; } = new();
    }

    public sealed class OperationBottleneckAnalysisSnapshotRow
    {
        public string? RouteID { get; set; }

        public string? RouteName { get; set; }

        public int OperationCount { get; set; }

        public string? BottleneckCellID { get; set; }

        public string? BottleneckCellName { get; set; }

        public double? BottleneckUtilization { get; set; }

        public double? ThroughputCapacity { get; set; }
    }

    public sealed class OperationBottleneckSummarySnapshotRow
    {
        public string? CategoryID { get; set; }

        public string? GroupKey { get; set; }

        public string? GroupText { get; set; }

        public List<string> RouteIDs { get; set; } = new();

        public int RouteCount { get; set; }

        public int OperationCount { get; set; }

        public double? CapacityTotal { get; set; }

        public double? CapacityAverage { get; set; }
    }

    public class OperationAnalysisResultSaveRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public int? TotalTimeSeconds { get; set; }

        public List<OperationAnalysisCellSnapshotRow> Cells { get; set; } = new();

        public List<OperationOccupationTimeTableSnapshotRow> OccupationTimeTableRows { get; set; } = new();

        public List<OperationBottleneckAnalysisSnapshotRow> BottleneckAnalysisRows { get; set; } = new();

        public List<OperationBottleneckSummarySnapshotRow> ThroughputSummaryRows { get; set; } = new();
    }

    public sealed class OperationAnalysisResultResponse : OperationAnalysisResultSaveRequest
    {
        public DateTime? UpdatedDate { get; set; }
    }

    public sealed class OperationOccupationTimeSubTableSaveRequest
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public List<OperationOccupationTimeSubTableSetting> SubTables { get; set; } = new();
    }
}
