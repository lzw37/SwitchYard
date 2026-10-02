using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.Service;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

public static class MovementCellOccupationChecks
{
    public static async Task<int> Run(HttpClient client)
    {
        var checks = 0;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string instanceID = "gantt-single-owned", stationSchemeID = "main", operationPlanID = "plan";
        var scope = new { instanceID, stationSchemeID, operationPlanID };
        var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        db.ExecuteNonQuery("INSERT INTO capacityinstance (ID,Name,Owner,IsActive) VALUES (@instanceID,'Gantt test','owner',1)", scope);
        db.ExecuteNonQuery("INSERT INTO stationscheme (InstanceID,ID,Name) VALUES (@instanceID,@stationSchemeID,'Gantt test')", scope);
        await Api(HttpMethod.Post, "CreateOperationPlan", new { instanceID, stationSchemeID, operationPlanID, name = "Gantt test" });
        foreach (var id in new[] { "T1", "T2" })
            await Api(HttpMethod.Post, "CreateTrain", new { instanceID, stationSchemeID, operationPlanID, id, trainNumber = id, name = id });
        db.ExecuteNonQuery("INSERT INTO stationroute (InstanceID,StationSchemeID,ID,CellList) VALUES (@instanceID,@stationSchemeID,'r1','c1,c2')", scope);
        foreach (var cellID in new[] { "c1", "c2" })
            db.ExecuteNonQuery("INSERT INTO stationroutetime (InstanceID,StationSchemeID,RouteID,TrainTypeID,CellID,StartOccupationShift,EndOccupationShift) VALUES (@instanceID,@stationSchemeID,'r1','',@cellID,-30,45)", new { instanceID, stationSchemeID, cellID });
        var movement = new MovementRow { InstanceID = instanceID, StationSchemeID = stationSchemeID, OperationPlanID = operationPlanID, TrainID = "T1", MovementID = "M1", Route = "r1", RouteIDList = "r1", EarliestStartTime = "08:00", LatestEndTime = "08:10", MinDuration = 120, Name = "接车" };
        await Api(HttpMethod.Post, "CreateMovement", movement);
        var other = JsonSerializer.Deserialize<MovementRow>(JsonSerializer.Serialize(movement, json), json)!;
        other.TrainID = "T2";
        await Api(HttpMethod.Post, "CreateMovement", other);
        var routeTimesBefore = RouteTimes();
        movement.CellOccupationOverridesJson = "{\"c1\":{\"routeID\":\"r1\",\"startOccupationShift\":90,\"endOccupationShift\":165}}";
        await Api(HttpMethod.Put, "EditMovement", movement);
        var saved = await Load();
        var first = saved.Movements.Single(row => row.TrainID == "T1");
        Check(first.EarliestStartTime == "08:00" && first.LatestEndTime == "08:10", "whole movement times remain unchanged");
        var offsets = MovementCellOccupationOverrides.Read(first.CellOccupationOverridesJson);
        Check(offsets.Count == 1 && offsets["c1"].StartOccupationShift == 90 && offsets["c1"].EndOccupationShift == 165, "only the selected cell is persisted");
        Check(saved.Movements.Single(row => row.TrainID == "T2").CellOccupationOverridesJson is null, "other movement is untouched");
        Check(RouteTimes() == routeTimesBefore, "shared route timings are untouched");
        var input = new StationCapacityInputBuilder().Build(new StationCapacityInputRequest { InstanceId = instanceID, StationSchemeId = stationSchemeID, OperationPlanId = operationPlanID });
        var overrideInput = input.Trains.Single(train => train.Id == "T1").Movements.Single().CellOccupationOverrides.Single();
        Check(overrideInput.CellId == "c1" && overrideInput.StartOccupationShiftSeconds == 90, "solver input receives the individual cell offsets");
        Check(input.Trains.Single(train => train.Id == "T2").Movements.Single().CellOccupationOverrides.Count == 0, "solver preserves other movements");
        first.CellOccupationOverridesJson = null;
        first.Name = "旧客户端编辑";
        await Api(HttpMethod.Put, "EditMovement", first);
        Check(MovementCellOccupationOverrides.Read((await Load()).Movements.Single(row => row.TrainID == "T1").CellOccupationOverridesJson).ContainsKey("c1"), "older clients preserve overrides");
        foreach (var invalid in new[] { "{", "null", "[]", "{\"c1\":null}", "{\"c1\":{\"routeID\":\"r1\",\"startOccupationShift\":1.5}}" })
        {
            first.CellOccupationOverridesJson = invalid;
            await Api(HttpMethod.Put, "EditMovement", first, expected: HttpStatusCode.BadRequest);
        }
        first.CellOccupationOverridesJson = movement.CellOccupationOverridesJson;
        await Api(HttpMethod.Put, "EditMovement", first, user: "outsider", expected: HttpStatusCode.Unauthorized);
        await Api(HttpMethod.Post, "CopyOperationPlan", new { instanceID, stationSchemeID, sourceOperationPlanID = operationPlanID, operationPlanID = "copy", name = "copy" });
        var copied = JsonSerializer.Deserialize<TrainOperationPlanResponse>(await Api(HttpMethod.Get, $"GetTrainOperationPlan?instanceID={instanceID}&stationSchemeID={stationSchemeID}&operationPlanID=copy"), json)!;
        Check(MovementCellOccupationOverrides.Read(copied.Movements.Single(row => row.TrainID == "T1").CellOccupationOverridesJson).ContainsKey("c1"), "plan copies retain overrides");
        Check(MovementCellOccupationOverrides.Read((await Load()).Movements.Single(row => row.TrainID == "T1").CellOccupationOverridesJson)["c1"].StartOccupationShift == 90, "rejected edits preserve persisted offsets");
        db.ExecuteNonQuery("INSERT INTO stationroute (InstanceID,StationSchemeID,ID,CellList) VALUES (@instanceID,@stationSchemeID,'r2','c3,c2')", scope);
        var second = Clone((await Load()).Movements.Single(row => row.TrainID == "T1"));
        second.MovementID = "M2";
        second.CellOccupationOverridesJson = null;
        await Api(HttpMethod.Post, "CreateMovement", second);
        var originalRows = (await Load()).Movements.Where(row => row.TrainID == "T1").OrderBy(row => row.MovementID).ToList();
        var otherBefore = JsonSerializer.Serialize((await Load()).Movements.Single(row => row.TrainID == "T2"), json);
        var edits = originalRows.Select(row => new MovementBatchEditItem { Original = Clone(row), Updated = Clone(row) }).ToList();
        foreach (var item in edits) { item.Updated!.Route = "r2"; item.Updated.RouteIDList = "r2"; item.Updated.CellOccupationOverridesJson = "{\"c3\":{\"routeID\":\"r2\",\"startOccupationShift\":75,\"endOccupationShift\":165}}"; }
        var batch = new MovementBatchEditRequest { Items = edits };
        edits[1].Original!.CellOccupationOverridesJson = "";
        await Api(HttpMethod.Put, "EditMovements", batch, user: "outsider", expected: HttpStatusCode.Unauthorized);
        edits[1].Original!.MovementID = edits[1].Updated!.MovementID = "missing";
        await Api(HttpMethod.Put, "EditMovements", batch, expected: HttpStatusCode.NotFound);
        Check((await Load()).Movements.Where(row => row.TrainID == "T1").All(row => row.Route == "r1"), "invalid second edit prevents the first edit from being saved");
        edits[1].Original!.MovementID = edits[1].Updated!.MovementID = "M2";
        edits[1].Original!.Name = "stale version";
        await Api(HttpMethod.Put, "EditMovements", batch, expected: HttpStatusCode.Conflict);
        edits[1].Original!.Name = originalRows[1].Name;
        db.ExecuteNonQuery("CREATE TRIGGER meso_edit_failure BEFORE UPDATE ON movement WHEN NEW.InstanceID='gantt-single-owned' AND NEW.MovementID='M2' AND NEW.Name='fail' BEGIN SELECT RAISE(ABORT, 'simulated failure'); END");
        edits[1].Updated!.Name = "fail";
        await Api(HttpMethod.Put, "EditMovements", batch, expected: HttpStatusCode.InternalServerError);
        Check((await Load()).Movements.Where(row => row.TrainID == "T1").All(row => row.Route == "r1"), "second write failure rolls back the first write");
        edits[1].Updated!.Name = originalRows[1].Name;
        await Api(HttpMethod.Put, "EditMovements", batch);
        var afterBatch = await Load();
        Check(afterBatch.Movements.Where(row => row.TrainID == "T1").All(row => row.Route == "r2" && row.RouteIDList == "r2" && MovementCellOccupationOverrides.Read(row.CellOccupationOverridesJson)["c3"].StartOccupationShift == 75), "both routes and individual cell times persist together");
        Check(JsonSerializer.Serialize(afterBatch.Movements.Single(row => row.TrainID == "T2"), json) == otherBefore, "batch edit preserves other trains");
        Check(RouteTimes() == routeTimesBefore, "batch edit preserves shared route timings");
        await Api(HttpMethod.Put, "EditMovements", batch, expected: HttpStatusCode.Conflict);
        var mixed = new MovementBatchEditRequest { Items = afterBatch.Movements.Select(row => new MovementBatchEditItem { Original = Clone(row), Updated = Clone(row) }).ToList() };
        await Api(HttpMethod.Put, "EditMovements", mixed, expected: HttpStatusCode.BadRequest);
        var duplicate = new MovementBatchEditRequest { Items = new() { edits[0], edits[0] } };
        await Api(HttpMethod.Put, "EditMovements", duplicate, expected: HttpStatusCode.BadRequest);
        TrainProcessSnapshotStore.EnsureSchema(db);
        db.ExecuteNonQuery("INSERT INTO trainprocesssnapshot (InstanceID,StationSchemeID,OperationPlanID,TrainID,Document) VALUES (@instanceID,@stationSchemeID,@operationPlanID,'T1','{}')", scope);
        var bound = new MovementBatchEditRequest { Items = afterBatch.Movements.Where(row => row.TrainID == "T1").Select(row => new MovementBatchEditItem { Original = Clone(row), Updated = Clone(row) }).ToList() };
        var unrestricted = bound.Items![0].Updated!;
        unrestricted.Route = unrestricted.RouteIDList = "missing-route";
        unrestricted.EarliestStartTime = "D+9 08:00";
        unrestricted.LatestEndTime = "D-1 23:59:30";
        unrestricted.MinDuration = 36000;
        unrestricted.CellOccupationOverridesJson = JsonSerializer.Serialize(new Dictionary<string, MovementCellOccupationOverride>
        {
            ["c3"] = new() { RouteID = "missing-route", StartOccupationShift = -900000, EndOccupationShift = 900000,
                StationPlanCellID = "unmatched-cell", StationPlanArriveMinutes = 12000, StationPlanDepartMinutes = -0.5 }
        }, json);
        await Api(HttpMethod.Put, "EditMovements", bound);
        db.ExecuteNonQuery("DELETE FROM trainprocesssnapshot WHERE InstanceID=@instanceID AND StationSchemeID=@stationSchemeID AND OperationPlanID=@operationPlanID", scope);
        var uncheckedPlan = await Load();
        var uncheckedMovement = uncheckedPlan.Movements.Single(row => row.TrainID == "T1" && row.MovementID == unrestricted.MovementID);
        Check(uncheckedMovement.Route == "missing-route", "missing routes do not reject process-bound diagram edits");
        Check(uncheckedMovement.EarliestStartTime == "D+9 08:00" && uncheckedMovement.LatestEndTime == "D-1 23:59:30" && uncheckedMovement.MinDuration == 36000,
            "reversed times and minimum duration violations are persisted verbatim");
        var raw = MovementCellOccupationOverrides.Read(uncheckedMovement.CellOccupationOverridesJson)["c3"];
        Check(raw.StartOccupationShift == -900000 && raw.EndOccupationShift == 900000, "diagram edits have no occupation offset bounds");
        Check(raw.StationPlanCellID == "unmatched-cell" && raw.StationPlanArriveMinutes == 12000 && raw.StationPlanDepartMinutes == -0.5,
            "unmatched sections and reversed raw diagram times survive reload");
        Check(JsonSerializer.Serialize(uncheckedPlan.Movements.Single(row => row.TrainID == "T2"), json) == otherBefore && RouteTimes() == routeTimesBefore,
            "unrestricted edits preserve other trains and shared timings");
        var followup = new MovementBatchEditRequest { Items = new() { new() { Original = Clone(uncheckedMovement), Updated = Clone(uncheckedMovement) } } };
        followup.Items[0].Updated!.Name = "继续调整";
        await Api(HttpMethod.Put, "EditMovements", followup);
        Check((await Load()).Movements.Single(row => row.TrainID == "T1" && row.MovementID == unrestricted.MovementID).Name == "继续调整",
            "unrestricted saved values can be edited again");
        await Api(HttpMethod.Post, "CopyOperationPlan", new { instanceID, stationSchemeID, sourceOperationPlanID = operationPlanID, operationPlanID = "unchecked-copy", name = "unchecked copy" });
        var rawCopy = JsonSerializer.Deserialize<TrainOperationPlanResponse>(await Api(HttpMethod.Get, $"GetTrainOperationPlan?instanceID={instanceID}&stationSchemeID={stationSchemeID}&operationPlanID=unchecked-copy"), json)!;
        Check(MovementCellOccupationOverrides.Read(rawCopy.Movements.Single(row => row.TrainID == "T1" && row.MovementID == unrestricted.MovementID).CellOccupationOverridesJson)["c3"].StationPlanDepartMinutes == -0.5,
            "plan copies retain unrestricted diagram values");
        var redoRows = (await Load()).Movements.Where(row => row.TrainID == "T1").OrderBy(row => row.MovementID).ToList();
        var undo = new MovementBatchEditRequest { Items = redoRows.Select((row, index) => new MovementBatchEditItem { Original = Clone(row), Updated = Clone(originalRows[index]) }).ToList() };
        foreach (var item in undo.Items!) item.Updated!.CellOccupationOverridesJson ??= "";
        await Api(HttpMethod.Put, "EditMovements", undo);
        var undone = (await Load()).Movements.Where(row => row.TrainID == "T1").OrderBy(row => row.MovementID).ToList();
        Check(undone.All(row => row.Route == "r1" && row.EarliestStartTime == "08:00" && row.LatestEndTime == "08:10"), "undo restores the entire batch's original routes and times");
        Check(MovementCellOccupationOverrides.Read(undone[0].CellOccupationOverridesJson)["c1"].StartOccupationShift == 90 && MovementCellOccupationOverrides.Read(undone[1].CellOccupationOverridesJson).Count == 0,
            "undo restores old offsets and explicitly clears previously absent overrides");
        var redo = new MovementBatchEditRequest { Items = undone.Select((row, index) => new MovementBatchEditItem { Original = Clone(row), Updated = Clone(redoRows[index]) }).ToList() };
        await Api(HttpMethod.Put, "EditMovements", redo);
        var redone = (await Load()).Movements.Where(row => row.TrainID == "T1").OrderBy(row => row.MovementID).ToList();
        Check(JsonSerializer.Serialize(redone, json) == JsonSerializer.Serialize(redoRows, json), "redo restores the exact saved batch without recomputing route choices or time legality");
        Console.WriteLine($"Single-cell occupancy HTTP/SQLite checks passed ({checks} assertions).");
        return checks;

        async Task<TrainOperationPlanResponse> Load() => JsonSerializer.Deserialize<TrainOperationPlanResponse>(await Api(HttpMethod.Get, $"GetTrainOperationPlan?instanceID={instanceID}&stationSchemeID={stationSchemeID}&operationPlanID={operationPlanID}"), json)!;
        string RouteTimes() => JsonSerializer.Serialize(db.Query<StationRouteTimeRow>("SELECT * FROM stationroutetime WHERE InstanceID=@instanceID AND StationSchemeID=@stationSchemeID ORDER BY CellID", scope), json);
        MovementRow Clone(MovementRow row) => JsonSerializer.Deserialize<MovementRow>(JsonSerializer.Serialize(row, json), json)!;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException("Single-cell occupancy: " + message); }
        async Task<string> Api(HttpMethod method, string endpoint, object? payload = null, string? user = "owner", HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var request = new HttpRequestMessage(method, "../OperationPlan/" + endpoint);
            if (payload is not null) request.Content = JsonContent.Create(payload, options: json);
            if (user is not null) request.Headers.Add("X-Test-User", user);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == expected, $"expected {(int)expected}, received {(int)response.StatusCode}: {text}");
            return text;
        }
    }
}
