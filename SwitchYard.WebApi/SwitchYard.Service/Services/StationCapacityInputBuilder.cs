using System.Globalization;
using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

public sealed class StationCapacityInputBuilder
{
    public StationCapacitySolveInput Build(StationCapacityInputRequest request)
    {
        var instanceId = request.InstanceId.Trim();
        var stationSchemeId = request.StationSchemeId.Trim();
        var operationPlanId = request.OperationPlanId.Trim();
        var dbConnector = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);

        var routeRows = dbConnector.Query<StationRouteRow>(
            @"SELECT InstanceID, StationSchemeID, ID, `Type` AS `Type`, Description,
                     NodeList, LinkList, SwitchList, CellList, InterruptCellList, SignalList,
                     AllowanceTags, ForbiddenTags, StartNodeID, EndNodeID
              FROM stationroute
              WHERE InstanceID = @instanceId AND StationSchemeID = @stationSchemeId
              ORDER BY ID",
            new { instanceId, stationSchemeId }) ?? new List<StationRouteRow>();

        var occupationRows = dbConnector.Query<StationRouteTimeRow>(
            @"SELECT InstanceID, StationSchemeID, RouteID, TrainTypeID, CellID,
                     StartOccupationShift, EndOccupationShift
              FROM stationroutetime
              WHERE InstanceID = @instanceId AND StationSchemeID = @stationSchemeId
              ORDER BY RouteID, TrainTypeID, CellID",
            new { instanceId, stationSchemeId }) ?? new List<StationRouteTimeRow>();

        var trainRows = dbConnector.Query<TrainRow>(
            @"SELECT InstanceID, StationSchemeID, OperationPlanID, ID, TrainTemplateID,
                     TrainNumber, Name, TrainType, IsFixedOperation
              FROM train
              WHERE InstanceID = @instanceId
                AND StationSchemeID = @stationSchemeId
                AND OperationPlanID = @operationPlanId
              ORDER BY TrainNumber, ID",
            new { instanceId, stationSchemeId, operationPlanId }) ?? new List<TrainRow>();

        var movementRows = dbConnector.Query<MovementRow>(
            @"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainID, TrainTemplateID,
                     MovementID, Name, RouteIDList, MinDuration, EarliestStartTime,
                     LatestEndTime, `Route` AS `Route`, Tag, SortOrder, CellOccupationOverridesJson, CellOccupationsJson
              FROM movement
              WHERE InstanceID = @instanceId
                AND StationSchemeID = @stationSchemeId
                AND OperationPlanID = @operationPlanId
              ORDER BY TrainID, SortOrder, MovementID",
            new { instanceId, stationSchemeId, operationPlanId }) ?? new List<MovementRow>();
        MovementCellOccupationStore.Materialize(dbConnector, instanceId, stationSchemeId, operationPlanId, movementRows);

        var movementsByTrain = movementRows
            .Where(movement => !string.IsNullOrWhiteSpace(movement.TrainID))
            .GroupBy(movement => movement.TrainID!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var input = new StationCapacitySolveInput
        {
            InstanceId = instanceId,
            StationSchemeId = stationSchemeId,
            OperationPlanId = operationPlanId,
            Routes = routeRows
                .Where(route => !string.IsNullOrWhiteSpace(route.ID))
                .Select(route => new StationCapacityRouteInput
                {
                    Id = route.ID!.Trim(),
                    CellIds = ParseList(route.CellList),
                    Tags = ParseList(route.AllowanceTags)
                })
                .Where(route => route.CellIds.Count > 0)
                .ToList(),
            RouteOccupations = occupationRows
                .Where(row => !string.IsNullOrWhiteSpace(row.RouteID) && !string.IsNullOrWhiteSpace(row.CellID))
                .Select(row => new StationCapacityRouteOccupationInput
                {
                    RouteId = row.RouteID!.Trim(),
                    TrainTypeId = row.TrainTypeID?.Trim() ?? string.Empty,
                    CellId = row.CellID!.Trim(),
                    StartOccupationShiftSeconds = row.StartOccupationShift ?? 0,
                    EndOccupationShiftSeconds = row.EndOccupationShift ?? 0
                })
                .ToList()
        };

        foreach (var trainRow in trainRows.Where(train => !string.IsNullOrWhiteSpace(train.ID)))
        {
            var trainId = trainRow.ID!.Trim();
            movementsByTrain.TryGetValue(trainId, out var trainMovements);
            var train = new StationCapacityTrainInput
            {
                Id = trainId,
                TrainType = trainRow.TrainType?.Trim() ?? string.Empty
            };

            var orderedMovements = (trainMovements ?? new List<MovementRow>())
                .OrderBy(movement => movement.SortOrder ?? int.MaxValue)
                .ThenBy(movement => movement.MovementID, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var index = 0; index < orderedMovements.Count; index++)
            {
                var movement = orderedMovements[index];
                var candidateRoutes = ParseList(movement.RouteIDList);
                if (candidateRoutes.Count == 0 && !string.IsNullOrWhiteSpace(movement.Route))
                {
                    candidateRoutes = ParseList(movement.Route);
                }

                var originalStart = ParseTimeSeconds(movement.EarliestStartTime);
                var originalEnd = ParseTimeSeconds(movement.LatestEndTime);
                var minDuration = Math.Max(0, movement.MinDuration ?? 0);
                if (originalEnd < originalStart + minDuration)
                {
                    originalEnd = Math.Min(86_400, originalStart + minDuration);
                }

                train.Movements.Add(new StationCapacityMovementInput
                {
                    Id = movement.MovementID?.Trim() ?? $"movement-{index + 1}",
                    Name = movement.Name?.Trim() ?? string.Empty,
                    Sequence = movement.SortOrder ?? index,
                    CandidateRouteIds = candidateRoutes,
                    RequiredRouteTags = ParseList(movement.Tag),
                    CellOccupationOverrides = (movement.CellOccupations ?? new()).Select(cell => new StationCapacityRouteOccupationInput
                        {
                            RouteId = cell.RouteID, CellId = cell.CellID,
                            StartOccupationShiftSeconds = cell.StartSeconds - MovementCellOccupationStore.ParseSeconds(movement.EarliestStartTime),
                            EndOccupationShiftSeconds = cell.EndSeconds - MovementCellOccupationStore.ParseSeconds(movement.LatestEndTime)
                        }).Concat(MovementCellOccupationOverrides.Read(movement.CellOccupationOverridesJson)
                        .Where(pair => !(movement.CellOccupations ?? new()).Any(cell => cell.RouteID == pair.Value.RouteID && cell.CellID == pair.Key))
                        .Select(pair => new StationCapacityRouteOccupationInput
                        {
                            RouteId = pair.Value.RouteID,
                            CellId = pair.Key,
                            StartOccupationShiftSeconds = pair.Value.StartOccupationShift,
                            EndOccupationShiftSeconds = pair.Value.EndOccupationShift
                        })).ToList(),
                    OriginalStartSeconds = originalStart,
                    OriginalEndSeconds = originalEnd,
                    MinDurationSeconds = minDuration,
                    MaxDurationSeconds = 86_400
                });
            }

            input.Trains.Add(train);
        }

        StationCapacityProcessInputBuilder.Apply(dbConnector, new ProcessScope
        {
            InstanceID = instanceId,
            StationSchemeID = stationSchemeId,
            OperationPlanID = operationPlanId
        }, input);
        if (input.RouteOccupations.Concat(input.Trains.SelectMany(train => train.Movements).SelectMany(movement => movement.CellOccupationOverrides))
            .Any(cell => cell.StartOccupationShiftSeconds != Math.Truncate(cell.StartOccupationShiftSeconds) ||
                cell.EndOccupationShiftSeconds != Math.Truncate(cell.EndOccupationShiftSeconds)))
            input.MinimumModelVersion = "2.1.0";
        return input;
    }

    internal static List<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                return document.RootElement.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" :
                        item.ValueKind == JsonValueKind.Number ? item.GetRawText() : "")
                    .Select(item => item.Trim())
                    .Where(item => item.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (JsonException)
            {
                // Fall back to the legacy delimited representation.
            }
        }

        return trimmed
            .Split(new[] { ',', ';', '，', '；', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double ParseTimeSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var text = value.Trim();
        var dayOffset = 0;
        if (text.StartsWith("D+", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0][2..], out dayOffset) || dayOffset < 0)
                throw new InvalidOperationException($"无法解析作业时间：{value}");
            text = parts[1];
        }
        var clock = text.Split(':');
        if (clock.Length is 2 or 3 &&
            int.TryParse(clock[0], out var hours) && hours >= 0 &&
            int.TryParse(clock[1], out var minutes) && minutes is >= 0 and < 60 &&
            (clock.Length == 2 || double.TryParse(clock[2], NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out _)))
        {
            var seconds = clock.Length == 3 ? double.Parse(clock[2], CultureInfo.InvariantCulture) : 0;
            if (seconds is >= 0 and < 60)
            {
                var total = dayOffset * 86_400d + hours * 3600d + minutes * 60d + seconds;
                if (total <= 604_800) return total;
            }
            throw new InvalidOperationException($"作业时间必须在七天内：{value}");
        }
        if (dayOffset > 0) throw new InvalidOperationException($"无法解析作业时间：{value}");
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time))
        {
            if (time.TotalSeconds is >= 0 and <= 604_800) return time.TotalSeconds;
            throw new InvalidOperationException($"作业时间必须在七天内：{value}");
        }

        if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            return (int)dateTime.TimeOfDay.TotalSeconds;
        }

        throw new InvalidOperationException($"无法解析作业时间：{value}");
    }
}
