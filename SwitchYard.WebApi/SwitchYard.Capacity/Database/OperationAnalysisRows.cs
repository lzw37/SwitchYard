namespace SwitchYard.Capacity
{
    public sealed class OperationBottleneckSummaryCategoryRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? CategoryID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationBottleneckSummaryCategoryRouteRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? CategoryID { get; set; }

        public string? RouteID { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationAnalysisMetaRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public int? TotalTimeSeconds { get; set; }

        public DateTime? UpdatedDate { get; set; }
    }

    public sealed class OperationAnalysisCellRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? CellID { get; set; }

        public string? CellName { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationOccupationTimeTableResultRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? RowKey { get; set; }

        public string? RowType { get; set; }

        public string? SequenceText { get; set; }

        public string? RouteID { get; set; }

        public string? RouteName { get; set; }

        public string? OperationCountText { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationOccupationTimeCellValueRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? RowKey { get; set; }

        public string? CellID { get; set; }

        public double? CellValue { get; set; }

        public double? InterruptCellValue { get; set; }
    }

    public sealed class OperationOccupationTimeSubTableSetting
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? SubTableID { get; set; }

        public string? SubTableName { get; set; }

        public List<string> CellIDs { get; set; } = new();

        public string? CellIDList { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationBottleneckAnalysisResultRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? RouteID { get; set; }

        public string? RouteName { get; set; }

        public int? OperationCount { get; set; }

        public string? BottleneckCellID { get; set; }

        public string? BottleneckCellName { get; set; }

        public double? BottleneckUtilization { get; set; }

        public double? ThroughputCapacity { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationThroughputSummaryResultRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? CategoryID { get; set; }

        public string? GroupKey { get; set; }

        public string? GroupText { get; set; }

        public int? RouteCount { get; set; }

        public int? OperationCount { get; set; }

        public double? CapacityTotal { get; set; }

        public double? CapacityAverage { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class OperationThroughputSummaryRouteRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? CategoryID { get; set; }

        public string? RouteID { get; set; }

        public int? SortOrder { get; set; }
    }
}
