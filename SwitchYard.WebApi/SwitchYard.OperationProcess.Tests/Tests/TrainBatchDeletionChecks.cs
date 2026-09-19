using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SwitchYard.Capacity;
using SwitchYard.Service;
using SwitchYard.Service.Controllers;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

/// <summary>Scoped, transactional batch deletion against the existing test-only SQLite host.</summary>
public static class TrainBatchDeletionChecks
{
    public static async Task<int> Run(HttpClient client)
    {
        const string owned = "batch-delete-owned";
        const string foreign = "batch-delete-foreign";
        const string filter = "InstanceID = @InstanceID AND StationSchemeID = @StationSchemeID AND OperationPlanID = @OperationPlanID";
        const string allTestScopes = "InstanceID IN (@owned, @foreign)";
        var checks = 0;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var db = DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        var main = Scope(owned, "main", "selected");
        var otherPlan = Scope(owned, "main", "other-plan");
        var otherScheme = Scope(owned, "other-scheme", "selected");
        var otherOwner = Scope(foreign, "main", "selected");
        var rollback = Scope(owned, "main", "rollback");
        var bulk = Scope(owned, "main", "bulk");
        var scopes = new[] { main, otherPlan, otherScheme, otherOwner, rollback, bulk };
        OperationProcessController.EnsureSchema(db);
        TrainProcessSnapshotStore.EnsureSchema(db);
        db.ExecuteNonQuery("INSERT INTO capacityinstance (ID,Name,Owner,IsActive) VALUES " +
            "(@owned,'批量删除测试','owner',1),(@foreign,'其他所有者','someone-else',1)", new { owned, foreign });
        foreach (var scope in scopes.DistinctBy(item => (item.InstanceID, item.StationSchemeID)))
            db.ExecuteNonQuery("INSERT INTO stationscheme (InstanceID,ID,Name) VALUES (@InstanceID,@StationSchemeID,'批量删除测试')", scope);
        foreach (var scope in scopes)
        {
            db.ExecuteNonQuery("INSERT INTO operationplan (InstanceID,StationSchemeID,OperationPlanID,Name) VALUES " +
                "(@InstanceID,@StationSchemeID,@OperationPlanID,'批量删除测试')", scope);
            SeedTemplates(scope);
        }
        SeedTrain(main, "process-a", snapshot: true);
        SeedTrain(main, "ordinary-a", movementCount: 2);
        SeedTrain(main, "keep", snapshot: true, movementCount: 2);
        SeedTrain(main, "CaseTrain");
        SeedTrain(main, "casetrain");
        SeedTrain(main, "train-'quoted");
        var maximumLengthId = new string('x', 50);
        SeedTrain(main, maximumLengthId);
        foreach (var scope in new[] { otherPlan, otherScheme, otherOwner }) SeedTrain(scope, "process-a", snapshot: true);
        SeedTrain(otherPlan, "other-plan-only");
        SeedTrain(otherScheme, "other-scheme-only");
        SeedTrain(otherOwner, "other-owner-only");
        SeedTrain(otherOwner, "admin-one", snapshot: true);
        SeedTrain(otherOwner, "admin-two", snapshot: true);
        var untouchedTemplates = TemplatesState();

        // Each rejected request must leave all test scopes and dependencies unchanged.
        await Reject(Payload(main, ["keep"]), HttpStatusCode.Unauthorized, user: null);
        await Reject(Payload(main, ["keep"]), HttpStatusCode.Forbidden, user: "outsider");
        await Reject(Payload(otherOwner, ["other-owner-only"]), HttpStatusCode.Forbidden);
        await Reject(null, HttpStatusCode.BadRequest);
        await Reject(Payload(main, null), HttpStatusCode.BadRequest);
        await Reject(Payload(main, []), HttpStatusCode.BadRequest);
        await Reject(Payload(main, [null]), HttpStatusCode.BadRequest);
        await Reject(Payload(main, [""]), HttpStatusCode.BadRequest);
        await Reject(Payload(main, ["   "]), HttpStatusCode.BadRequest);
        await Reject(Payload(main, [new string('x', 51)]), HttpStatusCode.BadRequest);
        await Reject(Payload(main, Enumerable.Repeat("keep", 10001)), HttpStatusCode.BadRequest);
        await Reject(Payload(Scope("", "main", "selected"), ["keep"]), HttpStatusCode.BadRequest);
        await Reject(Payload(Scope(owned, "main", new string('p', 51)), ["keep"]), HttpStatusCode.BadRequest);
        await Reject(Payload(Scope("batch-delete-missing", "main", "selected"), ["keep"]), HttpStatusCode.NotFound);
        await Reject(Payload(Scope(owned, "missing-scheme", "selected"), ["keep"]), HttpStatusCode.NotFound);
        await Reject(Payload(Scope(owned, "main", "missing-plan"), ["keep"]), HttpStatusCode.NotFound);
        var missing = await Reject(Payload(main, ["keep", "missing-train", "missing-train"]), HttpStatusCode.Conflict);
        Check(IDs(missing!, "missingTrainIDs").SequenceEqual(["missing-train"]), "missing train IDs are reported once without deleting a valid selection");
        foreach (var outsideId in new[] { "other-plan-only", "other-scheme-only", "other-owner-only" })
        {
            var conflict = await Reject(Payload(main, ["keep", outsideId]), HttpStatusCode.Conflict);
            Check(IDs(conflict!, "missingTrainIDs").SequenceEqual([outsideId]), "a train in another scope cannot satisfy current-scope membership");
        }
        await Reject(Payload(main, ["keep", "x') OR 1=1 --"]), HttpStatusCode.Conflict);

        var remainingTrain = TrainState(main, "keep");
        var outside = OutsideMainState();
        var deleted = await Request(Payload(main, [" process-a ", "ordinary-a", "process-a", "ordinary-a"]));
        Check(deleted!["deletedCount"]!.GetValue<int>() == 2 && IDs(deleted, "deletedTrainIDs").SequenceEqual(["process-a", "ordinary-a"]),
            "success trims and deduplicates IDs while preserving first input order");
        Check(Counts(main) == (5, 6, 1), "selected normal and process trains lose all movements and snapshots together");
        Check(TrainState(main, "process-a") == EmptyTrainState() && TrainState(main, "ordinary-a") == EmptyTrainState(),
            "deleted trains have no remaining train, movement, or snapshot rows");
        Check(TrainState(main, "keep") == remainingTrain, "unselected train rows, movements, and process document remain byte-for-byte intact");
        Check(OutsideMainState() == outside, "same train ID in another instance, scheme, or plan is unaffected");
        Check(TemplatesState() == untouchedTemplates, "batch deletion preserves train templates, movement templates, and original processes");

        deleted = await Request(Payload(main, ["CaseTrain", "casetrain", "CaseTrain", "train-'quoted", " " + maximumLengthId + " "]));
        Check(deleted!["deletedCount"]!.GetValue<int>() == 4 &&
            IDs(deleted, "deletedTrainIDs").SequenceEqual(["CaseTrain", "casetrain", "train-'quoted", maximumLengthId]),
            "ordinal case-distinct IDs, a quoted ID, and the 50-character boundary are handled exactly");
        Check(Counts(main) == (1, 2, 1) && TrainState(main, "keep") == remainingTrain, "only the intended remaining train survives deduplicated deletion");

        // Exercise both role forms against an instance owned by someone else.
        await Request(Payload(otherOwner, ["admin-one"]), user: "admin");
        await Request(Payload(otherOwner, ["admin-two"]), user: "multi-role-admin");
        Check(TrainState(otherOwner, "admin-one") == EmptyTrainState() && TrainState(otherOwner, "admin-two") == EmptyTrainState(),
            "admin role, including a later role claim, may delete another owner's selected trains");
        Check(Counts(otherOwner) == (2, 2, 1), "administrator deletion preserves other trains in that instance");

        foreach (var id in new[] { "rollback-a", "rollback-b", "rollback-c" }) SeedTrain(rollback, id, snapshot: true, movementCount: 2);
        var rollbackIds = new[] { "rollback-c", "rollback-a", "rollback-b" };
        // ABORT occurs after child tables have been cleared; IGNORE requires detecting
        // silently retained rows rather than relying on an exception from SQLite.
        await TriggerFailure("train", "ID", "ABORT", "rollback-b", rollback, rollbackIds);
        await TriggerFailure("movement", "TrainID", "IGNORE", "rollback-b", rollback, rollbackIds);
        await TriggerFailure("trainprocesssnapshot", "TrainID", "IGNORE", "rollback-b", rollback, rollbackIds);
        await TriggerFailure("train", "ID", "IGNORE", "rollback-b", rollback, rollbackIds);
        await Request(Payload(rollback, rollbackIds));
        Check(Counts(rollback) == (0, 0, 0), "transaction failures leave the endpoint usable for a subsequent complete deletion");

        var bulkIds = Enumerable.Range(0, 405).Select(index => $"bulk-{index:000}").ToArray();
        db.BeginTransaction();
        try
        {
            for (var index = 0; index < bulkIds.Length; index++)
                SeedTrain(bulk, bulkIds[index], snapshot: index is 0 or 400 or 404);
            db.Commit();
        }
        catch { db.Rollback(); throw; }
        Check(Counts(bulk) == (405, 405, 3), "large-batch fixture includes process snapshots on both sides of the parameter batch boundary");
        // The second chunk fails after the first 400 trains were deleted: a single
        // transaction must restore both chunks, not merely the failed SQL statement.
        await TriggerFailure("train", "ID", "ABORT", "bulk-404", bulk, bulkIds);
        deleted = await Request(Payload(bulk, bulkIds.Reverse()));
        Check(deleted!["deletedCount"]!.GetValue<int>() == 405 && IDs(deleted, "deletedTrainIDs").SequenceEqual(bulkIds.Reverse()),
            "more than 400 IDs delete successfully and response retains request order across chunks");
        Check(Counts(bulk) == (0, 0, 0), "all rows and snapshots are deleted across multiple parameter batches");
        Check(TrainState(main, "keep") == remainingTrain && TemplatesState() == untouchedTemplates,
            "large batches and forced failures preserve unrelated data and all source templates");
        return checks;

        void SeedTemplates(ProcessScope scope)
        {
            db.ExecuteNonQuery("INSERT INTO traintemplate (InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID,Name,Type,Number,IsFixedOperation) " +
                "VALUES (@InstanceID,@StationSchemeID,@OperationPlanID,'batch-template','不可删除的列车模板','',2,0)", scope);
            db.ExecuteNonQuery("INSERT INTO movementtemplate (InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID,MovementID,Name,RouteIDList,MinDuration,SortOrder) " +
                "VALUES (@InstanceID,@StationSchemeID,@OperationPlanID,'batch-template','batch-template-move','模板活动','',60,0)", scope);
            var process = Process(scope);
            db.ExecuteNonQuery("INSERT INTO operationprocesstemplate (InstanceID,StationSchemeID,OperationPlanID,TemplateID,Revision,Document,UpdatedAtUtc) " +
                "VALUES (@InstanceID,@StationSchemeID,@OperationPlanID,@TemplateID,1,@Document,@UpdatedAtUtc)",
                new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TemplateID = process.Id,
                    Document = JsonSerializer.Serialize(process, json), UpdatedAtUtc = new DateTime(2026, 1, 1) });
        }

        void SeedTrain(ProcessScope scope, string id, bool snapshot = false, int movementCount = 1)
        {
            db.ExecuteNonQuery("INSERT INTO train (InstanceID,StationSchemeID,OperationPlanID,ID,TrainTemplateID,TrainNumber,Name,TrainType,IsFixedOperation) " +
                "VALUES (@InstanceID,@StationSchemeID,@OperationPlanID,@ID,'batch-template',@ID,@ID,'',0)",
                new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, ID = id });
            for (var index = 0; index < movementCount; index++)
                db.ExecuteNonQuery("INSERT INTO movement (InstanceID,StationSchemeID,OperationPlanID,TrainID,TrainTemplateID,MovementID,Name,RouteIDList,MinDuration,EarliestStartTime,LatestEndTime,Route,Tag,SortOrder) " +
                    "VALUES (@InstanceID,@StationSchemeID,@OperationPlanID,@TrainID,'batch-template',@MovementID,'实际活动','',60,'08:00','08:01','','',@SortOrder)",
                    new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, TrainID = id, MovementID = id + "-m" + index, SortOrder = index });
            if (snapshot)
                TrainProcessSnapshotStore.Insert(db, scope, new TrainProcessSnapshot {
                    InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
                    TrainID = id, SourceTemplateID = "batch-process", SourceName = "不可删除的原作业过程", SourceRevision = 1,
                    OriginSeconds = 28800, Process = Process(scope), ActivityMovementMap = new() { ["activity"] = id + "-m0" },
                    EventTimes = new() { ["start"] = 28800, ["end"] = 28860 }, SelectedTrackIDs = new() { ["activity"] = "10" }
                });
        }

        async Task TriggerFailure(string table, string key, string action, string id, ProcessScope scope, string[] ids)
        {
            var trigger = "batch_delete_failure";
            // Every interpolated identifier/value here is an internal test constant.
            db.ExecuteNonQuery($"CREATE TRIGGER {trigger} BEFORE DELETE ON {table} " +
                $"WHEN OLD.InstanceID='{scope.InstanceID}' AND OLD.StationSchemeID='{scope.StationSchemeID}' AND " +
                $"OLD.OperationPlanID='{scope.OperationPlanID}' AND OLD.{key}='{id}' BEGIN SELECT RAISE({action}" +
                (action == "ABORT" ? ", 'intentional batch-delete regression'" : "") + "); END");
            try { await Reject(Payload(scope, ids), HttpStatusCode.InternalServerError); }
            finally { db.ExecuteNonQuery($"DROP TRIGGER {trigger}"); }
        }

        async Task<JsonNode?> Reject(object? payload, HttpStatusCode status, string? user = "owner")
        {
            var before = State();
            var response = await Request(payload, user, status);
            Check(State() == before, $"HTTP {(int)status} leaves every seeded row, dependency, and template unchanged");
            return response;
        }

        async Task<JsonNode?> Request(object? payload, string? user = "owner", HttpStatusCode status = HttpStatusCode.OK)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "../OperationPlan/DeleteTrains")
                { Content = JsonContent.Create(payload, options: json) };
            if (user is not null) request.Headers.Add("X-Test-User", user);
            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == status, $"batch deletion expected {(int)status}, received {(int)response.StatusCode}: {content}");
            return string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);
        }

        (int, int, int) Counts(ProcessScope scope) => (
            db.Query<int>($"SELECT COUNT(*) FROM train WHERE {filter}", scope)!.Single(),
            db.Query<int>($"SELECT COUNT(*) FROM movement WHERE {filter}", scope)!.Single(),
            db.Query<int>($"SELECT COUNT(*) FROM trainprocesssnapshot WHERE {filter}", scope)!.Single());

        string State() => JsonSerializer.Serialize(new {
            trains = db.Query<TrainRow>($"SELECT * FROM train WHERE {allTestScopes} ORDER BY InstanceID,StationSchemeID,OperationPlanID,ID", new { owned, foreign }),
            movements = db.Query<MovementRow>($"SELECT * FROM movement WHERE {allTestScopes} ORDER BY InstanceID,StationSchemeID,OperationPlanID,TrainID,MovementID", new { owned, foreign }),
            snapshots = db.Query<SnapshotDocument>($"SELECT * FROM trainprocesssnapshot WHERE {allTestScopes} ORDER BY InstanceID,StationSchemeID,OperationPlanID,TrainID", new { owned, foreign }),
            templates = TemplatesState()
        }, json);

        string OutsideMainState() => JsonSerializer.Serialize(new[] { otherPlan, otherScheme, otherOwner }
            .Select(scope => new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID,
                trains = db.Query<TrainRow>($"SELECT * FROM train WHERE {filter} ORDER BY ID", scope),
                movements = db.Query<MovementRow>($"SELECT * FROM movement WHERE {filter} ORDER BY TrainID,MovementID", scope),
                snapshots = db.Query<SnapshotDocument>($"SELECT * FROM trainprocesssnapshot WHERE {filter} ORDER BY TrainID", scope)
            }), json);

        string TemplatesState() => JsonSerializer.Serialize(new {
            trains = db.Query<TrainTemplateRow>($"SELECT * FROM traintemplate WHERE {allTestScopes} ORDER BY InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID", new { owned, foreign }),
            movements = db.Query<MovementTemplateRow>($"SELECT * FROM movementtemplate WHERE {allTestScopes} ORDER BY InstanceID,StationSchemeID,OperationPlanID,TrainTemplateID,MovementID", new { owned, foreign }),
            processes = db.Query<ProcessDocument>($"SELECT * FROM operationprocesstemplate WHERE {allTestScopes} ORDER BY InstanceID,StationSchemeID,OperationPlanID,TemplateID", new { owned, foreign })
        }, json);

        string TrainState(ProcessScope scope, string id)
        {
            var args = new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, id };
            return JsonSerializer.Serialize(new {
                trains = db.Query<TrainRow>($"SELECT * FROM train WHERE {filter} AND ID=@id", args),
                movements = db.Query<MovementRow>($"SELECT * FROM movement WHERE {filter} AND TrainID=@id ORDER BY MovementID", args),
                snapshots = db.Query<SnapshotDocument>($"SELECT * FROM trainprocesssnapshot WHERE {filter} AND TrainID=@id", args)
            }, json);
        }
        string EmptyTrainState() => JsonSerializer.Serialize(new { trains = new TrainRow[0], movements = new MovementRow[0], snapshots = new SnapshotDocument[0] }, json);
        static string[] IDs(JsonNode response, string property) => response[property]!.AsArray().Select(id => id!.GetValue<string>()).ToArray();
        static ProcessScope Scope(string instance, string scheme, string plan) => new() { InstanceID = instance, StationSchemeID = scheme, OperationPlanID = plan };
        static object Payload(ProcessScope scope, IEnumerable<string?>? ids) => new { scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, trainIDs = ids?.ToArray() };
        static OperationProcessTemplate Process(ProcessScope scope) => new() {
            Id = "batch-process", Name = "不可删除的原作业过程", Revision = 1,
            InstanceID = scope.InstanceID, StationSchemeID = scope.StationSchemeID, OperationPlanID = scope.OperationPlanID,
            Activities = [new() { Id = "activity", Name = "停留", Type = "Dwelling", MinDuration = 1, MaxDuration = 2,
                StartEvent = "start", EndEvent = "end", TrackList = ["10"], SelectedTrack = "10" }],
            Events = [new() { Id = "start", Name = "停留开始", Time = 0 }, new() { Id = "end", Name = "停留结束", Time = 1 }]
        };
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Batch deletion assertion failed: " + message);
            checks++;
        }
    }

    private sealed class SnapshotDocument : ProcessScope
    {
        public string TrainID { get; set; } = "";
        public string Document { get; set; } = "";
    }
    private sealed class ProcessDocument : ProcessScope
    {
        public string TemplateID { get; set; } = "";
        public int Revision { get; set; }
        public string Document { get; set; } = "";
        public DateTime UpdatedAtUtc { get; set; }
    }
}
