using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    [HttpPost(Name = "RecalculateCellOccupations")]
    public IActionResult RecalculateCellOccupations([FromBody] ProcessScope? request)
    {
        DBConnector? db = null;
        var inTransaction = false;
        try
        {
            var normalized = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID,
                request?.OperationPlanID, requireOperationPlanID: true);
            if (normalized.ErrorResult is not null) return normalized.ErrorResult;
            var scope = new ProcessScope {
                InstanceID = normalized.InstanceID!, StationSchemeID = normalized.StationSchemeID!,
                OperationPlanID = normalized.OperationPlanID!
            };
            db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, scope.InstanceID);
            if (auth is not null) return auth;
            EnsureTrainOperationPlanSchema(db);
            db.BeginTransaction();
            inTransaction = true;
            const string filter = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID";
            var lockSuffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
            if ((db.Query<OperationPlanRow>($"SELECT OperationPlanID FROM operationplan WHERE {filter}{lockSuffix}", scope) ?? new()).Count == 0)
            {
                db.Rollback();
                inTransaction = false;
                return NotFound("Operation plan not found.");
            }
            // Use the same train-then-movement lock order as a train type edit. No client times are accepted.
            db.Query<TrainRow>($"SELECT * FROM train WHERE {filter} ORDER BY ID{lockSuffix}", scope);
            var movements = db.Query<MovementRow>($"SELECT * FROM movement WHERE {filter} ORDER BY TrainID,MovementID{lockSuffix}", scope) ?? new();
            var occupations = new MovementCellOccupationStore(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID);
            foreach (var movement in movements)
            {
                // Explicit recalculation resets both absolute edits and legacy per-movement offsets.
                movement.CellOccupationOverridesJson = null;
                movement.CellOccupations = MovementCellOccupations.Normalize(occupations.Generate(movement, useLegacyAbsoluteTimes: false))!;
                movement.CellOccupationsJson = MovementCellOccupations.Write(movement.CellOccupations);
                if (db.ExecuteNonQuery($@"UPDATE movement
                    SET CellOccupationsJson=@CellOccupationsJson, CellOccupationOverridesJson=NULL
                    WHERE {filter} AND TrainID=@TrainID AND MovementID=@MovementID", movement) != 1)
                    throw new InvalidOperationException("Recalculated cell occupation times were not saved.");
            }
            // Cached analysis must not bring back statistics based on the old occupation times.
            DeleteOperationAnalysisResult(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID);
            var result = LoadTrainOperationPlan(db, scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID);
            db.Commit();
            inTransaction = false;
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            if (inTransaction) db?.Rollback();
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            if (inTransaction) db?.Rollback();
            _logger.LogError(ex, "Failed to recalculate cell occupation times.");
            return StatusCode(500, "Failed to recalculate cell occupation times.");
        }
    }
}
