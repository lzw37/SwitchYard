using SwitchYard.Capacity;

namespace SwitchYard.Service.Services;

internal static class StationCapacityLayoutInputBuilder
{
    public static StationCapacityLayoutInput Build(
        DBConnector db, string instanceId, string stationSchemeId, CalculationParameterSets parameterSets)
    {
        var scope = new { instanceId, stationSchemeId };
        const string filter = "WHERE InstanceID = @instanceId AND StationSchemeID = @stationSchemeId";
        var occupationsBySet = (db.Query<StationRouteTimeRow>(
            $"SELECT CalculationParameterSetID, RouteID, TrainTypeID, CellID, StartOccupationShift, EndOccupationShift FROM stationroutetime {filter} ORDER BY CalculationParameterSetID, RouteID, TrainTypeID, CellID",
            scope) ?? new())
            .GroupBy(row => row.CalculationParameterSetID ?? CalculationParameterSets.InitialDefaultID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(row => new StationCapacityLayoutRouteOccupationInput
            {
                RouteId = row.RouteID,
                TrainTypeId = row.TrainTypeID,
                CellId = row.CellID,
                StartOccupationShift = row.StartOccupationShift,
                EndOccupationShift = row.EndOccupationShift
            }).ToList(), StringComparer.Ordinal);
        foreach (var set in parameterSets.Sets)
            occupationsBySet.TryAdd(set.ID, new());

        return new StationCapacityLayoutInput
        {
            StationSchemeId = stationSchemeId,
            Cells = (db.Query<StationCapacityLayoutCellRow>(
                $"SELECT ID, LinkIDList, Name, LayoutOrder FROM cell {filter} ORDER BY LayoutOrder, ID", scope) ?? new())
                .Select(row => new StationCapacityLayoutCellInput
                {
                    Id = row.Id,
                    LinkIdList = row.LinkIdList,
                    Name = row.Name,
                    LayoutOrder = row.LayoutOrder
                }).ToList(),
            Routes = (db.Query<StationCapacityLayoutRouteRow>(
                $"SELECT ID, `Type`, TrainTypes, Description, NodeList, LinkList, SwitchList, CellList, InterruptCellList, SignalList, AllowanceTags, ForbiddenTags, StartNodeID, EndNodeID FROM stationroute {filter} ORDER BY ID", scope) ?? new())
                .Select(row => new StationCapacityLayoutRouteInput
                {
                    Id = row.Id,
                    Type = row.Type,
                    TrainTypes = row.TrainTypes,
                    Description = row.Description,
                    NodeList = row.NodeList,
                    LinkList = row.LinkList,
                    SwitchList = row.SwitchList,
                    CellList = row.CellList,
                    InterruptCellList = row.InterruptCellList,
                    SignalList = row.SignalList,
                    AllowanceTags = row.AllowanceTags,
                    ForbiddenTags = row.ForbiddenTags,
                    StartNodeId = row.StartNodeId,
                    EndNodeId = row.EndNodeId
                }).ToList(),
            RouteOperationTimes = (db.Query<StationCapacityLayoutRouteOperationTimeRow>(
                $"SELECT CalculationParameterSetID, RouteID, TrainTypeID, OperationTimeS FROM stationrouteoperationtime {filter} ORDER BY CalculationParameterSetID, RouteID, TrainTypeID", scope) ?? new())
                .Select(row => new StationCapacityLayoutRouteOperationTimeInput
                {
                    CalculationParameterSetId = row.CalculationParameterSetId,
                    RouteId = row.RouteId,
                    TrainTypeId = row.TrainTypeId,
                    OperationTimeS = row.OperationTimeS
                }).ToList(),
            CalculationParameterSets = new StationCapacityLayoutParameterSetsInput
            {
                // Include the effective default for a new scheme with no persisted sets yet.
                Sets = parameterSets.Sets.Select(set => new StationCapacityLayoutParameterSetInput
                {
                    Id = set.ID,
                    Name = set.Name,
                    IsDefault = set.IsDefault
                }).ToList(),
                RouteOccupations = occupationsBySet
            },
            Links = (db.Query<StationCapacityLayoutLinkRow>(
                $"SELECT ID, Name, FromNodeID, ToNodeID, ArrowDirection, ArrowType, X1, Y1, X2, Y2, LayoutOrder FROM link {filter} ORDER BY LayoutOrder, ID", scope) ?? new())
                .Select(row => new StationCapacityLayoutLinkInput
                {
                    Id = row.Id,
                    Name = row.Name,
                    FromNodeId = row.FromNodeId,
                    ToNodeId = row.ToNodeId,
                    ArrowDirection = row.ArrowDirection,
                    ArrowType = row.ArrowType,
                    X1 = row.X1,
                    Y1 = row.Y1,
                    X2 = row.X2,
                    Y2 = row.Y2,
                    LayoutOrder = row.LayoutOrder
                }).ToList(),
            Nodes = (db.Query<StationCapacityLayoutNodeRow>(
                $"SELECT ID, X, ActualX, Y, LayoutOrder FROM node {filter} ORDER BY LayoutOrder, ID", scope) ?? new())
                .Select(row => new StationCapacityLayoutNodeInput
                {
                    Id = row.Id,
                    X = row.X,
                    ActualX = row.ActualX,
                    Y = row.Y,
                    LayoutOrder = row.LayoutOrder
                }).ToList()
        };
    }
}
