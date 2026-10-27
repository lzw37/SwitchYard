using System.Text.Json;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

internal static class RouteContinuitySelfTests
{
    public static async Task RunAsync()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var input = new StationCapacitySolveInput
        {
            Settings = new() { LeftShiftToleranceSeconds = 0, RightShiftToleranceSeconds = 0, TimeLimitSeconds = 10, ThreadCount = 1 },
            Routes = new[] { "stop5", "departure3", "departure5" }.Select(id => new StationCapacityRouteInput { Id = id, CellIds = [id] }).ToList(),
            RouteOccupations = new[] { "stop5", "departure3", "departure5" }.Select(id => new StationCapacityRouteOccupationInput {
                RouteId = id, CellId = id, EndOccupationShiftSeconds = id == "stop5" ? 60 : 0
            }).ToList(),
            Trains = [new() {
                Id = "train1",
                ProcessConstraints = new() {
                    Events = [
                        new() { Id = "stop-start", OriginalTimeSeconds = 0, LocationIds = ["5L", "5R"] },
                        new() { Id = "stop-end", OriginalTimeSeconds = 60, LocationIds = ["5L", "5R"] },
                        new() { Id = "departure-start", OriginalTimeSeconds = 120, LocationIds = ["3R", "5R"] },
                        new() { Id = "departure-end", OriginalTimeSeconds = 180, LocationIds = ["out"] }
                    ],
                    Precedences = [new() { LeadingEventId = "stop-end", FollowingEventId = "departure-start", IntervalSeconds = 60 }]
                },
                Movements = [
                    new() { Id = "stop", CandidateRouteIds = ["stop5"], OriginalStartSeconds = 0, OriginalEndSeconds = 60, MinDurationSeconds = 60, MaxDurationSeconds = 60,
                        StartEventId = "stop-start", EndEventId = "stop-end", StartLocationIdsByRoute = new() { ["stop5"] = ["5L", "5R"] }, EndLocationIdsByRoute = new() { ["stop5"] = ["5L", "5R"] } },
                    new() { Id = "departure", CandidateRouteIds = ["departure3", "departure5"], OriginalStartSeconds = 120, OriginalEndSeconds = 180, MinDurationSeconds = 60, MaxDurationSeconds = 60,
                        StartEventId = "departure-start", EndEventId = "departure-end", StartLocationIdsByRoute = new() { ["departure3"] = ["3R"], ["departure5"] = ["5R"] },
                        EndLocationIdsByRoute = new() { ["departure3"] = ["out"], ["departure5"] = ["out"] } }
                ]
            }]
        };
        // End-to-start precedence constrains time even when the locations differ.
        input.Trains[0].Movements[1].CandidateRouteIds = ["departure3"];
        var result = await Solve();
        if (!result.HasSolution) throw new InvalidOperationException("次序不应要求两端事件地点相同。");
        var stop = result.Trains.Single().Movements.Single(m => m.Id == "stop");
        if (stop.CellOccupations.Single().StartSeconds != stop.StartSeconds || stop.CellOccupations.Single().EndSeconds != stop.EndSeconds + 60)
            throw new InvalidOperationException("停留占用自检失败：结束偏移 +60 秒必须将占用结束延后一分钟。");

        var process = input.Trains[0].ProcessConstraints!;
        process.Anchors = [new() { Id = "platform", RouteIds = ["stop5", "stop3"], MovementId = "stop" }];
        process.Events[0].AnchorChoicesByLocation = new() {
            ["5L"] = new() { AnchorId = "platform", RouteId = "stop5" }, ["5R"] = new() { AnchorId = "platform", RouteId = "stop5" }
        };
        process.Events[1].AnchorChoicesByLocation = new(process.Events[0].AnchorChoicesByLocation);
        process.Events[2].AnchorChoicesByLocation = new() {
            ["3R"] = new() { AnchorId = "platform", RouteId = "stop3" }, ["5R"] = new() { AnchorId = "platform", RouteId = "stop5" }
        };
        input.Trains[0].Movements[1].CandidateRouteIds = ["departure3", "departure5"];
        result = await Solve();
        if (!result.HasSolution || result.Trains.Single().Movements.Single(m => m.Id == "departure").RouteId != "departure5")
            throw new InvalidOperationException("连续进路自检失败：5 道停留必须选择 5 道发车。");
        input.Trains[0].Movements[1].CandidateRouteIds = ["departure3"];
        if ((await Solve()).HasSolution) throw new InvalidOperationException("连续进路自检失败：不可衔接的固定进路必须无解。");
        process.Precedences.Clear();
        if ((await Solve()).HasSolution) throw new InvalidOperationException("没有次序时，共同锚仍须落在同一条停留进路。");
        input.Trains[0].Movements[1].CandidateRouteIds = ["departure5"];
        input.Trains[0].Movements[0].StartLocationIdsByRoute["stop5"] = ["5L"];
        if (!(await Solve()).HasSolution) throw new InvalidOperationException("同一锚应允许停留进路两端节点。");
        process.Events[2].AnchorChoicesByLocation.Clear();
        input.Trains[0].Movements[1].CandidateRouteIds = ["departure3"];
        process.Precedences = [new() { LeadingEventId = "stop-end", FollowingEventId = "departure-start", IntervalSeconds = 61 }];
        if ((await Solve()).HasSolution) throw new InvalidOperationException("次序最小间隔必须保留。");
        process.Precedences[0].IntervalSeconds = 60;
        if (!(await Solve()).HasSolution) throw new InvalidOperationException("仅时间约束应允许不同地点。");
        process.Precedences[0].IsExact = true;
        if (!(await Solve()).HasSolution) throw new InvalidOperationException("严格等于实际间隔应可行。");
        process.Precedences[0].IntervalSeconds = 59.5;
        if ((await Solve()).HasSolution) throw new InvalidOperationException("严格次序不允许比规定间隔长半秒。");
        process.Precedences[0].IsExact = false;
        if (!(await Solve()).HasSolution) throw new InvalidOperationException("最小间隔应允许更长的实际间隔。");
        process.Precedences[0].IsExact = true;
        input.Settings.RightShiftToleranceSeconds = 60;
        result = await Solve();
        var times = result.Trains.Single().EventTimes;
        if (!result.HasSolution || Math.Abs(times["departure-start"] - times["stop-end"] - 59.5) > 0.000001)
            throw new InvalidOperationException("可调整时刻时，求解器必须满足小数秒严格间隔。");
        input.Settings.RightShiftToleranceSeconds = 0;
        process.Events[2].OriginalTimeSeconds = input.Trains[0].Movements[1].OriginalStartSeconds = 60;
        process.Events[3].OriginalTimeSeconds = input.Trains[0].Movements[1].OriginalEndSeconds = 120;
        process.Precedences[0].IntervalSeconds = 0;
        if (!(await Solve()).HasSolution) throw new InvalidOperationException("严格零间隔应允许同时发生的前后事件。");
        Console.WriteLine("PASS capacity solver: exact equality, minimum inequality, fractional seconds, zero interval and time adjustment");
        Console.WriteLine("PASS capacity solver: timing-only precedence, anchor consistency without precedence, opposite endpoints and minimum intervals");

        async Task<StationCapacitySolveResult> Solve() => (await new StationCapacityModel(runInProcess: true).SolveAsync(
            JsonSerializer.SerializeToElement(input, options), new("continuity-self-test", new() { CpuCoreCount = 1 }),
            (_, _) => Task.CompletedTask, CancellationToken.None)).Deserialize<StationCapacitySolveResult>(options)!;
    }
}
