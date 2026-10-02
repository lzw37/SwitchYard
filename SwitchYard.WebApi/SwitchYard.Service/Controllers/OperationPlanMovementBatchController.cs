using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using System.Text.Json;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    // A single visible dwelling run can span arrival, dwelling and departure movements.
    // Only persistence identity, ownership and concurrency are checked here. Business legality is external.
    [HttpPut(Name = "EditMovements")]
    public IActionResult EditMovements([FromBody] MovementBatchEditRequest? request)
    {
        DBConnector? db = null;
        var inTransaction = false;
        try
        {
            if (request?.Items is null || request.Items.Count is < 1 or > 1000)
                return BadRequest("Between 1 and 1000 movement edits are required.");
            var items = new List<(MovementRow Original, MovementRow Updated)>();
            foreach (var item in request.Items)
            {
                var original = NormalizeMovementRowRequest(item?.Original, false, limitOccupationOffsets: false);
                var updated = NormalizeMovementRowRequest(item?.Updated, false, limitOccupationOffsets: false);
                if (original.ErrorResult is not null) return original.ErrorResult;
                if (updated.ErrorResult is not null) return updated.ErrorResult;
                var a = original.Movement!;
                var b = updated.Movement!;
                if (a.InstanceID != b.InstanceID || a.StationSchemeID != b.StationSchemeID ||
                    a.OperationPlanID != b.OperationPlanID || a.TrainID != b.TrainID || a.MovementID != b.MovementID)
                    return BadRequest("An edit cannot change movement identity.");
                items.Add((a, b));
            }
            var first = items[0].Updated;
            if (items.Any(item => item.Updated.InstanceID != first.InstanceID || item.Updated.StationSchemeID != first.StationSchemeID ||
                item.Updated.OperationPlanID != first.OperationPlanID || item.Updated.TrainID != first.TrainID) ||
                items.Select(item => item.Updated.MovementID).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
                return BadRequest("Edits must reference distinct movements of one train in one plan.");
            db = GetCapacityDbConnector();
            var auth = ValidateCapacityInstanceOwnershipOrFail(db, first.InstanceID!);
            if (auth is not null) return auth;
            EnsureTrainOperationPlanSchema(db);
            db.BeginTransaction();
            inTransaction = true;
            foreach (var item in items)
            {
                var lockSuffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
                var current = db.Query<MovementRow>($@"SELECT * FROM {QuoteIdentifier("movement")}
                    WHERE InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID AND OperationPlanID=@OperationPlanID
                      AND TrainID=@TrainID AND MovementID=@MovementID{lockSuffix}", item.Updated)?.SingleOrDefault();
                if (current is null) { db.Rollback(); inTransaction = false; return NotFound("Movement not found."); }
                var normalized = NormalizeMovementRowRequest(current, false, limitOccupationOffsets: false).Movement!;
                // The UI represents an absent override document as an empty string.
                normalized.CellOccupationOverridesJson ??= "{}";
                item.Original.CellOccupationOverridesJson ??= "{}";
                if (JsonSerializer.Serialize(normalized) != JsonSerializer.Serialize(item.Original))
                { db.Rollback(); inTransaction = false; return Conflict("The plan has changed. Refresh before editing."); }
                item.Updated.CellOccupationOverridesJson ??= current.CellOccupationOverridesJson;
            }
            foreach (var item in items)
                if (UpdateMovement(db, item.Updated) != 1) throw new InvalidOperationException("Movement edit was not saved.");
            db.Commit();
            inTransaction = false;
            return Ok(items.Select(item => item.Updated).ToList());
        }
        catch (Exception ex)
        {
            if (inTransaction) db?.Rollback();
            _logger.LogError(ex, "Failed to save movement edits.");
            return StatusCode(500, "Failed to save movement edits.");
        }
    }
}
