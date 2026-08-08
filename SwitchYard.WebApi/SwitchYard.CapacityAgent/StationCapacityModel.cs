using System.Diagnostics;
using System.Text.Json;
using Google.OrTools.LinearSolver;
using SwitchYard.Capacity;

namespace SwitchYard.CapacityAgent;

internal sealed class StationCapacityModel : ICapacityModel
{
    private const double BigM = 300_000;
    private const double DaySeconds = 86_400;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CapacityModelDescriptor Descriptor { get; } = new()
    {
        Id = CapacityAgentProtocol.StationCapacityModelId,
        Name = "铁路车站通过能力模型",
        Version = "1.0.0",
        Description = "复现 PyStationCapacity 的作业时间、进路选择、进路连通及轨道电路占用冲突约束。"
    };

    public async Task<JsonElement> SolveAsync(
        JsonElement input,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        var modelInput = input.Deserialize<StationCapacitySolveInput>(JsonOptions)
            ?? throw new InvalidOperationException("求解输入为空或格式不正确。");

        Validate(modelInput);
        await reportProgress(10, "输入数据校验完成");

        return await Task.Run(async () =>
        {
            var result = Solve(modelInput, reportProgress, cancellationToken);
            await reportProgress(100, "求解结果已生成");
            return JsonSerializer.SerializeToElement(result, JsonOptions);
        }, cancellationToken);
    }

    private static StationCapacitySolveResult Solve(
        StationCapacitySolveInput input,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var solver = Solver.CreateSolver("SCIP") ?? Solver.CreateSolver("CBC_MIXED_INTEGER_PROGRAMMING")
            ?? throw new InvalidOperationException("当前 OR-Tools 运行环境不包含可用的混合整数规划求解器。");

        var settings = input.Settings ?? new StationCapacitySolveSettings();
        solver.SetTimeLimit(Math.Clamp(settings.TimeLimitSeconds, 1, 86_400) * 1000L);
        solver.SetNumThreads(Math.Clamp(settings.ThreadCount, 1, 64));

        var routeMap = input.Routes.ToDictionary(route => route.Id, StringComparer.OrdinalIgnoreCase);
        var movements = BuildMovementContexts(input, routeMap);
        var startVariables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        var endVariables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        var routeVariables = new Dictionary<(string MovementKey, string RouteId), Variable>();
        var occupationStarts = new Dictionary<(string MovementKey, string CellId), Variable>();
        var occupationEnds = new Dictionary<(string MovementKey, string CellId), Variable>();

        foreach (var movement in movements)
        {
            startVariables[movement.Key] = solver.MakeNumVar(0, DaySeconds, $"a_{movement.VariableId}");
            endVariables[movement.Key] = solver.MakeNumVar(0, DaySeconds, $"d_{movement.VariableId}");

            foreach (var route in movement.CandidateRoutes)
            {
                routeVariables[(movement.Key, route.Id)] =
                    solver.MakeBoolVar($"x_{movement.VariableId}_{Sanitize(route.Id)}");
            }

            foreach (var cellId in movement.CandidateCellIds)
            {
                occupationStarts[(movement.Key, cellId)] =
                    solver.MakeNumVar(-DaySeconds, DaySeconds * 2, $"s_{movement.VariableId}_{Sanitize(cellId)}");
                occupationEnds[(movement.Key, cellId)] =
                    solver.MakeNumVar(-DaySeconds, DaySeconds * 2, $"e_{movement.VariableId}_{Sanitize(cellId)}");
            }
        }

        reportProgress(30, "决策变量创建完成").GetAwaiter().GetResult();

        BuildMovementConstraints(input, solver, movements, startVariables, endVariables, routeVariables);
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
            occupationEnds);
        BuildConflictConstraints(
            solver,
            movements,
            routeVariables,
            occupationStarts,
            occupationEnds);
        BuildObjective(settings.Objective, solver, movements, startVariables, endVariables);

        reportProgress(55, "模型创建完成，开始调用 OR-Tools 求解").GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        using var cancellationRegistration = cancellationToken.Register(() => solver.InterruptSolve());
        var stopwatch = Stopwatch.StartNew();
        var status = solver.Solve();
        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();

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
            return result;
        }

        result.ObjectiveValue = FiniteOrZero(solver.Objective().Value());
        result.BestBound = FiniteOrZero(solver.Objective().BestBound());
        result.MipGap = Math.Abs(result.ObjectiveValue) < 1e-9
            ? 0
            : Math.Max(0, (result.ObjectiveValue - result.BestBound) / Math.Abs(result.ObjectiveValue));

        var lastOccupationEnd = 0;
        foreach (var trainGroup in movements.GroupBy(item => item.TrainIndex).OrderBy(group => group.Key))
        {
            var firstContext = trainGroup.First();
            var trainResult = new StationCapacityTrainResult
            {
                Id = firstContext.Train.Id,
                TrainType = firstContext.Train.TrainType
            };

            foreach (var movement in trainGroup.OrderBy(item => item.Movement.Sequence))
            {
                var selectedRoute = movement.CandidateRoutes
                    .OrderBy(route => route.Id, StringComparer.OrdinalIgnoreCase)
                    .First(route => routeVariables[(movement.Key, route.Id)].SolutionValue() > 0.5);
                var start = ClampSecond(startVariables[movement.Key].SolutionValue());
                var end = ClampSecond(endVariables[movement.Key].SolutionValue());
                var movementResult = new StationCapacityMovementResult
                {
                    Id = movement.Movement.Id,
                    Name = movement.Movement.Name,
                    RouteId = selectedRoute.Id,
                    StartSeconds = start,
                    EndSeconds = end,
                    StartTime = FormatTime(start),
                    EndTime = FormatTime(end)
                };

                foreach (var cellId in selectedRoute.CellIds)
                {
                    var occupationStart = ClampOccupationSecond(
                        occupationStarts[(movement.Key, cellId)].SolutionValue());
                    var occupationEnd = ClampOccupationSecond(
                        occupationEnds[(movement.Key, cellId)].SolutionValue());
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
        reportProgress(90, "求解完成，正在解析结果").GetAwaiter().GetResult();
        return result;
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
            .Where(route => movement.RequiredRouteTags.All(requiredTag =>
                route.Tags.Contains(requiredTag, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    private static void BuildMovementConstraints(
        StationCapacitySolveInput input,
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables)
    {
        var settings = input.Settings ?? new StationCapacitySolveSettings();
        foreach (var context in movements)
        {
            var movement = context.Movement;
            var start = starts[context.Key];
            var end = ends[context.Key];
            solver.Add(end - start >= movement.MinDurationSeconds);
            solver.Add(end - start <= movement.MaxDurationSeconds);
            solver.Add(start >= Math.Max(0, movement.OriginalStartSeconds - settings.LeftShiftToleranceSeconds));
            solver.Add(start <= Math.Min(DaySeconds, movement.OriginalStartSeconds + settings.RightShiftToleranceSeconds));
            solver.Add(end >= Math.Max(0, movement.OriginalEndSeconds - settings.LeftShiftToleranceSeconds));
            solver.Add(end <= Math.Min(DaySeconds, movement.OriginalEndSeconds + settings.RightShiftToleranceSeconds));

            LinearExpr routeSum = routeVariables[(context.Key, context.CandidateRoutes[0].Id)];
            foreach (var route in context.CandidateRoutes.Skip(1))
            {
                routeSum += routeVariables[(context.Key, route.Id)];
            }

            solver.Add(routeSum == 1);
        }
    }

    private static void BuildTrainConservationConstraints(
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends)
    {
        foreach (var trainMovements in movements.GroupBy(item => item.TrainIndex))
        {
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
        IReadOnlyDictionary<(string MovementKey, string CellId), Variable> occupationEnds)
    {
        foreach (var context in movements)
        {
            foreach (var route in context.CandidateRoutes)
            {
                var routeSelected = routeVariables[(context.Key, route.Id)];
                foreach (var cellId in route.CellIds)
                {
                    var window = ResolveOccupationWindow(input.RouteOccupations, route.Id, context.Train.TrainType, cellId);
                    var occupationStart = occupationStarts[(context.Key, cellId)];
                    var occupationEnd = occupationEnds[(context.Key, cellId)];
                    solver.Add(occupationStart + BigM * (1 - routeSelected) >=
                               starts[context.Key] + window.StartOccupationShiftSeconds);
                    solver.Add(occupationStart <=
                               starts[context.Key] + window.StartOccupationShiftSeconds + BigM * (1 - routeSelected));
                    solver.Add(occupationEnd + BigM * (1 - routeSelected) >=
                               ends[context.Key] + window.EndOccupationShiftSeconds);
                    solver.Add(occupationEnd <=
                               ends[context.Key] + window.EndOccupationShiftSeconds + BigM * (1 - routeSelected));
                }
            }
        }
    }

    private static void BuildConflictConstraints(
        Solver solver,
        IReadOnlyList<MovementContext> movements,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables,
        IReadOnlyDictionary<(string MovementKey, string CellId), Variable> occupationStarts,
        IReadOnlyDictionary<(string MovementKey, string CellId), Variable> occupationEnds)
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
                    var unusedRelaxation = BigM * (2 - firstUsesCell - secondUsesCell);
                    solver.Add(occupationStarts[(second.Key, cellId)] >=
                               occupationEnds[(first.Key, cellId)] - BigM * (1 - firstBeforeSecond) - unusedRelaxation);
                    solver.Add(occupationStarts[(first.Key, cellId)] >=
                               occupationEnds[(second.Key, cellId)] - BigM * firstBeforeSecond - unusedRelaxation);
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

        foreach (var train in input.Trains)
        {
            if (string.IsNullOrWhiteSpace(train.Id) || train.Movements.Count == 0)
            {
                throw new InvalidOperationException("每列列车都必须包含 ID 和至少一个作业。");
            }

            foreach (var movement in train.Movements)
            {
                if (string.IsNullOrWhiteSpace(movement.Id))
                {
                    throw new InvalidOperationException($"列车 {train.Id} 包含没有 ID 的作业。");
                }

                if (movement.MinDurationSeconds < 0 || movement.MaxDurationSeconds < movement.MinDurationSeconds)
                {
                    throw new InvalidOperationException($"列车 {train.Id} 的作业 {movement.Id} 持续时间范围无效。");
                }
            }
        }
    }

    private static bool RoutesConnect(StationCapacityRouteInput first, StationCapacityRouteInput second) =>
        first.CellIds.Count > 0 && second.CellIds.Count > 0 &&
        string.Equals(first.CellIds[^1], second.CellIds[0], StringComparison.OrdinalIgnoreCase);

    private static int ClampSecond(double value) => (int)Math.Clamp(Math.Round(value), 0, DaySeconds);

    private static int ClampOccupationSecond(double value) =>
        (int)Math.Clamp(Math.Round(value), -DaySeconds, DaySeconds * 2);

    private static string FormatTime(int seconds)
    {
        var sign = seconds < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(seconds);
        return $"{sign}{absolute / 3600:00}:{absolute % 3600 / 60:00}:{absolute % 60:00}";
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
