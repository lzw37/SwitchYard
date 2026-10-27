using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    [HttpPost(Name = "CreateTrainWithMovements")]
    public IActionResult CreateTrainWithMovements([FromBody] CreateTrainWithMovementsRequest? request)
    {
        DBConnector? db = null;
        var inTransaction = false;
        try
        {
            if (request?.Movements is null || request.Movements.Count is < 1 or > 1000)
                return BadRequest("Between 1 and 1000 movements are required.");
            var normalizedTrain = NormalizeTrainRowRequest(request.Train, allowMissingID: false);
            if (normalizedTrain.ErrorResult is not null) return normalizedTrain.ErrorResult;
            var train = normalizedTrain.Train!;
            if (train.ID!.Length > 50) return BadRequest("Train ID must not exceed 50 characters.");
            var movements = new List<MovementRow>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in request.Movements)
            {
                if (source is null) return BadRequest("A movement is required.");
                // New occupations always come from the route timing catalog, never client data.
                source.CellOccupations = null;
                source.CellOccupationOverridesJson = null;
                var normalized = NormalizeMovementRowRequest(source, allowMissingMovementID: false);
                if (normalized.ErrorResult is not null) return normalized.ErrorResult;
                var movement = normalized.Movement!;
                if (movement.InstanceID != train.InstanceID || movement.StationSchemeID != train.StationSchemeID ||
                    movement.OperationPlanID != train.OperationPlanID || movement.TrainID != train.ID)
                    return BadRequest("All movements must belong to the new train and its plan.");
                if (movement.MovementID!.Length > 50 || !ids.Add(movement.MovementID))
                    return BadRequest("Movement IDs must be distinct and no longer than 50 characters.");
                if (string.IsNullOrWhiteSpace(movement.EarliestStartTime) || string.IsNullOrWhiteSpace(movement.LatestEndTime))
                    return BadRequest("Each movement requires valid start and end times.");
                _ = MovementCellOccupationStore.ParseSeconds(movement.EarliestStartTime);
                _ = MovementCellOccupationStore.ParseSeconds(movement.LatestEndTime);
                movement.SortOrder = movements.Count;
                movements.Add(movement);
            }

            db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, train.InstanceID!);
            if (auth is not null) return auth;

            var scope = new ProcessScope { InstanceID = train.InstanceID!, StationSchemeID = train.StationSchemeID!, OperationPlanID = train.OperationPlanID! };
            var scopeError = AuthorizeProcessGenerationScope(db, scope);
            if (scopeError is not null) return scopeError;
            db.BeginTransaction();
            inTransaction = true;
            var lockSuffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
            const string planFilter = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID";
            const string stationFilter = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID";
            // Lock an existing parent row so simultaneous retries cannot both insert an absent train.
            if (!(db.Query<string>($"SELECT OperationPlanID FROM operationplan WHERE {planFilter}{lockSuffix}", scope)?.Any() ?? false))
                return Abort(NotFound("Operation plan not found."));
            var usedTrainIDs = (db.Query<string>($"SELECT ID FROM train WHERE {planFilter}", scope) ?? new())
                .Concat(db.Query<string>($"SELECT DISTINCT TrainID FROM movement WHERE {planFilter}", scope) ?? new());
            if (CapacityDataLifecycle.TableExists(db, "trainprocesssnapshot"))
                usedTrainIDs = usedTrainIDs.Concat(db.Query<string>($"SELECT TrainID FROM trainprocesssnapshot WHERE {planFilter}", scope) ?? new());
            if (usedTrainIDs.Contains(train.ID, StringComparer.OrdinalIgnoreCase))
                return Abort(Conflict("Train ID already exists in this plan. Refresh before retrying."));

            var nodes = (db.Query<string>($"SELECT ID FROM node WHERE {stationFilter}{lockSuffix}", scope) ?? new()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var routes = (db.Query<StationRouteRow>($"SELECT ID,StartNodeID,EndNodeID FROM stationroute WHERE {stationFilter}{lockSuffix}", scope) ?? new())
                .Where(route => !string.IsNullOrWhiteSpace(route.ID)).ToDictionary(route => route.ID!, StringComparer.OrdinalIgnoreCase);
            foreach (var movement in movements)
            {
                var routeID = MovementCellOccupationStore.ResolveRouteID(movement);
                if (!string.IsNullOrEmpty(routeID))
                {
                    if (!routes.TryGetValue(routeID, out var route)) return Abort(BadRequest($"Route not found in this station scheme: {routeID}"));
                    movement.Route = route.ID;
                    movement.RouteIDList = route.ID;
                    movement.StartNodeID = route.StartNodeID?.Trim() ?? "";
                    movement.EndNodeID = route.EndNodeID?.Trim() ?? "";
                }
                else { movement.Route = ""; movement.RouteIDList = ""; }
                if (string.IsNullOrEmpty(movement.StartNodeID) || string.IsNullOrEmpty(movement.EndNodeID) ||
                    !nodes.Contains(movement.StartNodeID) || !nodes.Contains(movement.EndNodeID))
                    return Abort(BadRequest($"Movement endpoints must exist in this station scheme: {movement.MovementID}"));
            }
            if (string.IsNullOrWhiteSpace(train.TrainNumber))
                train.TrainNumber = GenerateOperationTrainNumber(db, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!);
            if (InsertTrain(db, train) != 1) throw new InvalidOperationException("Train was not saved.");
            var occupations = new MovementCellOccupationStore(db, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!);
            foreach (var movement in movements)
                if (InsertMovement(db, movement, occupations) != 1) throw new InvalidOperationException("Movement was not saved.");
            CapacityDataLifecycle.InvalidateAnalysis(db, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!);
            db.Commit();
            inTransaction = false;
            return Ok(new CreateTrainWithMovementsResponse { Train = train, Movements = movements });

            IActionResult Abort(IActionResult error)
            {
                db.Rollback();
                inTransaction = false;
                return error;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            if (inTransaction) db?.Rollback();
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            if (inTransaction) db?.Rollback();
            _logger.LogError(ex, "Failed to create train with movements.");
            return StatusCode(500, "Failed to create train and movements. No rows were saved.");
        }
    }
}
