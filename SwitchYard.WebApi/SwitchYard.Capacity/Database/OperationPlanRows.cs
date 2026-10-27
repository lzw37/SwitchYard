namespace SwitchYard.Capacity
{
    public sealed class OperationPlanRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public int? SortOrder { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }
    }

    public sealed class TrainTemplateRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public int? Number { get; set; }

        public int? IsFixedOperation { get; set; }
    }

    public sealed class MovementTemplateRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? MovementID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? MinDuration { get; set; }

        public int? SortOrder { get; set; }
    }

    public sealed class TrainRow
    {
        public string? CalculationParameterSetID { get; set; }

        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? ID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? TrainNumber { get; set; }

        public string? Name { get; set; }

        public string? TrainType { get; set; }

        public int? IsFixedOperation { get; set; }
    }

    public sealed class MovementRow
    {
        public string? InstanceID { get; set; }

        public string? StationSchemeID { get; set; }

        public string? OperationPlanID { get; set; }

        public string? TrainID { get; set; }

        public string? TrainTemplateID { get; set; }

        public string? MovementID { get; set; }

        public string? Name { get; set; }

        public string? RouteIDList { get; set; }

        public int? MinDuration { get; set; }

        public string? EarliestStartTime { get; set; }

        public string? LatestEndTime { get; set; }

        public string? StartNodeID { get; set; }

        public string? EndNodeID { get; set; }

        public string? CellOccupationOverridesJson { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string? CellOccupationsJson { get; set; }

        public List<MovementCellOccupation>? CellOccupations { get; set; }

        public string? Route { get; set; }

        public string? Tag { get; set; }

        public int? SortOrder { get; set; }
    }
}
