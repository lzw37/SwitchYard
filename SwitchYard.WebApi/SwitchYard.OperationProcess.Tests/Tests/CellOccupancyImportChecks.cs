using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SwitchYard.Capacity;
using SwitchYard.Service;

/// <summary>Exercises CSV import through the real HTTP controllers using only the test host's temporary SQLite database.</summary>
public static class CellOccupancyImportChecks
{
    private const string Header = "TrainID,MovementID,MovementType,CellID,OccupancyStartTime,OccupancyEndTime";

    public static async Task<int> Run(HttpClient client)
    {
        const string owned = "cell-import-owned";
        const string foreign = "cell-import-foreign";
        const string scheme = "main";
        const string plan = "selected";
        const string fixtureFilter = "InstanceID IN (@owned,@foreign)";
        const string schemeFilter = "InstanceID=@owned AND StationSchemeID=@scheme";
        var checks = 0;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        var args = new { owned, foreign, scheme };
        Seed();
        var originalPlan = PlanState(plan);
        var originalTimes = RouteTimesState("arrival-existing");
        var initialState = State();
        var csv = Csv("T'01");
        var payload = Payload(csv);

        await Reject("PreviewCellOccupancyImport", payload, HttpStatusCode.Unauthorized, user: null);
        await Reject("PreviewCellOccupancyImport", payload, HttpStatusCode.Forbidden, user: "outsider");
        await Reject("PreviewCellOccupancyImport", Payload(csv, instance: foreign), HttpStatusCode.Forbidden);
        await Reject("PreviewCellOccupancyImport", Payload(csv, instance: "missing-import-instance"), HttpStatusCode.NotFound);
        await Reject("PreviewCellOccupancyImport", Payload(csv, station: "missing-scheme"), HttpStatusCode.NotFound);
        await Reject("PreviewCellOccupancyImport", Payload(csv, target: "missing-plan", createNew: false), HttpStatusCode.NotFound);
        await Reject("PreviewCellOccupancyImport", Payload(csv, instance: ""), HttpStatusCode.BadRequest);
        await Reject("ImportCellOccupancy", payload, HttpStatusCode.BadRequest);
        await Request("PreviewCellOccupancyImport", Payload(csv, instance: foreign), user: "admin");
        await Request("PreviewCellOccupancyImport", Payload(csv, instance: foreign), user: "multi-role-admin");

        foreach (var (invalid, label) in new[]
        {
            (Header.Replace("CellID", "OtherCell") + "\nT,0,Dw,b,08:00:00,08:01:00", "missing required header"),
            (Header + ",CellID\nT,0,Dw,b,08:00:00,08:01:00,b", "duplicate header"),
            (Header + "\nT,0,Dw,b,08:00:00", "missing record field"),
            (Header + "\nT,0,Teleport,b,08:00:00,08:01:00", "unknown movement type"),
            (Header + "\nT,0,Dw,b,08:60:00,09:01:00", "invalid minute"),
            (Header + "\nT,0,Dw,b,08:02:00,08:01:00", "end before start"),
            (Header + "\nT,0,Dw,b,08:00:00.25,08:01:00", "unsupported fractional seconds are not rounded"),
            (Header + "\nT,0,Dw,b,08:00:00,08:01:00\nT,0,Dw,b,08:00:00,08:01:00", "duplicate movement cell"),
            (Header + "\nT,0,Arr,a,08:00:00,08:01:00\nT,0,Dep,b,08:00:00,08:01:00", "conflicting types within a movement"),
            (Header + "\nT,0,Arr,a,08:00:00,08:01:00\nT,0,Arr,d,08:00:00,08:01:00", "disconnected Cell topology"),
            (Header + "\n\"T,0,Dw,b,08:00:00,08:01:00", "unterminated CSV quote")
        })
        {
            var result = await Preview(Payload(invalid));
            Check(!Bool(result, "valid") && result["errors"]!.AsArray().Count > 0, label + " fails with a useful validation error");
        }
        Check(State() == initialState, "all previews, including rejected malformed files, are read-only");

        var unknown = Payload(Header + "\nUnknown,0,Dw,external-track,08:00:00,08:01:00");
        var unresolved = await Preview(unknown);
        Check(!Bool(unresolved, "valid") && unresolved["cellMatches"]!.AsArray().Any(item => Bool(item!, "needsSelection")),
            "unknown Cell is exposed for an explicit mapping and cannot be silently skipped");
        unknown["previewToken"] = Text(unresolved, "previewToken");
        await Reject("ImportCellOccupancy", unknown, HttpStatusCode.BadRequest);
        unknown["cellMappings"] = new JsonObject { ["external-track"] = "b" };
        Check(Bool(await Preview(unknown), "valid"), "manual Cell mapping to the current station resolves an unknown name");
        unknown["cellMappings"] = new JsonObject { ["external-track"] = "foreign-only" };
        Check(!Bool(await Preview(unknown), "valid"), "manual mapping cannot bind a Cell from another station");

        db.ExecuteNonQuery($"UPDATE cell SET Name='1G' WHERE {schemeFilter} AND ID='d'", args);
        var ambiguous = Payload(csv);
        Check(!Bool(await Preview(ambiguous), "valid"), "ambiguous Cell names require selection instead of arbitrary first-match assignment");
        ambiguous["cellMappings"] = new JsonObject { ["1G"] = "b" };
        Check(Bool(await Preview(ambiguous), "valid"), "explicit mapping resolves an ambiguous Cell name");
        db.ExecuteNonQuery($"UPDATE cell SET Name='disconnected' WHERE {schemeFilter} AND ID='d'", args);

        var preview = await Preview(payload);
        Check(Bool(preview, "valid"), "BOM and quoted CSV fields parse successfully: " + preview.ToJsonString());
        Check(Int(preview, "trainCount") == 1 && Int(preview, "movementCount") == 3 && Int(preview, "occupancyCount") == 5,
            "preview counts trains, grouped movements, and every occupancy record separately");
        Check(Int(preview, "newRouteCount") == 2 && Int(preview, "reusedRouteCount") == 1,
            "matching Arrival route is reused and missing Dw/Departure routes are planned");
        Check(preview["cellMatches"]!.AsArray().Any(item => Text(item!, "sourceCell") == "A, \"入口\"" && Text(item!, "cellID") == "a"),
            "quoted commas and escaped quotes survive name-based Cell matching");
        Check(preview["availableCells"]!.AsArray().All(item => Text(item!, "id") != "foreign-only"), "mapping choices contain only Cells in the selected station scheme");
        var differentOffsets = await Preview(Payload(csv.Replace("08:00:01,08:00:55", "08:00:02,08:00:56")));
        Check(Bool(differentOffsets, "valid") && Int(differentOffsets, "newRouteCount") == 3 && Int(differentOffsets, "reusedRouteCount") == 0,
            "the same Cell path with different occupancy shifts requires a separate route rather than overwriting an existing profile");
        db.ExecuteNonQuery($"UPDATE stationroutetime SET TrainTypeID='' WHERE {schemeFilter} AND RouteID='arrival-existing' AND CellID='b'", args);
        var mixedProfiles = await Preview(payload);
        Check(Bool(mixedProfiles, "valid") && Int(mixedProfiles, "newRouteCount") == 3 && Int(mixedProfiles, "reusedRouteCount") == 0,
            "an incomplete train-type profile cannot borrow individual Cells from the default profile and appear reusable");
        db.ExecuteNonQuery($"UPDATE stationroutetime SET TrainTypeID='旅客列车' WHERE {schemeFilter} AND RouteID='arrival-existing' AND CellID='b'", args);
        Check(State() == initialState, "successful preview creates no plans, trains, routes, or route times");

        // The default omitted createNewPlan field must create a separate plan and retain the selected plan.
        payload.Remove("createNewPlan");
        preview = await Preview(payload);
        payload["previewToken"] = Text(preview, "previewToken");
        var imported = await Request("ImportCellOccupancy", payload);
        var importedPlan = Text(imported!, "operationPlanID");
        Check(!string.IsNullOrWhiteSpace(importedPlan) && importedPlan != plan, "import defaults to creating a new operation plan");
        Check(Text(imported!, "operationPlanName") == "导入 ' 参数测试", "plan name containing a quote is stored exactly");
        Check(PlanState(plan) == originalPlan, "creating an imported plan preserves the previously selected plan and its existing train");
        Check(RouteTimesState("arrival-existing") == originalTimes, "reusing an existing route never overwrites shared occupation settings");
        CheckExactOccupancy(importedPlan, "T'01");
        await Reject("ImportCellOccupancy", payload, HttpStatusCode.Conflict);

        var append = Payload(Csv("T');--"), target: importedPlan, createNew: false);
        preview = await Preview(append);
        Check(Bool(preview, "valid"), "append of a different train previews successfully");
        append["previewToken"] = Text(preview, "previewToken");
        var appendResponse = await Request("ImportCellOccupancy", append);
        Check(Text(appendResponse!, "operationPlanID") == importedPlan, "append retains the chosen plan ID");
        Check(TrainCount(importedPlan) == 2, "quoted SQL-like train IDs are imported literally without affecting any other rows");
        CheckExactOccupancy(importedPlan, "T');--");
        var duplicate = await Preview(Payload(Csv("T'01"), target: importedPlan, createNew: false));
        Check(!Bool(duplicate, "valid") && duplicate["errors"]!.AsArray().Count > 0,
            "appending an existing train refuses the complete batch before any write");
        Check(TrainCount(importedPlan) == 2 && PlanState(plan) == originalPlan, "duplicate import preserves both plans and all trains");

        // A preview is tied to both the submitted content and the station snapshot.
        var changedPayload = Payload(Csv("changed-after-preview"));
        preview = await Preview(changedPayload);
        changedPayload["previewToken"] = Text(preview, "previewToken");
        changedPayload["csvText"] = Csv("other-file");
        await Reject("ImportCellOccupancy", changedPayload, HttpStatusCode.Conflict);
        var stale = Payload(Csv("stale-catalog"));
        preview = await Preview(stale);
        stale["previewToken"] = Text(preview, "previewToken");
        db.ExecuteNonQuery($"UPDATE cell SET Name='changed-after-preview' WHERE {schemeFilter} AND ID='d'", args);
        await Reject("ImportCellOccupancy", stale, HttpStatusCode.Conflict);
        db.ExecuteNonQuery($"UPDATE cell SET Name='disconnected' WHERE {schemeFilter} AND ID='d'", args);

        // ABORT must roll back earlier inserts. IGNORE must also fail rather than committing a partial import.
        foreach (var (table, action) in new[] {
            ("movement", "ABORT"), ("movement", "IGNORE"), ("train", "IGNORE"), ("operationplan", "IGNORE"),
            ("stationroute", "IGNORE"), ("stationroutetime", "ABORT"), ("stationroutetime", "IGNORE")
        })
        {
            // Slightly different per-Cell shifts force a new route without changing the topology.
            var rollback = Payload(Csv("rollback-" + table + "-" + action).Replace("08:00:01,08:00:55", "08:00:02,08:00:56"));
            preview = await Preview(rollback);
            Check(Bool(preview, "valid"), "rollback fixture previews successfully");
            rollback["previewToken"] = Text(preview, "previewToken");
            const string trigger = "cell_import_failure";
            db.ExecuteNonQuery($"CREATE TRIGGER {trigger} BEFORE INSERT ON {table} WHEN NEW.InstanceID='{owned}' " +
                $"BEGIN SELECT RAISE({action}" + (action == "ABORT" ? ", 'intentional Cell import regression'" : "") + "); END");
            try { await Reject("ImportCellOccupancy", rollback, HttpStatusCode.InternalServerError); }
            finally { db.ExecuteNonQuery($"DROP TRIGGER {trigger}"); }
        }
        Check(RouteTimesState("arrival-existing") == originalTimes && PlanState(plan) == originalPlan,
            "all forced failures preserve shared route settings and original plan data");
        Console.WriteLine($"CellOccupancy import HTTP/SQLite checks passed ({checks} assertions).");
        return checks;

        void Seed()
        {
            db.ExecuteNonQuery("INSERT INTO capacityinstance (ID,Name,Owner,IsActive) VALUES " +
                "(@owned,'Cell导入测试','owner',1),(@foreign,'其他所有者','someone-else',1)", args);
            db.ExecuteNonQuery("INSERT INTO stationscheme (InstanceID,ID,Name) VALUES " +
                "(@owned,@scheme,'Cell导入测试'),(@owned,'other','其他站場'),(@foreign,@scheme,'其他所有者')", args);
            db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) VALUES " +
                "(@owned,@scheme,'selected','原计划'),(@foreign,@scheme,'selected','其他所有者')", args);
            db.ExecuteNonQuery("INSERT INTO train (InstanceID,StationSchemeID,OperationPlanID,ID,TrainNumber,Name,TrainType,IsFixedOperation) " +
                "VALUES (@owned,@scheme,'selected','original-train','original-train','原车次','旅客列车',1)", args);
            foreach (var id in new[] { 1, 2, 3, 4, 8, 9 })
                db.ExecuteNonQuery("INSERT INTO node (InstanceID,StationSchemeID,ID,X,Y) VALUES (@owned,@scheme,@id,@x,0)",
                    new { owned, scheme, id, x = id * 100 });
            db.ExecuteNonQuery("INSERT INTO link (InstanceID,StationSchemeID,ID,Name,FromNodeID,ToNodeID) VALUES " +
                "(@owned,@scheme,11,'',1,2),(@owned,@scheme,12,'1道',2,3),(@owned,@scheme,13,'',3,4),(@owned,@scheme,14,'',8,9)", args);
            foreach (var cell in new[] { ("a", "A, \"入口\"", "[11]"), ("b", "1G", "[12]"), ("c", "出口", "[13]"), ("d", "disconnected", "[14]") })
                db.ExecuteNonQuery("INSERT INTO cell (InstanceID,StationSchemeID,ID,Name,LinkIDList) VALUES (@owned,@scheme,@id,@name,@links)",
                    new { owned, scheme, id = cell.Item1, name = cell.Item2, links = cell.Item3 });
            db.ExecuteNonQuery("INSERT INTO cell (InstanceID,StationSchemeID,ID,Name,LinkIDList) VALUES (@owned,'other','foreign-only','其他站场Cell','[11]')", args);
            db.ExecuteNonQuery("INSERT INTO stationroute (InstanceID,StationSchemeID,ID,Type,Description,NodeList,LinkList,CellList,StartNodeID,EndNodeID) " +
                "VALUES (@owned,@scheme,'arrival-existing','Arrival','原接车进路','[1,2,3]','[11,12]','[\"a\",\"b\"]','1','3')", args);
            foreach (var time in new[] { ("旅客列车", "a", -10, -7), ("旅客列车", "b", 0, 0), ("other-type", "a", 777, 888) })
                db.ExecuteNonQuery("INSERT INTO stationroutetime (InstanceID,StationSchemeID,RouteID,TrainTypeID,CellID,StartOccupationShift,EndOccupationShift) " +
                    "VALUES (@owned,@scheme,'arrival-existing',@trainType,@cell,@start,@end)",
                    new { owned, scheme, trainType = time.Item1, cell = time.Item2, start = time.Item3, end = time.Item4 });
        }

        void CheckExactOccupancy(string targetPlan, string trainId)
        {
            var query = new { owned, scheme, targetPlan, trainId };
            var train = db.Query<TrainRow>($"SELECT * FROM train WHERE {schemeFilter} AND OperationPlanID=@targetPlan AND ID=@trainId", query)!.Single();
            var moves = db.Query<MovementRow>($"SELECT * FROM movement WHERE {schemeFilter} AND OperationPlanID=@targetPlan AND TrainID=@trainId ORDER BY SortOrder", query)!.ToList();
            Check(moves.Count == 3 && moves.Select(move => move.MovementID).SequenceEqual(new[] { "0", "1", "2" }), "all CSV MovementIDs persist in operation order");
            var expected = new[] {
                new[] { ("a", 28801, 28855), ("b", 28811, 28862) },
                new[] { ("b", 28862, 29043) },
                new[] { ("b", 29043, 29104), ("c", 29042, 29104) }
            };
            for (var index = 0; index < moves.Count; index++)
            {
                var move = moves[index];
                var route = db.Query<StationRouteRow>($"SELECT * FROM stationroute WHERE {schemeFilter} AND ID=@id", new { owned, scheme, id = move.Route })!.Single();
                var routeTimes = db.Query<StationRouteTimeRow>($"SELECT * FROM stationroutetime WHERE {schemeFilter} AND RouteID=@id",
                    new { owned, scheme, id = move.Route })!.ToList();
                var times = routeTimes.Any(time => time.TrainTypeID == train.TrainType)
                    ? routeTimes.Where(time => time.TrainTypeID == train.TrainType).ToList()
                    : routeTimes.Where(time => string.IsNullOrWhiteSpace(time.TrainTypeID)).ToList();
                Check(times.Count == expected[index].Length, "movement route has every Cell occupation for the imported train type");
                foreach (var (cell, start, end) in expected[index])
                {
                    var time = times.Single(item => item.CellID == cell);
                    Check(Seconds(move.EarliestStartTime!) + time.StartOccupationShift == start && Seconds(move.LatestEndTime!) + time.EndOccupationShift == end,
                        $"{trainId}/{move.MovementID}/{cell} reconstructs exact CSV start/end seconds, including advance locking");
                }
                if (index == 1) Check(route.Type == "Dwelling", "Dw imports a Dwelling route for stationary 2D and 3D animation");
                if (index == 2) Check(times.Any(time => time.StartOccupationShift < 0), "departure retains an early-locking Cell with a negative offset");
            }
        }

        async Task<JsonNode> Preview(JsonObject body)
        {
            var before = State();
            var response = (await Request("PreviewCellOccupancyImport", body))!;
            Check(State() == before, "preview does not persist any changes");
            return response;
        }
        async Task Reject(string endpoint, JsonObject body, HttpStatusCode status, string? user = "owner")
        {
            var before = State();
            await Request(endpoint, body, status, user);
            Check(State() == before, $"{endpoint} HTTP {(int)status} preserves every test row atomically");
        }
        async Task<JsonNode?> Request(string endpoint, JsonObject body, HttpStatusCode status = HttpStatusCode.OK, string? user = "owner")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "../OperationPlan/" + endpoint) { Content = JsonContent.Create(body, options: json) };
            if (user is not null) request.Headers.Add("X-Test-User", user);
            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == status, $"{endpoint} expected {(int)status}, received {(int)response.StatusCode}: {content}");
            if (string.IsNullOrWhiteSpace(content)) return null;
            try { return JsonNode.Parse(content); }
            catch (JsonException) { return JsonValue.Create(content); }
        }

        JsonObject Payload(string text, string instance = owned, string station = scheme, string target = plan, bool createNew = true) => new() {
            ["instanceID"] = instance, ["stationSchemeID"] = station, ["operationPlanID"] = target,
            ["csvText"] = text, ["fileName"] = "CellOccupancy.csv", ["createNewPlan"] = createNew,
            ["planName"] = "导入 ' 参数测试", ["cellMappings"] = new JsonObject()
        };
        string State() => JsonSerializer.Serialize(new {
            plans = db.Query<OperationPlanRow>($"SELECT * FROM operationplan WHERE {fixtureFilter} ORDER BY InstanceID,StationSchemeID,OperationPlanID", args),
            trains = db.Query<TrainRow>($"SELECT * FROM train WHERE {fixtureFilter} ORDER BY InstanceID,StationSchemeID,OperationPlanID,ID", args),
            moves = db.Query<MovementRow>($"SELECT * FROM movement WHERE {fixtureFilter} ORDER BY InstanceID,StationSchemeID,OperationPlanID,TrainID,MovementID", args),
            routes = db.Query<StationRouteRow>($"SELECT * FROM stationroute WHERE {fixtureFilter} ORDER BY InstanceID,StationSchemeID,ID", args),
            times = db.Query<StationRouteTimeRow>($"SELECT * FROM stationroutetime WHERE {fixtureFilter} ORDER BY InstanceID,StationSchemeID,RouteID,TrainTypeID,CellID", args),
            cells = db.Query<StationCellRow>($"SELECT * FROM cell WHERE {fixtureFilter} ORDER BY InstanceID,StationSchemeID,ID", args)
        }, json);
        string PlanState(string targetPlan) => JsonSerializer.Serialize(new {
            plans = db.Query<OperationPlanRow>($"SELECT * FROM operationplan WHERE {schemeFilter} AND OperationPlanID=@targetPlan", new { owned, scheme, targetPlan }),
            trains = db.Query<TrainRow>($"SELECT * FROM train WHERE {schemeFilter} AND OperationPlanID=@targetPlan ORDER BY ID", new { owned, scheme, targetPlan }),
            moves = db.Query<MovementRow>($"SELECT * FROM movement WHERE {schemeFilter} AND OperationPlanID=@targetPlan ORDER BY TrainID,MovementID", new { owned, scheme, targetPlan })
        }, json);
        string RouteTimesState(string route) => JsonSerializer.Serialize(db.Query<StationRouteTimeRow>(
            $"SELECT * FROM stationroutetime WHERE {schemeFilter} AND RouteID=@route ORDER BY TrainTypeID,CellID", new { owned, scheme, route }), json);
        int TrainCount(string targetPlan) => db.Query<int>($"SELECT COUNT(*) FROM train WHERE {schemeFilter} AND OperationPlanID=@targetPlan", new { owned, scheme, targetPlan })!.Single();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("CellOccupancy import assertion failed: " + message);
            checks++;
        }
    }

    private static string Csv(string train) => "\uFEFF" + Header + "\r\n" + string.Join("\r\n", new[] {
        $"{train},0,Arr,\"A, \"\"入口\"\"\",08:00:01,08:00:55",
        $"{train},0,Arr,1G,08:00:11,08:01:02",
        $"{train},1,Dw,1G,08:01:02,08:04:03",
        $"{train},2,Dep,1G,08:04:03,08:05:04",
        $"{train},2,Dep,c,08:04:02,08:05:04"
    }) + "\r\n";
    private static bool Bool(JsonNode node, string property) => node[property]!.GetValue<bool>();
    private static int Int(JsonNode node, string property) => node[property]!.GetValue<int>();
    private static string Text(JsonNode node, string property) => node[property]?.GetValue<string>() ?? "";
    private static int Seconds(string value) => (int)TimeSpan.Parse(value, CultureInfo.InvariantCulture).TotalSeconds;
}
