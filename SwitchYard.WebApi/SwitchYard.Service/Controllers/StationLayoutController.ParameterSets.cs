using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class StationLayoutController
{
    [HttpGet]
    public IActionResult GetCalculationParameterSets(string instanceID, string stationSchemeID)
    {
        var db = GetCapacityDbConnector();
        var error = ValidateParameterSetScope(db, instanceID, stationSchemeID);
        return error ?? Ok(new CalculationParameterSets(db, instanceID, stationSchemeID).Sets);
    }

    [HttpPost]
    public IActionResult SaveCalculationParameterSet([FromBody] CalculationParameterSetRequest request)
    {
        var db = GetCapacityDbConnector();
        var error = ValidateParameterSetScope(db, request.InstanceID, request.StationSchemeID);
        if (error is not null) return error;
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > 200) return BadRequest("计算参数集名称必须为 1–200 个字符。");
        db.BeginTransaction();
        try
        {
            LockParameterSetScheme(db, request.InstanceID, request.StationSchemeID);
            CalculationParameterSets.EnsureDefault(db, request.InstanceID, request.StationSchemeID);
            var catalog = new CalculationParameterSets(db, request.InstanceID, request.StationSchemeID);
            var creating = string.IsNullOrWhiteSpace(request.ID);
            var id = creating ? _snowflakeIdGenerator.NextIdString() : request.ID!.Trim();
            var existing = catalog.Sets.SingleOrDefault(set => set.ID == id);
            if (!creating && existing is null) { db.Rollback(); return NotFound("计算参数集不存在。"); }
            if (creating && !string.IsNullOrWhiteSpace(request.CopyFromID) && !catalog.Sets.Any(set => set.ID == request.CopyFromID))
            { db.Rollback(); return BadRequest("要复制的计算参数集不存在。"); }
            var row = new CalculationParameterSet { InstanceID = request.InstanceID, StationSchemeID = request.StationSchemeID,
                ID = id, Name = name, IsDefault = request.IsDefault || existing?.IsDefault == true };
            if (row.IsDefault) db.ExecuteNonQuery($"UPDATE calculationparameterset SET IsDefault=0 WHERE {CalculationParameterSets.Scope}", row);
            db.ExecuteNonQuery(creating
                ? "INSERT INTO calculationparameterset(InstanceID,StationSchemeID,ID,Name,IsDefault) VALUES(@InstanceID,@StationSchemeID,@ID,@Name,@IsDefault)"
                : $"UPDATE calculationparameterset SET Name=@Name,IsDefault=@IsDefault WHERE {CalculationParameterSets.Scope} AND ID=@ID", row);
            if (creating && !string.IsNullOrWhiteSpace(request.CopyFromID))
                db.ExecuteNonQuery($"INSERT INTO stationroutetime(InstanceID,StationSchemeID,CalculationParameterSetID,RouteID,TrainTypeID,CellID,StartOccupationShift,EndOccupationShift) SELECT InstanceID,StationSchemeID,@ID,RouteID,TrainTypeID,CellID,StartOccupationShift,EndOccupationShift FROM stationroutetime WHERE {CalculationParameterSets.Scope} AND COALESCE(CalculationParameterSetID,'default')=@CopyFromID",
                    new { row.InstanceID, row.StationSchemeID, row.ID, request.CopyFromID });
            db.Commit();
            return Ok(row);
        }
        catch (Exception ex) { db.Rollback(); _logger.LogError(ex, "Failed to save calculation parameter set."); return StatusCode(500, "保存计算参数集失败。"); }
    }

    [HttpDelete]
    public IActionResult DeleteCalculationParameterSet(string instanceID, string stationSchemeID, string id)
    {
        var db = GetCapacityDbConnector();
        var error = ValidateParameterSetScope(db, instanceID, stationSchemeID);
        if (error is not null) return error;
        db.BeginTransaction();
        try
        {
            LockParameterSetScheme(db, instanceID, stationSchemeID);
            var catalog = new CalculationParameterSets(db, instanceID, stationSchemeID);
            if (!catalog.Sets.Any(set => set.ID == id)) { db.Rollback(); return NotFound(); }
            if (catalog.DefaultID == id) { db.Rollback(); return BadRequest("请先将其他计算参数集设为默认，再删除此参数集。"); }
            var scope = new { InstanceID = instanceID, StationSchemeID = stationSchemeID, id };
            db.ExecuteNonQuery($"DELETE FROM stationroutetime WHERE {CalculationParameterSets.Scope} AND CalculationParameterSetID=@id", scope);
            db.ExecuteNonQuery($"DELETE FROM calculationparameterset WHERE {CalculationParameterSets.Scope} AND ID=@id", scope);
            db.Commit();
            return Ok();
        }
        catch (Exception ex) { db.Rollback(); _logger.LogError(ex, "Failed to delete calculation parameter set."); return StatusCode(500, "删除计算参数集失败。"); }
    }

    private static string RequireCalculationParameterSet(DBConnector db, string instanceID, string stationSchemeID, string? id)
    {
        var sets = new CalculationParameterSets(db, instanceID, stationSchemeID);
        if (!string.IsNullOrWhiteSpace(id) && !sets.Sets.Any(set => set.ID == id.Trim()))
            throw new ArgumentException("计算参数集不存在，请刷新后重新选择。");
        return sets.Resolve(id);
    }

    private IActionResult? ValidateParameterSetScope(DBConnector db, string instanceID, string stationSchemeID)
    {
        if (string.IsNullOrWhiteSpace(instanceID) || string.IsNullOrWhiteSpace(stationSchemeID)) return BadRequest("请选择车站方案。");
        return ValidateCapacityInstanceOwnershipOrFail(db, instanceID)
            ?? (StationSchemeIDExists(db, instanceID, stationSchemeID) ? null : NotFound("Station scheme not found."));
    }

    private static void LockParameterSetScheme(DBConnector db, string instanceID, string stationSchemeID)
    {
        var suffix = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
        if (!(db.Query<string>("SELECT ID FROM stationscheme WHERE InstanceID=@instanceID AND ID=@stationSchemeID" + suffix, new { instanceID, stationSchemeID })?.Any() ?? false))
            throw new InvalidOperationException("Station scheme not found.");
    }
}
