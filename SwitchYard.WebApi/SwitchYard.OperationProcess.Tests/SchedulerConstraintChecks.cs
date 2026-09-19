using System.Text.Json;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;

/// <summary>Independent, database-free checks of temporal propagation and joint resource choice.</summary>
public static class SchedulerConstraintChecks
{
    public static int Run()
    {
        var checks = 0;
        var catalog = Catalog();

        // Uniform slots position whole trains. Within a train, unconstrained events
        // take their earliest simultaneous feasible times rather than separate slots.
        var continuous = Process(Activity("first", "a", "b", 1, 3, "r12"),
            Activity("second", "c", "d", 1, 3, "r34"));
        continuous.Precedences.Add(new() { Id = "next", LeadingEvent = "b", FollowingEvent = "c", Interval = 0 });
        var continuousTrain = Build(continuous, 1, 0, 600).Single();
        Near(continuousTrain.EventTimes["a"], 0, "unfixed serial first start is earliest");
        Near(continuousTrain.EventTimes["b"], 60, "unfixed serial first end is earliest");
        Near(continuousTrain.EventTimes["c"], 60, "zero-interval successor starts immediately after predecessor");
        Near(continuousTrain.EventTimes["d"], 120, "unfixed serial completion has no artificial uniform-slot delay");
        Verify(continuous, continuousTrain, 0, 600);

        var separated = Process(Activity("first", "a", "b", 1, 3, "r12"),
            Activity("second", "c", "d", 1, 3, "r34"));
        separated.Precedences.Add(new() { Id = "gap", LeadingEvent = "b", FollowingEvent = "c", Interval = .5 });
        var separatedTrain = Build(separated, 1, 0, 600).Single();
        Near(separatedTrain.EventTimes["c"], 90, "unfixed positive-interval successor takes the earliest legal start");
        Near(separatedTrain.EventTimes["c"] - separatedTrain.EventTimes["b"], 30, "nonzero precedence adds exactly its required gap");
        Near(separatedTrain.EventTimes["d"], 150, "positive-interval successor finishes at its minimum duration");
        Verify(separated, separatedTrain, 0, 600);

        var freeParallel = Process(Activity("long", "a", "b", 2, 4, "r12"),
            Activity("short", "c", "d", 1, 3, "r13"));
        freeParallel.Events.Add(new() { Id = "standalone", Name = "独立事件" });
        var freeParallelTrain = Build(freeParallel, 1, 3600, 4200).Single();
        Near(freeParallelTrain.EventTimes["a"], 3600, "unfixed parallel first activity starts at the origin");
        Near(freeParallelTrain.EventTimes["c"], 3600, "unfixed parallel second activity starts at the same earliest time");
        Near(freeParallelTrain.EventTimes["b"], 3720, "parallel long activity ends at its minimum duration");
        Near(freeParallelTrain.EventTimes["d"], 3660, "parallel short activity ends independently at its minimum duration");
        Near(freeParallelTrain.EventTimes["standalone"], 3600, "an isolated event is earliest, not placed at the end of the window");
        Verify(freeParallel, freeParallelTrain, 3600, 4200);

        // Upper duration bounds can force an early event later than the origin. The
        // earliest solution must propagate those bounds back through the entire chain.
        var boundedEnd = Process(Activity("lead", "a", "m", 1, 2, "r12"),
            Activity("tail", "m", "e", 2, 4, "r23"));
        Time(boundedEnd, "e", 8);
        var boundedTrain = Build(boundedEnd, 1, 0, 720).Single();
        Near(boundedTrain.OriginSeconds, 0, "late fixed endpoint does not move an already feasible train origin");
        Near(boundedTrain.EventTimes["a"], 120, "earliest first event honors both downstream maximum durations");
        Near(boundedTrain.EventTimes["m"], 240, "earliest shared event is back-propagated from the fixed endpoint");
        Near(boundedTrain.EventTimes["e"], 480, "fixed downstream event is retained exactly");
        Verify(boundedEnd, boundedTrain, 0, 720);

        var reordered = Process(Activity("independent", "a", "b", 1, 4, "loop"),
            Activity("branch", "c", "d", 2, 4, "loop"), Activity("successor", "e", "f", 1, 4, "loop"));
        reordered.Precedences.Add(new() { Id = "branch-next", LeadingEvent = "d", FollowingEvent = "e", Interval = .25 });
        var originalOrder = JsonSerializer.Serialize(reordered);
        var orderTrain = Build(reordered, 1, 500, 1500).Single();
        var reversed = JsonSerializer.Deserialize<OperationProcessTemplate>(originalOrder)!;
        reversed.Activities.Reverse();
        reversed.Events.Reverse();
        var reversedTrain = Build(reversed, 1, 500, 1500).Single();
        var earliestByID = new Dictionary<string, double> { ["a"] = 500, ["b"] = 560, ["c"] = 500, ["d"] = 620, ["e"] = 635, ["f"] = 695 };
        foreach (var (id, expectedTime) in earliestByID)
        {
            Near(orderTrain.EventTimes[id], expectedTime, $"earliest graph event {id}");
            Near(reversedTrain.EventTimes[id], expectedTime, $"activity and event array order do not change earliest event {id}");
        }
        Check(JsonSerializer.Serialize(reordered) == originalOrder, "earliest scheduling keeps the source graph unmodified");
        Verify(reordered, orderTrain, 500, 1500);
        Verify(reversed, reversedTrain, 500, 1500);

        var fractionalChain = Process(Activity("tiny-first", "a", "b", .001, .002, "r12"),
            Activity("tiny-next", "c", "d", .0005, .001, "r34"));
        fractionalChain.Precedences.Add(new() { Id = "fractional-gap", LeadingEvent = "b", FollowingEvent = "c", Interval = .0005 });
        var fractionalChainTrain = Build(fractionalChain, 1, 86399.97, 86760).Single();
        Near(fractionalChainTrain.EventTimes["a"], 86399.97, "cross-day fractional sequence starts at the earliest origin");
        Near(fractionalChainTrain.EventTimes["b"], 86400.03, "earliest fractional first completion crosses midnight");
        Near(fractionalChainTrain.EventTimes["c"], 86400.06, "fractional precedence gap introduces no minute-slot padding");
        Near(fractionalChainTrain.EventTimes["d"], 86400.09, "fractional successor retains its earliest subsecond end");
        Check(OperationProcessPlanScheduler.FormatTime(fractionalChainTrain.EventTimes["d"]) == "D+1 00:00:00.09", "earliest cross-day end formats without losing fractional seconds");
        Verify(fractionalChain, fractionalChainTrain, 86399.97, 86760);

        var spacedTrains = Build(continuous, 3, 0, 900);
        // Original formula: round((train index * 2 activities) * 14 minutes / 5 slots).
        var originalOrigins = new[] { 0d, 360d, 660d };
        for (var index = 0; index < spacedTrains.Count; index++)
        {
            var train = spacedTrains[index];
            Near(train.OriginSeconds, originalOrigins[index], $"multi-activity train {index} retains its legacy uniform origin");
            Near(train.EventTimes["a"], originalOrigins[index], $"train {index} first event is earliest relative to its own origin");
            Near(train.EventTimes["b"], originalOrigins[index] + 60, $"train {index} first completion");
            Near(train.EventTimes["c"], originalOrigins[index] + 60, $"train {index} internal activities remain continuous");
            Near(train.EventTimes["d"], originalOrigins[index] + 120, $"train {index} earliest completion preserves average inter-train spacing");
            Verify(continuous, train, 0, 900);
        }

        // Parallel activities retain independent fixed offsets from their own train origin.
        var parallel = Process(Activity("long", "a", "b", 2, 2, "r12"), Activity("short", "c", "d", 1, 1, "r13"));
        Time(parallel, "a", 0); Time(parallel, "b", 2); Time(parallel, "c", .5); Time(parallel, "d", 1.5);
        var parallelTrain = Build(parallel, 1, 3600, 4200).Single();
        Near(parallelTrain.OriginSeconds, 3600, "parallel origin");
        Near(parallelTrain.EventTimes["c"], 3630, "parallel fixed start");
        Near(parallelTrain.EventTimes["d"], 3690, "parallel fixed end");
        Check(parallelTrain.EventTimes["c"] < parallelTrain.EventTimes["b"], "parallel overlap is retained");
        Verify(parallel, parallelTrain, 3600, 4200);

        // A fixed late end forces its preceding shared event, which propagates backwards
        // through a MAXIMUM duration and both activities constrain that same event.
        var backwards = Process(Activity("first", "a", "b", 1, 2, "r12"), Activity("second", "b", "c", 3, 3, "r23"));
        Time(backwards, "c", 6);
        var backwardsTrain = Build(backwards, 1, 0, 600).Single();
        Near(backwardsTrain.EventTimes["a"], 60, "maximum duration moves earlier start forward");
        Near(backwardsTrain.EventTimes["b"], 180, "shared event bounds remain consistent");
        Near(backwardsTrain.EventTimes["c"], 360, "fixed end preserved after reverse propagation");
        Verify(backwards, backwardsTrain, 0, 600);

        // Every duration and event-DAG edge is locally legal, but the two paths from s
        // to t impose incompatible overall bounds. This requires STN negative-cycle detection.
        var impossible = Process(Activity("short-path", "s", "t", 1, 2, "r12"),
            Activity("long-path", "s", "m", 3, 3, "r13"), Activity("join", "m", "t", 0, 0, "r32"));
        Check(OperationProcessValidator.Validate(impossible, catalog).Count == 0, "globally impossible fixture is locally valid");
        Reject(() => Build(impossible, 1, 0, 600), "global incompatible path durations rejected");

        var fixedGap = Process(Activity("left", "a", "b", 1, 1, "r12"), Activity("right", "c", "d", 1, 1, "r34"));
        Time(fixedGap, "a", 0); Time(fixedGap, "b", 1); Time(fixedGap, "c", 1.5); Time(fixedGap, "d", 2.5);
        fixedGap.Precedences.Add(new() { Id = "gap", LeadingEvent = "b", FollowingEvent = "c", Interval = .5 });
        var gapTrain = Build(fixedGap, 1, 0, 600).Single();
        Near(gapTrain.EventTimes["c"] - gapTrain.EventTimes["b"], 30, "fixed positive precedence interval");
        Verify(fixedGap, gapTrain, 0, 600);
        fixedGap.Precedences[0].Interval = 1;
        Reject(() => Build(fixedGap, 1, 0, 600), "fixed event offsets cannot be shifted to hide interval conflict");

        // Keep the existing minute-slot formula, including Math.Round midpoint-to-even.
        var uniform = Process(Activity("only", "s", "e", 1, 1, "r12", "r13"));
        var uniformTrains = Build(uniform, 3, 0, 600);
        var expected = new[] { 0d, 240d, 540d };
        for (var index = 0; index < uniformTrains.Count; index++)
        {
            Near(uniformTrains[index].EventTimes["s"], expected[index], $"uniform start {index}");
            Near(uniformTrains[index].EventTimes["e"], expected[index] + 60, $"uniform end {index}");
            Check(uniformTrains[index].SelectedRouteIDs["only"] == (index == 1 ? "r13" : "r12"), $"uniform candidate rotation {index}");
            Verify(uniform, uniformTrains[index], 0, 600);
        }

        var decimals = Process(Activity("fraction", "s", "e", 1.1, 1.1, "r12"));
        var decimalTrain = Build(decimals, 1, 0, 120).Single();
        Near(decimalTrain.EventTimes["e"] - decimalTrain.EventTimes["s"], 66, "1.1 minutes remains 66 seconds");
        var subsecond = Process(Activity("fraction", "s", "e", .0005, .0005, "r12"));
        Time(subsecond, "s", .0005); Time(subsecond, "e", .001);
        var subsecondTrain = Build(subsecond, 1, 0, 1).Single();
        Near(subsecondTrain.EventTimes["s"], .03, "fractional fixed start");
        Near(subsecondTrain.EventTimes["e"], .06, "fractional fixed end");
        Check(OperationProcessPlanScheduler.FormatTime(.03) == "00:00:00.03", "fractional clock formatting");
        Verify(subsecond, subsecondTrain, 0, 1);
        var overnight = Build(decimals, 1, 86370, 86760).Single();
        Check(OperationProcessPlanScheduler.FormatTime(overnight.EventTimes["s"]) == "23:59:30", "cross-day start formatting");
        Check(OperationProcessPlanScheduler.FormatTime(overnight.EventTimes["e"]) == "D+1 00:00:36", "cross-day end formatting");
        Verify(decimals, overnight, 86370, 86760);
        Reject(() => Build(decimals, 1, 0, 60), "short window rejected instead of overflowing it");

        // A tempting first route fits the next activity locally but makes the final
        // shared node impossible. Search must backtrack two levels and restore domains.
        var joint = Process(Activity("first", "a", "x", 1, 1, "r12", "r13"),
            Activity("middle", "x", "y", 1, 1, "r23", "r34"), Activity("last", "y", "z", 1, 1, "r45"));
        var before = JsonSerializer.Serialize(joint);
        var jointTrain = Build(joint, 1, 0, 600).Single();
        Check(jointTrain.SelectedRouteIDs["first"] == "r13", "joint search revisits first choice");
        Check(jointTrain.SelectedRouteIDs["middle"] == "r34", "joint search restores then narrows middle domain");
        Check(jointTrain.SelectedRouteIDs["last"] == "r45", "joint search retains final required route");
        Check(JsonSerializer.Serialize(joint) == before, "scheduling does not mutate source constraints");
        Verify(joint, jointTrain, 0, 600);

        var differentNodes = Process(Activity("first", "s", "x", 1, 1, "r12"), Activity("second", "x", "e", 1, 1, "r34"));
        Check(OperationProcessPlanScheduler.ResolveResources(differentNodes, catalog).Count == 2, "conflicting shared nodes each have local candidates");
        Reject(() => Build(differentNodes, 1, 0, 600), "shared event cannot occupy different nodes");

        var anchors = Process(Activity("first", "s", "x", 1, 1, "r12"), Activity("second", "x", "e", 1, 1, "r23", "r24"));
        anchors.Anchors = [new() { Id = "left-anchor", Name = "左锚", TrackID = "t12" }, new() { Id = "right-anchor", Name = "右锚", TrackID = "t23" }];
        anchors.RouteAnchors = [new() { RouteID = "r12", EndAnchor = "left-anchor" },
            new() { RouteID = "r23", StartAnchor = "right-anchor" }, new() { RouteID = "r24", StartAnchor = "left-anchor" }];
        var anchorTrain = Build(anchors, 1, 0, 600).Single();
        Check(anchorTrain.SelectedRouteIDs["second"] == "r24", "shared node also requires a common anchor");
        anchors.Activities[1].RouteList = ["r23"];
        Check(OperationProcessPlanScheduler.ResolveResources(anchors, catalog).Count == 2, "conflicting anchors each have local candidates");
        Reject(() => Build(anchors, 1, 0, 600), "same node with incompatible explicit anchors rejected");

        var dwelling = Process(Activity("arrive", "s", "x", 1, 1, "r12"),
            new() { Id = "stay", Name = "停留", Type = "Dwelling", StartEvent = "x", EndEvent = "y", MinDuration = 1, MaxDuration = 1, TrackList = ["t34", "t23"] },
            Activity("leave", "y", "e", 1, 1, "r34"));
        var dwellingTrain = Build(dwelling, 1, 0, 600).Single();
        Check(dwellingTrain.SelectedTrackIDs["stay"] == "t23", "dwelling track matches both shared endpoint locations");
        Verify(dwelling, dwellingTrain, 0, 600);

        // Guaranteed-feasible diamond networks exercise both directions of bound
        // propagation. The fixed endpoints give an independent earliest-time oracle.
        var random = new Random(20260918);
        for (var sample = 0; sample < 32; sample++)
        {
            var known = new[] { 0, random.Next(30, 90), random.Next(30, 90), 180 };
            var pairs = new[] { (0, 1), (0, 2), (1, 3), (2, 3) };
            var activities = pairs.Select((pair, index) => {
                var duration = known[pair.Item2] - known[pair.Item1];
                var minimum = Math.Max(0, duration - random.Next(0, 31));
                var maximum = duration + random.Next(0, 31);
                return Activity($"a{index}", $"e{pair.Item1}", $"e{pair.Item2}", minimum / 60d, maximum / 60d, "loop");
            }).ToArray();
            var diamond = Process(activities);
            Time(diamond, "e0", 0); Time(diamond, "e3", 3);
            var train = Build(diamond, 1, 0, 300).Single();
            Near(train.EventTimes["e3"] - train.EventTimes["e0"], 180, $"feasible diamond endpoints {sample}");
            Near(train.EventTimes["e1"], Math.Max(activities[0].MinDuration * 60, 180 - activities[2].MaxDuration * 60),
                $"diamond first branch has the earliest globally feasible event {sample}");
            Near(train.EventTimes["e2"], Math.Max(activities[1].MinDuration * 60, 180 - activities[3].MaxDuration * 60),
                $"diamond second branch has the earliest globally feasible event {sample}");
            Verify(diamond, train, 0, 300);
        }
        return checks;

        List<ProcessScheduledTrain> Build(OperationProcessTemplate source, int count, double start, double end) =>
            OperationProcessPlanScheduler.Build(source, catalog, count, start, end);

        void Verify(OperationProcessTemplate source, ProcessScheduledTrain train, double start, double end)
        {
            foreach (var ev in source.Events)
            {
                Check(train.EventTimes.TryGetValue(ev.Id, out var actual) && double.IsFinite(actual) && actual >= start - 1e-6 && actual <= end + 1e-6,
                    $"event {ev.Id} within generation window");
                Check(actual >= train.OriginSeconds - 1e-6, $"event {ev.Id} does not precede train origin");
                if (ev.Time is double offset) Near(actual - train.OriginSeconds, (double)((decimal)offset * 60m), $"event {ev.Id} relative fixed time");
            }
            foreach (var activity in source.Activities)
            {
                var duration = train.EventTimes[activity.EndEvent] - train.EventTimes[activity.StartEvent];
                Check(duration >= activity.MinDuration * 60 - 1e-6 && duration <= activity.MaxDuration * 60 + 1e-6,
                    $"activity {activity.Id} retains both duration bounds");
            }
            foreach (var precedence in source.Precedences)
                Check(train.EventTimes[precedence.FollowingEvent] - train.EventTimes[precedence.LeadingEvent] >= precedence.Interval * 60 - 1e-6,
                    $"precedence {precedence.Id} interval preserved");
        }
        void Near(double actual, double expectedValue, string name) => Check(Math.Abs(actual - expectedValue) < 1e-6, $"{name}: expected {expectedValue}, actual {actual}");
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Scheduler constraint assertion failed: " + name);
            checks++;
        }
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (ArgumentException) { checks++; return; }
            throw new InvalidOperationException("Scheduler should reject: " + name);
        }
    }

    private static ProcessActivity Activity(string id, string start, string end, double min, double max, params string[] routes) =>
        new() { Id = id, Name = id, Type = "Arrival", StartEvent = start, EndEvent = end, MinDuration = min, MaxDuration = max, RouteList = routes.ToList() };

    private static OperationProcessTemplate Process(params ProcessActivity[] activities) => new() {
        Id = "scheduler-check", Name = "排布约束回归", Activities = activities.ToList(),
        Events = activities.SelectMany(activity => new[] { activity.StartEvent, activity.EndEvent }).Distinct(StringComparer.Ordinal)
            .Select(id => new ProcessEvent { Id = id, Name = id }).ToList()
    };

    private static void Time(OperationProcessTemplate source, string eventID, double minutes) => source.Events.Single(ev => ev.Id == eventID).Time = minutes;

    private static ProcessCatalog Catalog() => new() {
        Nodes = Enumerable.Range(1, 5).Select(id => new ProcessCatalogNode { Id = id.ToString(), Name = id.ToString() }).ToList(),
        Tracks = new[] { ("t12", "1", "2"), ("t23", "2", "3"), ("t24", "2", "4"), ("t34", "3", "4"), ("t45", "4", "5") }
            .Select(item => new ProcessCatalogTrack { Id = item.Item1, Name = item.Item1, FromNodeID = item.Item2, ToNodeID = item.Item3 }).ToList(),
        Routes = new[] { ("r12", "1", "2"), ("r13", "1", "3"), ("r23", "2", "3"), ("r24", "2", "4"), ("r32", "3", "2"),
                ("r34", "3", "4"), ("r45", "4", "5"), ("loop", "1", "1") }
            .Select(item => new ProcessCatalogRoute { Id = item.Item1, Name = item.Item1, Type = "Arrival", StartNodeID = item.Item2, EndNodeID = item.Item3 }).ToList()
    };
}
