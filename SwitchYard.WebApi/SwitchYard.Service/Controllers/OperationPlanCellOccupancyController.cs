using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

namespace SwitchYard.Service.Controllers;

public partial class OperationPlanController
{
    private static readonly SemaphoreSlim CellOccupancyImportGate = new(1, 1);
    private const int CellOccupancyMaxBytes = 5 * 1024 * 1024;
    private const string CellImportStationFilter = "InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID";
    private const string CellImportPlanFilter = CellImportStationFilter + " AND OperationPlanID = @OperationPlanID";

    [HttpPost(Name = "PreviewCellOccupancyImport")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<IActionResult> PreviewCellOccupancyImport([FromBody] CellOccupancyImportRequest? request) =>
        HandleCellOccupancyImport(request, commit: false);

    [HttpPost(Name = "ImportCellOccupancy")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<IActionResult> ImportCellOccupancy([FromBody] CellOccupancyImportRequest? request) =>
        HandleCellOccupancyImport(request, commit: true);

    private async Task<IActionResult> HandleCellOccupancyImport(CellOccupancyImportRequest? request, bool commit)
    {
        DBConnector? db = null;
        var gateHeld = false;
        try
        {
            if (request is null) return BadRequest(new { message = "请选择 CellOccupancy CSV 文件。" });
            request.InstanceID = request.InstanceID?.Trim() ?? "";
            request.StationSchemeID = request.StationSchemeID?.Trim() ?? "";
            request.OperationPlanID = request.OperationPlanID?.Trim() ?? "";
            request.PlanName = request.PlanName?.Trim() ?? "";
            request.CellMappings ??= new();
            var scope = new ProcessScope { InstanceID = request.InstanceID, StationSchemeID = request.StationSchemeID, OperationPlanID = request.OperationPlanID };
            if (new[] { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID }.Any(id => id.Length is 0 or > 50))
                return BadRequest(new { message = "请先选择实例、站场方案和作业计划。" });
            if (request.CreateNewPlan && request.PlanName.Length is 0 or > 100)
                return BadRequest(new { message = "新计划名称须为 1–100 个字符。" });
            if (string.IsNullOrWhiteSpace(request.CsvText) || Encoding.UTF8.GetByteCount(request.CsvText) > CellOccupancyMaxBytes)
                return BadRequest(new { message = "请选择非空 UTF-8 CSV 文件，大小不能超过 5 MB。" });
            if (request.CellMappings.Count > 5000 || request.CellMappings.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 100 || string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > 50))
                return BadRequest(new { message = "Cell 映射无效，请重新选择站场中的 Cell。" });
            if (commit && (request.PreviewToken?.Length != 64 || !request.PreviewToken.All(Uri.IsHexDigit)))
                return BadRequest(new { message = "请先完成导入预览，再确认导入。" });

            db = GetCapacityDbConnector();
            var permissionError = AuthorizeProcessGenerationScope(db, scope);
            if (permissionError is not null) return permissionError;
            if (commit)
            {
                await CellOccupancyImportGate.WaitAsync(HttpContext.RequestAborted);
                gateHeld = true;
                db.BeginTransaction();
                var lockClause = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
                // Serialize imports and station edits before reading the reviewed snapshot.
                db.Query<string>($"SELECT ID FROM stationscheme WHERE InstanceID = @InstanceID AND ID = @StationSchemeID{lockClause}", scope);
                permissionError = AuthorizeProcessGenerationScope(db, scope);
                if (permissionError is not null) return permissionError;
            }

            var catalog = LoadCellOccupancyCatalog(db, scope, commit);
            var plans = LoadCellImportRows<OperationPlanRow>(db, "operationplan", "OperationPlanID", scope, commit, false);
            var trains = LoadCellImportRows<TrainRow>(db, "train", "ID", scope, commit, true);
            var movements = LoadCellImportRows<MovementRow>(db, "movement", "TrainID, MovementID", scope, commit, true);
            var result = new CellOccupancyImportBuilder().Build(request, catalog);
            var preview = result.Preview;
            if (!request.CreateNewPlan)
            {
                var occupiedIDs = new HashSet<string>(trains.Select(row => row.ID ?? "").Concat(movements.Select(row => row.TrainID ?? "")), StringComparer.OrdinalIgnoreCase);
                var trainNumbers = new HashSet<string>(trains.Select(row => row.TrainNumber ?? ""), StringComparer.OrdinalIgnoreCase);
                var conflicts = result.Trains.Where(row => occupiedIDs.Contains(row.ID) || trainNumbers.Contains(row.TrainNumber)).Select(row => row.TrainNumber).ToArray();
                if (conflicts.Length > 0)
                    preview.Errors.Add($"当前计划已存在同编号列车或作业：{string.Join("、", conflicts.Take(12))}。请选择新建计划，或先处理重复记录。");
            }
            preview.Valid = preview.Errors.Count == 0 && preview.TrainCount > 0;
            preview.PreviewToken = ComputeCellImportToken(request, catalog, plans, trains, movements);
            if (!commit) return Ok(preview);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(request.PreviewToken!), Convert.FromHexString(preview.PreviewToken)))
                return Conflict(new { message = "文件、导入选项或站场/计划数据已变化，或本次预览已经导入。请重新预览后再导入。" });
            if (!preview.Valid) return BadRequest(new { message = "导入校验未通过，未保存任何数据。", errors = preview.Errors });

            var target = new ProcessScope { InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID };
            var planName = plans.Single(plan => plan.OperationPlanID == scope.OperationPlanID).Name ?? "";
            if (request.CreateNewPlan)
            {
                target.OperationPlanID = GenerateOperationPlanID(db, scope.InstanceID, scope.StationSchemeID);
                planName = request.PlanName;
                var now = DateTime.Now;
                RequireCellImportInsert(db.ExecuteNonQuery($@"INSERT INTO {QuoteIdentifier("operationplan")}
                    (InstanceID,StationSchemeID,OperationPlanID,Name,Description,SortOrder,CreatedDate,UpdatedDate)
                    VALUES (@InstanceID,@StationSchemeID,@OperationPlanID,@Name,@Description,@SortOrder,@CreatedDate,@UpdatedDate)", new {
                    target.InstanceID, target.StationSchemeID, target.OperationPlanID, Name = planName,
                    Description = $"CellOccupancy CSV 导入：{preview.TrainCount} 列车，{preview.MovementCount} 项作业，{preview.OccupancyCount} 条占用。",
                    SortOrder = plans.Select(plan => plan.SortOrder ?? 0).DefaultIfEmpty(0).Max() + 1,
                    CreatedDate = now, UpdatedDate = now
                }));
            }

            foreach (var route in result.NewRoutes) InsertCellImportRow(db, "stationroute", route, target, false);
            foreach (var time in result.NewRouteTimes) InsertCellImportRow(db, "stationroutetime", time, target, false);
            foreach (var train in result.Trains) InsertCellImportRow(db, "train", train, target, true);
            foreach (var movement in result.Movements) InsertCellImportRow(db, "movement", movement, target, true);
            // Read back the whole batch before commit: ignored/altered inserts must roll back.
            VerifyCellImportRows(result.NewRoutes, LoadCellImportRows<CellOccupancyRoute>(db, "stationroute", "ID", target, false, false), row => row.ID);
            VerifyCellImportRows(result.NewRouteTimes, LoadCellImportRows<CellOccupancyRouteTime>(db, "stationroutetime", "RouteID,TrainTypeID,CellID", target, false, false), row => $"{row.RouteID}\0{row.TrainTypeID}\0{row.CellID}");
            VerifyCellImportRows(result.Trains, LoadCellImportRows<CellOccupancyTrainDraft>(db, "train", "ID", target, false, true), row => row.ID);
            VerifyCellImportRows(result.Movements, LoadCellImportRows<CellOccupancyMovementDraft>(db, "movement", "TrainID,MovementID", target, false, true), row => $"{row.TrainID}\0{row.MovementID}");
            if (db.Query<long>($"SELECT COUNT(1) FROM operationplan WHERE {CellImportPlanFilter}", target)?.Single() != 1)
                throw new InvalidOperationException("The imported operation plan was not saved exactly once.");
            LoadMovements(db, target.InstanceID, target.StationSchemeID, target.OperationPlanID);
            db.Commit();
            return Ok(new {
                preview.Valid, preview.TrainCount, preview.MovementCount, preview.OccupancyCount,
                preview.NewRouteCount, preview.ReusedRouteCount, preview.Warnings, preview.Errors,
                preview.CellMatches, preview.AvailableCells, preview.RouteMatches, preview.PreviewToken,
                target.OperationPlanID, OperationPlanName = planName
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (OperationCanceledException) { return StatusCode(499); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} CellOccupancy import.", commit ? "save" : "preview");
            return StatusCode(500, new { message = commit ? "导入失败，本次新计划、进路和列车作业已全部回滚。" : "读取导入数据失败，请刷新站场方案后重试。" });
        }
        finally
        {
            try { db?.Rollback(); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to clean up the CellOccupancy import transaction."); }
            finally { if (gateHeld) CellOccupancyImportGate.Release(); }
        }
    }

    private static List<T> LoadCellImportRows<T>(DBConnector db, string table, string order, ProcessScope scope, bool locking, bool planScoped)
    {
        var suffix = locking && DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
        return db.Query<T>($"SELECT * FROM {QuoteIdentifier(table)} WHERE {(planScoped ? CellImportPlanFilter : CellImportStationFilter)} ORDER BY {order}{suffix}", scope) ?? new();
    }

    private static CellOccupancyImportCatalog LoadCellOccupancyCatalog(DBConnector db, ProcessScope scope, bool locking)
    {
        var suffix = locking && DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? " FOR UPDATE" : "";
        return new() {
            Cells = LoadCellImportRows<CellOccupancyCell>(db, "cell", "ID", scope, locking, false),
            Links = LoadCellImportRows<CellOccupancyLink>(db, "link", "ID", scope, locking, false),
            Nodes = LoadCellImportRows<CellOccupancyNode>(db, "node", "ID", scope, locking, false),
            Switches = LoadCellImportRows<CellOccupancyBoundItem>(db, "switch", "ID", scope, locking, false),
            Signals = LoadCellImportRows<CellOccupancyBoundItem>(db, "signal", "ID", scope, locking, false),
            RouteEnds = db.Query<CellOccupancyBoundItem>($"SELECT ID, ID AS Name, BindingNodeID FROM stationrouteend WHERE {CellImportStationFilter} ORDER BY ID{suffix}", scope) ?? new(),
            Routes = LoadCellImportRows<CellOccupancyRoute>(db, "stationroute", "ID", scope, locking, false),
            RouteTimes = LoadCellImportRows<CellOccupancyRouteTime>(db, "stationroutetime", "RouteID,TrainTypeID,CellID", scope, locking, false)
        };
    }

    private static string ComputeCellImportToken(CellOccupancyImportRequest request, CellOccupancyImportCatalog catalog,
        List<OperationPlanRow> plans, List<TrainRow> trains, List<MovementRow> movements)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(new {
            request.InstanceID, request.StationSchemeID, request.OperationPlanID, request.CsvText,
            request.CreateNewPlan, request.PlanName,
            CellMappings = request.CellMappings.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            catalog, plans, trains, movements
        });
        return Convert.ToHexString(SHA256.HashData(content));
    }

    private static void InsertCellImportRow<T>(DBConnector db, string table, T row, ProcessScope scope, bool planScoped)
    {
        var columns = typeof(T).GetProperties().Select(property => property.Name).ToList();
        var parameters = new DynamicParameters(row);
        columns.AddRange(new[] { "InstanceID", "StationSchemeID" });
        parameters.Add("InstanceID", scope.InstanceID);
        parameters.Add("StationSchemeID", scope.StationSchemeID);
        if (planScoped)
        {
            columns.AddRange(new[] { "OperationPlanID", "TrainTemplateID" });
            parameters.Add("OperationPlanID", scope.OperationPlanID);
            parameters.Add("TrainTemplateID", "");
        }
        RequireCellImportInsert(db.ExecuteNonQuery($"INSERT INTO {QuoteIdentifier(table)} ({string.Join(",", columns.Select(QuoteIdentifier))}) VALUES ({string.Join(",", columns.Select(column => "@" + column))})", parameters));
    }

    private static void RequireCellImportInsert(int count)
    {
        if (count != 1) throw new InvalidOperationException("Expected exactly one CellOccupancy row to be inserted.");
    }

    private static void VerifyCellImportRows<T>(List<T> expected, List<T> actual, Func<T, string> key)
    {
        var lookup = actual.ToLookup(key, StringComparer.OrdinalIgnoreCase);
        foreach (var row in expected)
        {
            var found = lookup[key(row)].ToArray();
            if (found.Length != 1 || JsonSerializer.Serialize(found[0]) != JsonSerializer.Serialize(row))
                throw new InvalidOperationException("CellOccupancy read-back does not match the reviewed import.");
        }
    }
}
