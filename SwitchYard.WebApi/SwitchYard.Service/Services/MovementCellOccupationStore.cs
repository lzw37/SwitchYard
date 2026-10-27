using System.Globalization;
using System.Text.RegularExpressions;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>The server is the only place that materializes route offsets into occupation times.</summary>
public sealed class MovementCellOccupationStore
{
    private readonly Dictionary<string, StationRouteRow> _routes;
    private readonly List<StationRouteTimeRow> _times;
    private readonly Dictionary<string, TrainRow> _trains;
    private readonly CalculationParameterSets _parameterSets;

    public MovementCellOccupationStore(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID,
        TrainRow? previewTrain = null, IReadOnlyList<MovementRow>? movements = null)
    {
        var scope = new ProcessScope { InstanceID = instanceID, StationSchemeID = stationSchemeID, OperationPlanID = operationPlanID };
        const string station = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID";
        var routeIDs = movements?.Select(ResolveRouteID).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var trainIDs = movements?.Select(row => row.TrainID).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var parameters = new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, routeIDs, trainIDs };
        _routes = (routeIDs is { Length: 0 } ? [] : db.Query<StationRouteRow>($"SELECT ID,Type,LinkList,CellList,InterruptCellList FROM stationroute WHERE {station}" +
            (routeIDs is null ? "" : " AND ID IN @routeIDs"), parameters) ?? [])
            .Where(row => !string.IsNullOrWhiteSpace(row.ID)).ToDictionary(row => row.ID!, StringComparer.OrdinalIgnoreCase);
        _times = routeIDs is { Length: 0 } ? [] : db.Query<StationRouteTimeRow>($"SELECT CalculationParameterSetID,RouteID,TrainTypeID,CellID,StartOccupationShift,EndOccupationShift FROM stationroutetime WHERE {station}" +
            (routeIDs is null ? "" : " AND RouteID IN @routeIDs"), parameters) ?? [];
        _parameterSets = new CalculationParameterSets(db, instanceID, stationSchemeID);
        _trains = (trainIDs is { Length: 0 } ? [] : db.Query<TrainRow>($"SELECT ID,TrainType,CalculationParameterSetID FROM train WHERE {station} AND OperationPlanID=@OperationPlanID" +
            (trainIDs is null ? "" : " AND ID IN @trainIDs"), parameters) ?? [])
            .Where(row => !string.IsNullOrWhiteSpace(row.ID)).ToDictionary(row => row.ID!, row => row, StringComparer.OrdinalIgnoreCase);
        if (previewTrain?.ID is not null) _trains[previewTrain.ID] = previewTrain;

    }

    public List<MovementCellOccupation> Generate(MovementRow movement, bool useLegacyAbsoluteTimes = true)
    {
        var routeID = ResolveRouteID(movement);
        _routes.TryGetValue(routeID, out var route);
        var cellIDs = StationCapacityInputBuilder.ParseList(route?.CellList);
        var interruptIDs = StationCapacityInputBuilder.ParseList(route?.InterruptCellList);
        var train = _trains.GetValueOrDefault(movement.TrainID ?? "");
        var type = train?.TrainType ?? "";
        var parameterSetID = _parameterSets.Resolve(train?.CalculationParameterSetID);
        var times = _times.Where(row => string.Equals(row.RouteID, routeID, StringComparison.OrdinalIgnoreCase)
            && (row.CalculationParameterSetID ?? CalculationParameterSets.InitialDefaultID) == parameterSetID).ToList();
        var overrides = MovementCellOccupationOverrides.Read(movement.CellOccupationOverridesJson);
        if (cellIDs.Count + interruptIDs.Count == 0)
            cellIDs = times.Select(row => row.CellID ?? "").Concat(overrides.Where(pair => pair.Value.RouteID == routeID).Select(pair => pair.Key))
                .Where(id => id.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var start = ParseSeconds(movement.EarliestStartTime);
        var end = ParseSeconds(movement.LatestEndTime);
        return cellIDs.Concat(interruptIDs).Distinct(StringComparer.OrdinalIgnoreCase).Select(cellID => {
            var timing = times.FirstOrDefault(row => string.Equals(row.CellID, cellID, StringComparison.OrdinalIgnoreCase) && row.TrainTypeID == type)
                ?? times.FirstOrDefault(row => string.Equals(row.CellID, cellID, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(row.TrainTypeID));
            var custom = overrides.GetValueOrDefault(cellID);
            if (custom?.RouteID != routeID) custom = null;
            return new MovementCellOccupation {
                CellID = cellID, RouteID = routeID,
                StartSeconds = (useLegacyAbsoluteTimes ? custom?.StationPlanArriveMinutes * 60 : null) ?? start + (custom?.StartOccupationShift ?? timing?.StartOccupationShift ?? 0),
                EndSeconds = (useLegacyAbsoluteTimes ? custom?.StationPlanDepartMinutes * 60 : null) ?? end + (custom?.EndOccupationShift ?? timing?.EndOccupationShift ?? 0),
                IsInterruptCell = interruptIDs.Contains(cellID, StringComparer.OrdinalIgnoreCase) && !cellIDs.Contains(cellID, StringComparer.OrdinalIgnoreCase),
                DisplayCellID = useLegacyAbsoluteTimes ? custom?.StationPlanCellID : null, IsEdited = useLegacyAbsoluteTimes && custom is not null
            };
        }).ToList();
    }

    public void Prepare(MovementRow movement, MovementRow? previous = null)
    {
        var old = previous?.CellOccupations ?? MovementCellOccupations.Read(previous?.CellOccupationsJson);
        var routeChanged = previous is not null && ResolveRouteID(previous) != ResolveRouteID(movement);
        var offsetsChanged = previous is not null && MovementCellOccupationOverrides.Normalize(previous.CellOccupationOverridesJson, false) !=
            MovementCellOccupationOverrides.Normalize(movement.CellOccupationOverridesJson, false);
        var basisChanged = previous is null || routeChanged || offsetsChanged ||
            previous.EarliestStartTime != movement.EarliestStartTime || previous.LatestEndTime != movement.LatestEndTime;
        var rows = !basisChanged && old is not null ? MovementCellOccupations.Normalize(old)! : Generate(movement);
        // Restore only Cell/route pairs valid for the new route.
        var generatedRoutesByCell = routeChanged
            ? rows.ToDictionary(row => row.CellID, row => row.RouteID, StringComparer.OrdinalIgnoreCase) : null;
        if (basisChanged && !routeChanged && !offsetsChanged && old is not null)
            foreach (var row in old.Where(row => row.IsEdited)) Replace(rows, row);
        // An unchanged array sent with a movement edit is not a request to undo regenerated defaults.
        var incoming = MovementCellOccupations.Normalize(movement.CellOccupations);
        if (incoming is not null && (old is null || MovementCellOccupations.Write(incoming) != MovementCellOccupations.Write(old)))
            foreach (var row in incoming) {
                if (routeChanged && (!generatedRoutesByCell!.TryGetValue(row.CellID, out var generatedRouteID) ||
                    !string.Equals(row.RouteID, generatedRouteID, StringComparison.OrdinalIgnoreCase))) continue;
                var oldRow = old?.FirstOrDefault(value => value.CellID == row.CellID);
                if (!basisChanged || oldRow is null || MovementCellOccupations.Write(new() { row }) != MovementCellOccupations.Write(new() { oldRow }))
                    Replace(rows, row);
            }
        movement.CellOccupations = rows;
        movement.CellOccupationsJson = MovementCellOccupations.Write(rows);
    }

    private static void Replace(List<MovementCellOccupation> rows, MovementCellOccupation row)
    {
        var index = rows.FindIndex(value => string.Equals(value.CellID, row.CellID, StringComparison.OrdinalIgnoreCase));
        if (index < 0) rows.Add(row); else rows[index] = row;
    }

    public static void RefreshTrain(DBConnector db, TrainRow train)
    {
        var catalog = new MovementCellOccupationStore(db, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!);
        var lockSuffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
        var movements = db.Query<MovementRow>(@"SELECT * FROM movement
            WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID AND TrainID=@ID" + lockSuffix, train) ?? new();
        foreach (var movement in movements) {
            var cells = catalog.Generate(movement);
            foreach (var cell in (MovementCellOccupations.Read(movement.CellOccupationsJson) ?? new()).Where(cell => cell.IsEdited))
                Replace(cells, cell);
            movement.CellOccupationsJson = MovementCellOccupations.Write(cells);
            if (db.ExecuteNonQuery(@"UPDATE movement SET CellOccupationsJson=@CellOccupationsJson
                WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID
                  AND TrainID=@TrainID AND MovementID=@MovementID", movement) != 1)
                throw new InvalidOperationException("Updated train occupation times were not saved.");
        }
    }

    public static void Materialize(DBConnector db, string instanceID, string stationSchemeID, string operationPlanID, List<MovementRow> movements)
    {
        MovementCellOccupationStore? catalog = null;
        foreach (var movement in movements)
        {
            movement.CellOccupations = MovementCellOccupations.Read(movement.CellOccupationsJson);
            if (movement.CellOccupations is not null) continue;
            // A read-only fallback supports externally imported legacy rows. Startup migration
            // persists historical values in batches; GET requests never write to the database.
            catalog ??= new(db, instanceID, stationSchemeID, operationPlanID, movements: movements);
            catalog.Prepare(movement);
        }
    }

    public static string ResolveRouteID(MovementRow movement) => !string.IsNullOrWhiteSpace(movement.Route)
        ? movement.Route.Trim() : StationCapacityInputBuilder.ParseList(movement.RouteIDList?.TrimStart().StartsWith('[') == true
            ? movement.RouteIDList : movement.RouteIDList?.Replace("->", ",")).FirstOrDefault() ?? "";

    public static double ParseSeconds(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var match = Regex.Match(text.Trim(), @"^(?:D(?<day>[+-]?\d+)\s+)?(?<hour>\d+):(?<minute>\d{1,2})(?::(?<second>\d+(?:\.\d+)?))?$", RegexOptions.IgnoreCase);
        if (!match.Success) throw new ArgumentException($"无法解析作业时间：{text}");
        double Number(string name) => match.Groups[name].Success ? double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : 0;
        var result = Number("day") * 86400 + Number("hour") * 3600 + Number("minute") * 60 + Number("second");
        if (!double.IsFinite(result) || Number("minute") >= 60 || Number("second") >= 60) throw new ArgumentException($"无法解析作业时间：{text}");
        return result;
    }

}
