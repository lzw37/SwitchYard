using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Google.OrTools.LinearSolver;
using Serilog;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

internal sealed class StationCapacityModel : ICapacityModel
{
    private const double DaySeconds = 86_400;
    private const double MaximumHorizonSeconds = 7 * DaySeconds;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ILogger Logger = Log.ForContext<StationCapacityModel>();
    private readonly bool _runInProcess;

    public StationCapacityModel(bool runInProcess = false)
    {
        _runInProcess = runInProcess;
    }

    public CapacityModelDescriptor Descriptor { get; } = new()
    {
        Id = CapacityAgentProtocol.StationCapacityModelId,
        Name = "铁路车站通过能力模型",
        Version = "2.0.0",
        Description = "支持原列车作业与完整作业过程事件图、资源地点、持续时间和轨道电路占用约束。"
    };

    public async Task<JsonElement> SolveAsync(
        JsonElement input,
        CapacityModelExecutionContext executionContext,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        if (!_runInProcess)
        {
            return await CapacitySolveWorkerProcess.SolveAsync(
                input,
                executionContext,
                reportProgress,
                cancellationToken);
        }

        var modelInput = input.Deserialize<StationCapacitySolveInput>(JsonOptions)
            ?? throw new InvalidOperationException("求解输入为空或格式不正确。");

        Validate(modelInput);
        Logger.Information(
            "能力计算输入校验完成：{TrainCount} 列列车、{RouteCount} 条进路、{OccupationCount} 项占用参数",
            modelInput.Trains.Count,
            modelInput.Routes.Count,
            modelInput.RouteOccupations.Count);
        await reportProgress(10, "输入数据校验完成");

        return await Task.Run(async () =>
        {
            var result = Solve(modelInput, executionContext.Resources, reportProgress, cancellationToken);
            await reportProgress(100, "求解结果已生成");
            return JsonSerializer.SerializeToElement(result, JsonOptions);
        }, cancellationToken);
    }

    private static StationCapacitySolveResult Solve(
        StationCapacitySolveInput input,
        CapacityTaskResourceLimits resources,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var solver = CreateSolver(out var solverBackend);

        var settings = input.Settings ?? new StationCapacitySolveSettings();
        var horizon = EffectiveHorizon(input);
        var occupations = input.RouteOccupations.Concat(input.Trains.SelectMany(train => train.Movements)
            .SelectMany(movement => movement.CellOccupationOverrides)).ToList();
        var minimumShift = occupations.SelectMany(window => new[]
            { window.StartOccupationShiftSeconds, window.EndOccupationShiftSeconds }).DefaultIfEmpty(0).Min();
        var maximumShift = occupations.SelectMany(window => new[]
            { window.StartOccupationShiftSeconds, window.EndOccupationShiftSeconds }).DefaultIfEmpty(0).Max();
        var occupationLower = Math.Min(-horizon, minimumShift);
        var occupationUpper = Math.Max(horizon * 2, horizon + maximumShift);
        var bigM = Math.Max(300_000, 2 * (occupationUpper - occupationLower) + horizon);
        var timeLimitSeconds = Math.Clamp(settings.TimeLimitSeconds, 1, 86_400);
        var cpuCoreLimit = Math.Clamp(resources.CpuCoreCount, 1, 128);
        var threadCount = Math.Min(Math.Clamp(settings.ThreadCount, 1, 128), cpuCoreLimit);
        solver.SetTimeLimit(timeLimitSeconds * 1000L);
        solver.SetNumThreads(threadCount);
        if (string.Equals(solverBackend, "SCIP", StringComparison.Ordinal) && resources.MemoryLimitBytes > 0)
        {
            var memoryLimitMb = Math.Max(1, resources.MemoryLimitBytes / (1024L * 1024L));
            if (!solver.SetSolverSpecificParametersAsString($"limits/memory = {memoryLimitMb}"))
            {
                Logger.Warning("SCIP 未接受每任务内存上限参数：{MemoryLimitMb} MB", memoryLimitMb);
            }
        }

        var routeMap = input.Routes.ToDictionary(route => route.Id, StringComparer.OrdinalIgnoreCase);
        var movements = BuildMovementContexts(input, routeMap);
        var startVariables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        var endVariables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        var routeVariables = new Dictionary<(string MovementKey, string RouteId), Variable>();
        var occupationStarts = new Dictionary<(string MovementKey, string CellId), Variable>();
        var occupationEnds = new Dictionary<(string MovementKey, string CellId), Variable>();

        foreach (var movement in movements)
        {
            var movementHorizon = movement.Train.ProcessConstraints is null ? Math.Min(DaySeconds, horizon) : horizon;
            startVariables[movement.Key] = solver.MakeNumVar(0, movementHorizon, $"a_{movement.VariableId}");
            endVariables[movement.Key] = solver.MakeNumVar(0, movementHorizon, $"d_{movement.VariableId}");

            foreach (var route in movement.CandidateRoutes)
            {
                routeVariables[(movement.Key, route.Id)] =
                    solver.MakeBoolVar($"x_{movement.VariableId}_{Sanitize(route.Id)}");
            }

            foreach (var cellId in movement.CandidateCellIds)
            {
                occupationStarts[(movement.Key, cellId)] =
                    solver.MakeNumVar(occupationLower, occupationUpper, $"s_{movement.VariableId}_{Sanitize(cellId)}");
                occupationEnds[(movement.Key, cellId)] =
                    solver.MakeNumVar(occupationLower, occupationUpper, $"e_{movement.VariableId}_{Sanitize(cellId)}");
            }
        }

        reportProgress(30, "决策变量创建完成").GetAwaiter().GetResult();

        BuildMovementConstraints(input, solver, movements, startVariables, endVariables, routeVariables, horizon);
        var eventVariables = BuildProcessConstraints(input, solver, movements, startVariables, endVariables, routeVariables, horizon);
        BuildTrainConservationConstraints(solver, movements, startVariables, endVariables);
        BuildRouteConnectionConstraints(solver, movements, routeVariables);
        BuildOccupationConstraints(
            input,
            solver,
            movements,
            startVariables,
            endVariables,
            routeVariables,
            occupationStarts,
            occupationEnds,
            bigM);
        BuildConflictConstraints(
            solver,
            movements,
            routeVariables,
            occupationStarts,
            occupationEnds,
            bigM);
        BuildObjective(settings.Objective, solver, movements, startVariables, endVariables);

        reportProgress(55, "模型创建完成，开始调用 OR-Tools 求解").GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        using var cancellationRegistration = cancellationToken.Register(() => solver.InterruptSolve());
        Logger.Information(
            "OR-Tools 求解开始：后端 {SolverBackend}，版本 {SolverVersion}，变量 {VariableCount}，约束 {ConstraintCount}，时限 {TimeLimitSeconds} 秒，线程 {ThreadCount}，内存上限 {MemoryLimitMb} MB",
            solverBackend,
            solver.SolverVersion(),
            solver.NumVariables(),
            solver.NumConstraints(),
            timeLimitSeconds,
            threadCount,
            resources.MemoryLimitBytes / (1024L * 1024L));
        solver.EnableOutput();
        var stopwatch = Stopwatch.StartNew();
        var status = OrToolsLogCapture.Run(solver.Solve);
        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();
        Logger.Information(
            "OR-Tools 求解结束：状态 {SolverStatus}，耗时 {ElapsedSeconds:F3} 秒，迭代 {IterationCount}，分支 {NodeCount}",
            MapStatus(status),
            stopwatch.Elapsed.TotalSeconds,
            solver.Iterations(),
            solver.Nodes());

        var hasSolution = status is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE;
        var result = new StationCapacitySolveResult
        {
            Solver = solver.SolverVersion(),
            Status = MapStatus(status),
            HasSolution = hasSolution,
            SolveTimeSeconds = stopwatch.Elapsed.TotalSeconds,
            TrainCount = input.Trains.Count,
            CompletedAt = DateTimeOffset.UtcNow
        };

        if (!hasSolution)
        {
            Logger.Warning("OR-Tools 未找到可用解：{SolverStatus}", result.Status);
            return result;
        }

        result.ObjectiveValue = FiniteOrZero(solver.Objective().Value());
        result.BestBound = FiniteOrZero(solver.Objective().BestBound());
        result.MipGap = Math.Abs(result.ObjectiveValue) < 1e-9
            ? 0
            : Math.Max(0, (result.ObjectiveValue - result.BestBound) / Math.Abs(result.ObjectiveValue));

        var lastOccupationEnd = 0d;
        foreach (var trainGroup in movements.GroupBy(item => item.TrainIndex).OrderBy(group => group.Key))
        {
            var firstContext = trainGroup.First();
            var trainResult = new StationCapacityTrainResult
            {
                Id = firstContext.Train.Id,
                TrainType = firstContext.Train.TrainType,
                EventTimes = firstContext.Train.ProcessConstraints?.Events.ToDictionary(ev => ev.Id,
                    ev => CleanSecond(eventVariables[(firstContext.TrainIndex, ev.Id)].SolutionValue(), 0, horizon),
                    StringComparer.Ordinal) ?? new(StringComparer.Ordinal)
            };

            foreach (var movement in trainGroup.OrderBy(item => item.Movement.Sequence))
            {
                var selectedRoute = movement.CandidateRoutes
                    .OrderBy(route => route.Id, StringComparer.OrdinalIgnoreCase)
                    .First(route => routeVariables[(movement.Key, route.Id)].SolutionValue() > 0.5);
                var start = CleanSecond(startVariables[movement.Key].SolutionValue(), 0, horizon);
                var end = CleanSecond(endVariables[movement.Key].SolutionValue(), 0, horizon);
                var movementResult = new StationCapacityMovementResult
                {
                    Id = movement.Movement.Id,
                    Name = movement.Movement.Name,
                    RouteId = selectedRoute.TrackId is null ? selectedRoute.Id : string.Empty,
                    TrackId = selectedRoute.TrackId,
                    StartSeconds = start,
                    EndSeconds = end,
                    StartTime = FormatTime(start),
                    EndTime = FormatTime(end)
                };

                foreach (var cellId in selectedRoute.CellIds)
                {
                    var occupationStart = CleanSecond(
                        occupationStarts[(movement.Key, cellId)].SolutionValue(), occupationLower, occupationUpper);
                    var occupationEnd = CleanSecond(
                        occupationEnds[(movement.Key, cellId)].SolutionValue(), occupationLower, occupationUpper);
                    lastOccupationEnd = Math.Max(lastOccupationEnd, occupationEnd);
                    movementResult.CellOccupations.Add(new StationCapacityCellOccupationResult
                    {
                        CellId = cellId,
                        StartSeconds = occupationStart,
                        EndSeconds = occupationEnd,
                        StartTime = FormatTime(occupationStart),
                        EndTime = FormatTime(occupationEnd)
                    });
                }

                trainResult.Movements.Add(movementResult);
            }

            result.Trains.Add(trainResult);
        }

        result.TotalOccupationTimeSeconds = lastOccupationEnd;
        result.CapacityValue = lastOccupationEnd <= 0
            ? 0
            : input.Trains.Count * 64_800d / lastOccupationEnd;
        Logger.Information(
            "能力计算结果解析完成：目标值 {ObjectiveValue}，最优界 {BestBound}，MIP Gap {MipGap:P4}，能力值 {CapacityValue}",
            result.ObjectiveValue,
            result.BestBound,
            result.MipGap,
            result.CapacityValue);
        reportProgress(90, "求解完成，正在解析结果").GetAwaiter().GetResult();
        return result;
    }

    private static Solver CreateSolver(out string backend)
    {
        var solver = Solver.CreateSolver("SCIP");
        if (solver is not null)
        {
            backend = "SCIP";
            return solver;
        }

        solver = Solver.CreateSolver("CBC_MIXED_INTEGER_PROGRAMMING");
        if (solver is not null)
        {
            backend = "CBC";
            return solver;
        }

        throw new InvalidOperationException("当前 OR-Tools 运行环境不包含可用的混合整数规划求解器。");
    }

    private static List<MovementContext> BuildMovementContexts(
        StationCapacitySolveInput input,
        IReadOnlyDictionary<string, StationCapacityRouteInput> routeMap)
    {
        var result = new List<MovementContext>();
        for (var trainIndex = 0; trainIndex < input.Trains.Count; trainIndex++)
        {
            var train = input.Trains[trainIndex];
            var orderedMovements = train.Movements.OrderBy(item => item.Sequence).ToList();
            for (var movementIndex = 0; movementIndex < orderedMovements.Count; movementIndex++)
            {
                var movement = orderedMovements[movementIndex];
                var candidates = ResolveCandidateRoutes(movement, routeMap);
                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException($"列车 {train.Id} 的作业 {movement.Id} 没有可行进路。");
                }

                var key = $"{trainIndex}:{movementIndex}";
                result.Add(new MovementContext(
                    key,
                    $"{trainIndex}_{movementIndex}_{Sanitize(movement.Id)}",
                    trainIndex,
                    train,
                    movement,
                    candidates,
                    candidates.SelectMany(route => route.CellIds).Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
            }
        }

        return result;
    }

    private static List<StationCapacityRouteInput> ResolveCandidateRoutes(
        StationCapacityMovementInput movement,
        IReadOnlyDictionary<string, StationCapacityRouteInput> routeMap)
    {
        if (movement.CandidateRouteIds.Count > 0)
        {
            return movement.CandidateRouteIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(routeMap.ContainsKey)
                .Select(routeId => routeMap[routeId])
                .ToList();
        }

        return routeMap.Values
            .Where(route => route.TrackId is null && movement.RequiredRouteTags.All(requiredTag =>
                route.Tags.Contains(requiredTag, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    private static void BuildMovementConstraints(
        StationCapacitySolveInput input,
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables,
        double horizon)
    {
        var settings = input.Settings ?? new StationCapacitySolveSettings();
        foreach (var context in movements)
        {
            var movement = context.Movement;
            var start = starts[context.Key];
            var end = ends[context.Key];
            var movementHorizon = context.Train.ProcessConstraints is null ? Math.Min(DaySeconds, horizon) : horizon;
            solver.Add(end - start >= movement.MinDurationSeconds);
            solver.Add(end - start <= movement.MaxDurationSeconds);
            solver.Add(start >= Math.Max(0, movement.OriginalStartSeconds - settings.LeftShiftToleranceSeconds));
            solver.Add(start <= Math.Min(movementHorizon, movement.OriginalStartSeconds + settings.RightShiftToleranceSeconds));
            solver.Add(end >= Math.Max(0, movement.OriginalEndSeconds - settings.LeftShiftToleranceSeconds));
            solver.Add(end <= Math.Min(movementHorizon, movement.OriginalEndSeconds + settings.RightShiftToleranceSeconds));

            LinearExpr routeSum = routeVariables[(context.Key, context.CandidateRoutes[0].Id)];
            foreach (var route in context.CandidateRoutes.Skip(1))
            {
                routeSum += routeVariables[(context.Key, route.Id)];
            }

            solver.Add(routeSum == 1);
        }
    }

    private static Dictionary<(int TrainIndex, string EventId), Variable> BuildProcessConstraints(
        StationCapacitySolveInput input, Solver solver, IReadOnlyList<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts, IReadOnlyDictionary<string, Variable> ends,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables, double horizon)
    {
        var result = new Dictionary<(int TrainIndex, string EventId), Variable>();
        var settings = input.Settings ?? new StationCapacitySolveSettings();
        foreach (var group in movements.GroupBy(item => item.TrainIndex))
        {
            var process = group.First().Train.ProcessConstraints;
            if (process is null) continue;
            var events = process.Events.ToDictionary(ev => ev.Id, StringComparer.Ordinal);
            var locations = new Dictionary<(string EventId, string LocationId), Variable>();
            foreach (var ev in process.Events)
            {
                var variable = solver.MakeNumVar(process.OriginSeconds, horizon, $"event_{group.Key}_{Sanitize(ev.Id)}");
                result[(group.Key, ev.Id)] = variable;
                solver.Add(variable >= Math.Max(process.OriginSeconds, ev.OriginalTimeSeconds - settings.LeftShiftToleranceSeconds));
                solver.Add(variable <= Math.Min(horizon, ev.OriginalTimeSeconds + settings.RightShiftToleranceSeconds));
                if (ev.FixedTimeSeconds is double fixedTime) solver.Add(variable == fixedTime);
                if (ev.LocationIds.Count == 0) continue;
                for (var index = 0; index < ev.LocationIds.Count; index++)
                    locations[(ev.Id, ev.LocationIds[index])] = solver.MakeBoolVar($"location_{group.Key}_{Sanitize(ev.Id)}_{index}");
                LinearExpr sum = locations[(ev.Id, ev.LocationIds[0])];
                foreach (var location in ev.LocationIds.Skip(1)) sum += locations[(ev.Id, location)];
                solver.Add(sum == 1);
            }
            foreach (var movement in group)
            {
                solver.Add(starts[movement.Key] == result[(group.Key, movement.Movement.StartEventId!)]);
                solver.Add(ends[movement.Key] == result[(group.Key, movement.Movement.EndEventId!)]);
                BindLocations(movement, movement.Movement.StartEventId!, movement.Movement.StartLocationIdsByRoute);
                BindLocations(movement, movement.Movement.EndEventId!, movement.Movement.EndLocationIdsByRoute);
            }
            foreach (var precedence in process.Precedences)
                solver.Add(result[(group.Key, precedence.FollowingEventId)] - result[(group.Key, precedence.LeadingEventId)] >= precedence.IntervalSeconds);

            void BindLocations(MovementContext movement, string eventId, Dictionary<string, List<string>> candidates)
            {
                var ev = events[eventId];
                if (ev.LocationIds.Count == 0) return;
                foreach (var route in movement.CandidateRoutes)
                {
                    var permitted = candidates[route.Id].Intersect(ev.LocationIds, StringComparer.Ordinal).ToList();
                    var selected = routeVariables[(movement.Key, route.Id)];
                    if (permitted.Count == 0) { solver.Add(selected == 0); continue; }
                    LinearExpr sum = locations[(eventId, permitted[0])];
                    foreach (var location in permitted.Skip(1)) sum += locations[(eventId, location)];
                    solver.Add(selected <= sum);
                }
            }
        }
        return result;
    }

    private static void BuildTrainConservationConstraints(
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends)
    {
        foreach (var trainMovements in movements.GroupBy(item => item.TrainIndex))
        {
            if (trainMovements.First().Train.ProcessConstraints is not null) continue;
            var ordered = trainMovements.OrderBy(item => item.Movement.Sequence).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                solver.Add(ends[ordered[index - 1].Key] == starts[ordered[index].Key]);
            }
        }
    }

    private static void BuildRouteConnectionConstraints(
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables)
    {
        foreach (var trainMovements in movements.GroupBy(item => item.TrainIndex))
        {
            if (trainMovements.First().Train.ProcessConstraints is not null) continue;
            var ordered = trainMovements.OrderBy(item => item.Movement.Sequence).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                foreach (var previousRoute in previous.CandidateRoutes)
                {
                    foreach (var currentRoute in current.CandidateRoutes)
                    {
                        if (!RoutesConnect(previousRoute, currentRoute))
                        {
                            solver.Add(
                                routeVariables[(previous.Key, previousRoute.Id)] +
                                routeVariables[(current.Key, currentRoute.Id)] <= 1);
                        }
                    }
                }
            }
        }
    }

    private static void BuildOccupationConstraints(
        StationCapacitySolveInput input,
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables,
        IReadOnlyDictionary<(string MovementKey, string CellId), Variable> occupationStarts,
        IReadOnlyDictionary<(string MovementKey, string CellId), Variable> occupationEnds,
        double bigM)
    {
        foreach (var context in movements)
        {
            foreach (var route in context.CandidateRoutes)
            {
                var routeSelected = routeVariables[(context.Key, route.Id)];
                foreach (var cellId in route.CellIds)
                {
                    var window = context.Movement.CellOccupationOverrides.FirstOrDefault(occupation =>
                        occupation.RouteId == route.Id && occupation.CellId == cellId)
                        ?? ResolveOccupationWindow(input.RouteOccupations, route.Id, context.Train.TrainType, cellId);
                    var occupationStart = occupationStarts[(context.Key, cellId)];
                    var occupationEnd = occupationEnds[(context.Key, cellId)];
                    solver.Add(occupationStart + bigM * (1 - routeSelected) >=
                               starts[context.Key] + window.StartOccupationShiftSeconds);
                    solver.Add(occupationStart <=
                               starts[context.Key] + window.StartOccupationShiftSeconds + bigM * (1 - routeSelected));
                    solver.Add(occupationEnd + bigM * (1 - routeSelected) >=
                               ends[context.Key] + window.EndOccupationShiftSeconds);
                    solver.Add(occupationEnd <=
                               ends[context.Key] + window.EndOccupationShiftSeconds + bigM * (1 - routeSelected));
                }
            }
        }
    }

    private static void BuildConflictConstraints(
        Solver solver,
        IReadOnlyList<MovementContext> movements,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables,
        IReadOnlyDictionary<(string MovementKey, string CellId), Variable> occupationStarts,
        IReadOnlyDictionary<(string MovementKey, string CellId), Variable> occupationEnds,
        double bigM)
    {
        for (var firstIndex = 0; firstIndex < movements.Count; firstIndex++)
        {
            var first = movements[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < movements.Count; secondIndex++)
            {
                var second = movements[secondIndex];
                if (first.TrainIndex == second.TrainIndex)
                {
                    continue;
                }

                var sharedCells = first.CandidateCellIds.Intersect(second.CandidateCellIds, StringComparer.OrdinalIgnoreCase);
                foreach (var cellId in sharedCells)
                {
                    var firstCellRoutes = first.CandidateRoutes
                        .Where(route => route.CellIds.Contains(cellId, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    LinearExpr firstUsesCell = routeVariables[(first.Key, firstCellRoutes[0].Id)];
                    foreach (var route in firstCellRoutes.Skip(1))
                    {
                        firstUsesCell += routeVariables[(first.Key, route.Id)];
                    }

                    var secondCellRoutes = second.CandidateRoutes
                        .Where(route => route.CellIds.Contains(cellId, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    LinearExpr secondUsesCell = routeVariables[(second.Key, secondCellRoutes[0].Id)];
                    foreach (var route in secondCellRoutes.Skip(1))
                    {
                        secondUsesCell += routeVariables[(second.Key, route.Id)];
                    }

                    var firstBeforeSecond = solver.MakeBoolVar(
                        $"y_{Sanitize(cellId)}_{first.VariableId}_{second.VariableId}");
                    var unusedRelaxation = bigM * (2 - firstUsesCell - secondUsesCell);
                    solver.Add(occupationStarts[(second.Key, cellId)] >=
                               occupationEnds[(first.Key, cellId)] - bigM * (1 - firstBeforeSecond) - unusedRelaxation);
                    solver.Add(occupationStarts[(first.Key, cellId)] >=
                               occupationEnds[(second.Key, cellId)] - bigM * firstBeforeSecond - unusedRelaxation);
                }
            }
        }
    }

    private static void BuildObjective(
        string objectiveName,
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends)
    {
        var movementList = movements.ToList();
        LinearExpr objective = ObjectiveTerm(objectiveName, movementList[0], starts, ends);
        foreach (var movement in movementList.Skip(1))
        {
            objective += ObjectiveTerm(objectiveName, movement, starts, ends);
        }

        solver.Minimize(objective);
    }

    private static LinearExpr ObjectiveTerm(
        string objectiveName,
        MovementContext movement,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends) =>
        objectiveName.Trim().ToLowerInvariant() switch
        {
            "min-duration" => ends[movement.Key] - starts[movement.Key],
            "min-start-and-end" => ends[movement.Key] + starts[movement.Key],
            _ => ends[movement.Key]
        };

    private static StationCapacityRouteOccupationInput ResolveOccupationWindow(
        IEnumerable<StationCapacityRouteOccupationInput> windows,
        string routeId,
        string trainType,
        string cellId)
    {
        var candidates = windows.Where(item =>
            string.Equals(item.RouteId, routeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.CellId, cellId, StringComparison.OrdinalIgnoreCase)).ToList();
        var exact = candidates.FirstOrDefault(item =>
            string.Equals(item.TrainTypeId, trainType, StringComparison.OrdinalIgnoreCase));
        var fallback = candidates.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.TrainTypeId));
        return exact ?? fallback ?? throw new InvalidOperationException(
            $"进路 {routeId}、车型 {trainType}、轨道电路 {cellId} 缺少占用时间参数。");
    }

    private static void Validate(StationCapacitySolveInput input)
    {
        if (!double.IsFinite(input.HorizonSeconds) || input.HorizonSeconds <= 0 || input.HorizonSeconds > MaximumHorizonSeconds)
            throw new InvalidOperationException("求解时间范围无效，最长支持七天。");
        if (input.Settings is not null && (input.Settings.LeftShiftToleranceSeconds < 0 || input.Settings.RightShiftToleranceSeconds < 0))
            throw new InvalidOperationException("时间移位容差不能为负数。");
        if (input.Routes.Count == 0)
        {
            throw new InvalidOperationException("输入中没有进路数据。");
        }

        if (input.Trains.Count == 0)
        {
            throw new InvalidOperationException("输入中没有列车数据。");
        }

        if (input.Routes.Any(route => string.IsNullOrWhiteSpace(route.Id) || route.CellIds.Count == 0))
        {
            throw new InvalidOperationException("每条进路都必须包含 ID 和至少一个轨道电路。");
        }
        if (input.Routes.Select(route => route.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Routes.Count ||
            input.Trains.Select(train => train.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Trains.Count)
            throw new InvalidOperationException("进路或列车 ID 重复。");
        var routeIds = input.Routes.Select(route => route.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var horizon = EffectiveHorizon(input);

        foreach (var train in input.Trains)
        {
            if (string.IsNullOrWhiteSpace(train.Id) || train.Movements.Count == 0)
            {
                throw new InvalidOperationException("每列列车都必须包含 ID 和至少一个作业。");
            }
            if (train.Movements.Select(movement => movement.Id).Distinct(StringComparer.Ordinal).Count() != train.Movements.Count)
                throw new InvalidOperationException($"列车 {train.Id} 的移动 ID 重复。");

            foreach (var movement in train.Movements)
            {
                if (string.IsNullOrWhiteSpace(movement.Id))
                {
                    throw new InvalidOperationException($"列车 {train.Id} 包含没有 ID 的作业。");
                }

                if (!double.IsFinite(movement.MinDurationSeconds) || !double.IsFinite(movement.MaxDurationSeconds) ||
                    movement.MinDurationSeconds < 0 || movement.MaxDurationSeconds < movement.MinDurationSeconds ||
                    !double.IsFinite(movement.OriginalStartSeconds) || !double.IsFinite(movement.OriginalEndSeconds) ||
                    movement.OriginalStartSeconds < 0 || movement.OriginalEndSeconds < 0)
                {
                    throw new InvalidOperationException($"列车 {train.Id} 的作业 {movement.Id} 持续时间范围无效。");
                }
            }
            if (train.ProcessConstraints is not { } process) continue;
            if (!double.IsFinite(process.OriginSeconds) || process.OriginSeconds < 0 || process.OriginSeconds > horizon ||
                process.Events is null || process.Precedences is null || process.Events.Count == 0 ||
                process.Events.Any(ev => ev is null || string.IsNullOrWhiteSpace(ev.Id)) ||
                process.Events.Select(ev => ev.Id).Distinct(StringComparer.Ordinal).Count() != process.Events.Count)
                throw new InvalidOperationException($"列车 {train.Id} 的过程事件图无效。");
            var events = process.Events.ToDictionary(ev => ev.Id, StringComparer.Ordinal);
            foreach (var ev in events.Values)
            {
                if (!double.IsFinite(ev.OriginalTimeSeconds) || ev.OriginalTimeSeconds < process.OriginSeconds || ev.OriginalTimeSeconds > horizon ||
                    ev.FixedTimeSeconds is double fixedTime && (!double.IsFinite(fixedTime) || fixedTime < process.OriginSeconds || fixedTime > horizon) ||
                    ev.LocationIds is null || ev.LocationIds.Any(location => location is null) ||
                    ev.LocationIds.Distinct(StringComparer.Ordinal).Count() != ev.LocationIds.Count)
                    throw new InvalidOperationException($"列车 {train.Id} 的事件 {ev.Id} 时刻或地点无效。");
            }
            foreach (var movement in train.Movements)
            {
                if (movement.StartEventId is null || movement.EndEventId is null ||
                    !events.ContainsKey(movement.StartEventId) || !events.ContainsKey(movement.EndEventId) ||
                    movement.StartEventId == movement.EndEventId || movement.CandidateRouteIds.Count == 0 ||
                    movement.CandidateRouteIds.Any(id => !routeIds.Contains(id)))
                    throw new InvalidOperationException($"列车 {train.Id} 的过程移动 {movement.Id} 缺少有效事件或候选进路。");
                ValidateLocations(movement.StartEventId, movement.StartLocationIdsByRoute);
                ValidateLocations(movement.EndEventId, movement.EndLocationIdsByRoute);
                void ValidateLocations(string eventId, Dictionary<string, List<string>> choices)
                {
                    if (events[eventId].LocationIds.Count == 0) return;
                    if (choices is null || movement.CandidateRouteIds.Any(id =>
                            !choices.TryGetValue(id, out var values) || values is null || values.Count == 0))
                        throw new InvalidOperationException($"列车 {train.Id} 的过程移动 {movement.Id} 地点候选不完整。");
                }
            }
            foreach (var precedence in process.Precedences)
                if (precedence is null || !events.ContainsKey(precedence.LeadingEventId) || !events.ContainsKey(precedence.FollowingEventId) ||
                    precedence.LeadingEventId == precedence.FollowingEventId || !double.IsFinite(precedence.IntervalSeconds) || precedence.IntervalSeconds < 0)
                    throw new InvalidOperationException($"列车 {train.Id} 的次序约束无效。");
        }
    }

    private static bool RoutesConnect(StationCapacityRouteInput first, StationCapacityRouteInput second) =>
        first.CellIds.Count > 0 && second.CellIds.Count > 0 &&
        string.Equals(first.CellIds[^1], second.CellIds[0], StringComparison.OrdinalIgnoreCase);

    private static double EffectiveHorizon(StationCapacitySolveInput input)
    {
        var referenceEnd = input.Trains.Where(train => train.ProcessConstraints is not null)
            .SelectMany(train => train.ProcessConstraints!.Events ?? new())
            .Where(ev => ev is not null).Select(ev => ev.OriginalTimeSeconds)
            .DefaultIfEmpty(0).Max();
        return Math.Max(input.HorizonSeconds, Math.Min(MaximumHorizonSeconds,
            referenceEnd + Math.Max(0, input.Settings?.RightShiftToleranceSeconds ?? 50_400)));
    }

    private static double CleanSecond(double value, double lower, double upper) =>
        Math.Round(Math.Clamp(value, lower, upper), 7);

    private static string FormatTime(double seconds)
    {
        var sign = seconds < 0 ? "-" : string.Empty;
        var time = TimeSpan.FromSeconds(Math.Round(Math.Abs(seconds), 7));
        var clock = $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}";
        var fraction = (time.Ticks % TimeSpan.TicksPerSecond).ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        if (fraction.Length > 0) clock += "." + fraction;
        return time.Days > 0 ? $"{sign}D+{time.Days} {clock}" : sign + clock;
    }

    private static string MapStatus(Solver.ResultStatus status) => status switch
    {
        Solver.ResultStatus.OPTIMAL => "optimal",
        Solver.ResultStatus.FEASIBLE => "feasible",
        Solver.ResultStatus.INFEASIBLE => "infeasible",
        Solver.ResultStatus.UNBOUNDED => "unbounded",
        Solver.ResultStatus.ABNORMAL => "abnormal",
        Solver.ResultStatus.MODEL_INVALID => "model-invalid",
        _ => "not-solved"
    };

    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) ? character : '_'));

    private sealed record MovementContext(
        string Key,
        string VariableId,
        int TrainIndex,
        StationCapacityTrainInput Train,
        StationCapacityMovementInput Movement,
        List<StationCapacityRouteInput> CandidateRoutes,
        List<string> CandidateCellIds);
}
