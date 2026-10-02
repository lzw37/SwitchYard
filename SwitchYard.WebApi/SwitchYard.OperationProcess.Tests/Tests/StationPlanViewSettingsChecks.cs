using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SwitchYard.Capacity;
using SwitchYard.Service;

public static class StationPlanViewSettingsChecks
{
    public static async Task<int> Run(HttpClient client)
    {
        var checks = 0;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        const string instanceID = "station-plan-view-owned", stationSchemeID = "main", operationPlanID = "plan";
        var scope = new { instanceID, stationSchemeID, operationPlanID };
        var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        db.ExecuteNonQuery("INSERT INTO capacityinstance (ID,Name,Owner,IsActive) VALUES (@instanceID,'Station plan view test','owner',1)", scope);
        db.ExecuteNonQuery("INSERT INTO stationscheme (InstanceID,ID,Name) VALUES (@instanceID,@stationSchemeID,'Station plan view test')", scope);
        await Api(HttpMethod.Post, "CreateOperationPlan", new { instanceID, stationSchemeID, operationPlanID, name = "中观图" });
        var initial = await Load(operationPlanID);
        Check(!initial.IsConfigured, "a new plan uses the default selection");
        var selection = new StationPlanViewSettingsRequest { InstanceID = instanceID, StationSchemeID = stationSchemeID, OperationPlanID = operationPlanID, CellIDs = [" c1 ", "c2", "c1"], EndpointNodeIDs = ["east", "west"] };
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", selection);
        var loaded = await Load(operationPlanID);
        Check(loaded.IsConfigured && loaded.CellIDs.SequenceEqual(["c1", "c2"]) && loaded.EndpointNodeIDs.SequenceEqual(["east", "west"]), "selection is normalized and reloaded from SQLite");
        await Api(HttpMethod.Get, Query(operationPlanID), user: null, expected: HttpStatusCode.Unauthorized);
        await Api(HttpMethod.Get, Query(operationPlanID), user: "outsider", expected: HttpStatusCode.Unauthorized);
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", selection, user: "outsider", expected: HttpStatusCode.Unauthorized);
        await Api(HttpMethod.Get, Query("missing"), expected: HttpStatusCode.NotFound);
        var invalid = new StationPlanViewSettingsRequest { InstanceID = instanceID, StationSchemeID = stationSchemeID, OperationPlanID = "missing", CellIDs = [], EndpointNodeIDs = [] };
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", invalid, expected: HttpStatusCode.NotFound);
        invalid.OperationPlanID = operationPlanID;
        invalid.CellIDs = null;
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", invalid, expected: HttpStatusCode.BadRequest);
        invalid.CellIDs = [new string('x', 51)];
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", invalid, expected: HttpStatusCode.BadRequest);
        invalid.CellIDs = [""];
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", invalid, expected: HttpStatusCode.BadRequest);
        invalid.CellIDs = Enumerable.Repeat("cell", 2001).ToList();
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", invalid, expected: HttpStatusCode.BadRequest);
        Check((await Load(operationPlanID)).CellIDs.SequenceEqual(["c1", "c2"]), "rejected updates preserve the saved selection");
        await Api(HttpMethod.Post, "CreateOperationPlan", new { instanceID, stationSchemeID, operationPlanID = "other", name = "其他计划" });
        Check(!(await Load("other")).IsConfigured, "settings are isolated by operation plan");

        // Prove delete-and-insert is atomic even if the insert silently writes no row.
        db.ExecuteNonQuery("CREATE TRIGGER ignore_station_plan_settings BEFORE INSERT ON stationplanviewsettings WHEN NEW.InstanceID='station-plan-view-owned' BEGIN SELECT RAISE(IGNORE); END");
        try { await Api(HttpMethod.Put, "SaveStationPlanViewSettings", selection, expected: HttpStatusCode.InternalServerError); }
        finally { db.ExecuteNonQuery("DROP TRIGGER ignore_station_plan_settings"); }
        Check((await Load(operationPlanID)).CellIDs.SequenceEqual(["c1", "c2"]), "failed save rolls back to the previous configuration");

        await Api(HttpMethod.Post, "CopyOperationPlan", new { instanceID, stationSchemeID, sourceOperationPlanID = operationPlanID, operationPlanID = "copy", name = "复制" });
        Check((await Load("copy")).EndpointNodeIDs.SequenceEqual(["east", "west"]), "plan copy retains axis selections");
        await Api(HttpMethod.Put, "EditOperationPlan", new { instanceID, stationSchemeID, originalOperationPlanID = "copy", operationPlanID = "renamed", name = "重命名" });
        Check((await Load("renamed")).CellIDs.SequenceEqual(["c1", "c2"]), "plan ID change rebinds axis selections");
        selection.CellIDs = [];
        selection.EndpointNodeIDs = [];
        await Api(HttpMethod.Put, "SaveStationPlanViewSettings", selection);
        loaded = await Load(operationPlanID);
        Check(loaded.IsConfigured && loaded.CellIDs.Count == 0 && loaded.EndpointNodeIDs.Count == 0, "explicit empty selection remains empty after reload");
        Check((await Load("renamed")).CellIDs.Count == 2, "editing source settings does not change copied settings");
        await Api(HttpMethod.Delete, $"DeleteOperationPlan?instanceID={instanceID}&stationSchemeID={stationSchemeID}&operationPlanID=renamed");
        Check(db.Query<CountRow>("SELECT COUNT(*) AS Value FROM stationplanviewsettings WHERE InstanceID=@instanceID AND OperationPlanID='renamed'", scope)!.Single().Value == 0, "plan deletion removes its settings");
        Console.WriteLine($"Station plan view settings HTTP/SQLite checks passed ({checks} assertions).");
        return checks;

        string Query(string plan) => $"GetStationPlanViewSettings?instanceID={instanceID}&stationSchemeID={stationSchemeID}&operationPlanID={plan}";
        async Task<StationPlanViewSettings> Load(string plan) => JsonSerializer.Deserialize<StationPlanViewSettings>(await Api(HttpMethod.Get, Query(plan)), json)!;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException("Station plan view settings: " + message); }
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
    private sealed class CountRow { public int Value { get; set; } }
}
