using System.Diagnostics;
using System.Globalization;
using Google.OrTools.LinearSolver;
using Serilog;
using SwitchYard.Capacity;

namespace SwitchYard.CapacitySolver;

/// <summary>
/// 铁路车站通过能力 MIP 求解器，使用 OR-Tools 混合整数规划协调作业时刻、进路选择、过程事件及轨道电路占用。
/// </summary>
public sealed class MIPCapacitySolver : ICapacitySolver
{
    /// <summary>
    /// 一天包含的秒数 86400，用于限制未使用过程事件图的作业时域，并计算最大支持时域。
    /// </summary>
    private const double DaySeconds = 86_400;
    /// <summary>
    /// 求解输入允许的最大时域，单位为秒，固定为七天；同时限制根据过程事件推导的时域扩展。
    /// </summary>
    private const double MaximumHorizonSeconds = 7 * DaySeconds;
    /// <summary>
    /// 带有 MIP 能力求解器类型上下文的日志记录器，用于记录输入校验、OR-Tools 求解摘要和能力指标。
    /// </summary>
    private static readonly ILogger Logger = Log.ForContext<MIPCapacitySolver>();

    /// <summary>
    /// MIP 能力求解器的协议描述，包含供服务端分派任务的标识、当前实现版本及支持的约束范围。
    /// </summary>
    public CapacityModelDescriptor Descriptor { get; } = new()
    {
        Id = CapacityAgentProtocol.StationCapacityModelId,
        Name = "铁路车站通过能力 MIP 求解器",
        Version = "2.5.0",
        Description = "支持原列车作业与完整作业过程事件图、资源地点、持续时间和轨道电路占用约束。"
    };

    /// <summary>
    /// 接收共享领域层已组装的能力问题，校验后在后台执行混合整数规划求解，并返回强类型结果。
    /// </summary>
    /// <param name="problem">由 SwitchYard.Capacity 组装完成的领域对象图，包含列车、进路、事件及资源的关联。</param>
    /// <param name="executionContext">任务标识和本次求解可使用的资源配额。</param>
    /// <param name="reportProgress">接收进度百分比及中文进度说明的异步回调。</param>
    /// <param name="cancellationToken">用于请求取消求解的令牌。</param>
    /// <returns>包含求解状态、列车时刻及资源占用的强类型结果。</returns>
    public async Task<StationCapacitySolveResult> SolveAsync(
        CapacityProblem problem,
        CapacitySolverExecutionContext executionContext,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(problem);
        cancellationToken.ThrowIfCancellationRequested();
        var input = problem.Input;
        Validate(input);
        Logger.Information(
            "能力计算输入校验完成：{TrainCount} 列列车、{RouteCount} 条进路、{OccupationCount} 项占用参数",
            input.Trains.Count,
            input.Routes.Count,
            input.RouteOccupations.Count);
        await reportProgress(10, "输入数据校验完成");

        return await Task.Run(async () =>
        {
            var result = Solve(problem, executionContext.Resources, reportProgress, cancellationToken);
            await reportProgress(100, "求解结果已生成");
            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// 创建决策变量、约束和目标函数，在资源配额内调用 OR-Tools，并解析列车时刻、进路、占用及能力指标。
    /// </summary>
    /// <param name="problem">已关联列车、进路、事件及占用参数的领域问题。</param>
    /// <param name="resources">限制求解线程数及 SCIP 内存配置的任务配额。</param>
    /// <param name="reportProgress">在建模、求解和结果解析阶段报告进度的回调。</param>
    /// <param name="cancellationToken">取消时中断 OR-Tools 求解并抛出取消异常。</param>
    /// <returns>包含求解状态的结果；存在可行解时还包含时刻、资源占用及能力值。</returns>
    private static StationCapacitySolveResult Solve(
        CapacityProblem problem,
        CapacityTaskResourceLimits resources,
        Func<int, string, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var solver = CreateSolver(out var solverBackend);

        var input = problem.Input;
        var settings = problem.Settings ?? new StationCapacitySolveSettings();
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

        var movements = BuildMovementContexts(problem);
        var startVariables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        var endVariables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);
        var routeVariables = new Dictionary<(string MovementKey, string RouteId), Variable>();
        var occupationStarts = new Dictionary<(string MovementKey, string CellId), Variable>();
        var occupationEnds = new Dictionary<(string MovementKey, string CellId), Variable>();

        foreach (var movement in movements)
        {
            var movementHorizon = movement.Train.Process is null ? Math.Min(DaySeconds, horizon) : horizon;
            startVariables[movement.Key] = solver.MakeNumVar(0, movementHorizon, $"a_{movement.VariableId}");
            endVariables[movement.Key] = solver.MakeNumVar(0, movementHorizon, $"d_{movement.VariableId}");

            foreach (var route in movement.CandidateRoutes)
            {
                routeVariables[(movement.Key, route.Id)] =
                    solver.MakeBoolVar($"x_{movement.VariableId}_{Sanitize(route.Id)}");
            }

            foreach (var cell in movement.CandidateCells)
            {
                var cellId = cell.Id;
                occupationStarts[(movement.Key, cellId)] =
                    solver.MakeNumVar(occupationLower, occupationUpper, $"s_{movement.VariableId}_{Sanitize(cellId)}");
                occupationEnds[(movement.Key, cellId)] =
                    solver.MakeNumVar(occupationLower, occupationUpper, $"e_{movement.VariableId}_{Sanitize(cellId)}");
            }
        }

        reportProgress(30, "决策变量创建完成").GetAwaiter().GetResult();

        BuildMovementConstraints(settings, solver, movements, startVariables, endVariables, routeVariables, horizon);
        var eventVariables = BuildProcessConstraints(settings, solver, movements, startVariables, endVariables, routeVariables, horizon);
        BuildTrainConservationConstraints(solver, movements, startVariables, endVariables);
        BuildRouteConnectionConstraints(solver, movements, routeVariables);
        BuildOccupationConstraints(
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
        var stopwatch = Stopwatch.StartNew();
        var status = solver.Solve();
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
                TrainType = firstContext.Train.Input.TrainType,
                CalculationParameterSetId = firstContext.Train.Input.CalculationParameterSetId,
                EventTimes = firstContext.Train.Process?.Events.ToDictionary(ev => ev.Id,
                    ev => CleanSecond(eventVariables[(firstContext.TrainIndex, ev.Id)].SolutionValue(), 0, horizon),
                    StringComparer.Ordinal) ?? new(StringComparer.Ordinal)
            };

            foreach (var movement in trainGroup.OrderBy(item => item.Movement.Input.Sequence))
            {
                var selectedRoute = movement.CandidateRoutes
                    .OrderBy(route => route.Id, StringComparer.OrdinalIgnoreCase)
                    .First(route => routeVariables[(movement.Key, route.Id)].SolutionValue() > 0.5);
                var start = CleanSecond(startVariables[movement.Key].SolutionValue(), 0, horizon);
                var end = CleanSecond(endVariables[movement.Key].SolutionValue(), 0, horizon);
                var movementResult = new StationCapacityMovementResult
                {
                    Id = movement.Movement.Id,
                    Name = movement.Movement.Input.Name,
                    RouteId = selectedRoute.Id,
                    StartSeconds = start,
                    EndSeconds = end,
                    StartTime = FormatTime(start),
                    EndTime = FormatTime(end)
                };

                foreach (var cell in selectedRoute.Cells)
                {
                    var cellId = cell.Id;
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

    /// <summary>
    /// 优先创建 SCIP 混合整数规划求解器，不可用时回退到 CBC，两者均不可用时抛出异常。
    /// </summary>
    /// <param name="backend">实际选用的后端名称。</param>
    /// <returns>由调用方负责释放的求解器实例。</returns>
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

    /// <summary>
    /// 按列车及作业顺序整理建模上下文，生成唯一键与变量名，并保留候选进路和轨道电路。
    /// </summary>
    /// <param name="problem">已解析候选进路和轨道电路的领域问题。</param>
    /// <returns>供变量和约束创建使用的作业上下文列表。</returns>
    /// <exception cref="InvalidOperationException">存在没有可行进路的作业。</exception>
    private static List<MovementContext> BuildMovementContexts(CapacityProblem problem)
    {
        var result = new List<MovementContext>();
        foreach (var train in problem.Trains)
        {
            var trainIndex = train.Index;
            var orderedMovements = train.Movements.OrderBy(item => item.Input.Sequence).ToList();
            for (var movementIndex = 0; movementIndex < orderedMovements.Count; movementIndex++)
            {
                var movement = orderedMovements[movementIndex];
                var candidates = movement.CandidateRoutes;
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
                    movement.CandidateCells));
            }
        }

        return result;
    }

    /// <summary>
    /// 为每个作业添加持续时间上下限、相对原始时刻的移位范围，以及恰好选择一条候选进路的约束。
    /// </summary>
    /// <remarks>未使用过程事件图的列车，作业时间上界同时受一天时长限制；所有时间量均以秒为单位。</remarks>
    private static void BuildMovementConstraints(
        StationCapacitySolveSettings settings,
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables,
        double horizon)
    {
        foreach (var context in movements)
        {
            var movement = context.Movement.Input;
            var start = starts[context.Key];
            var end = ends[context.Key];
            var movementHorizon = context.Train.Process is null ? Math.Min(DaySeconds, horizon) : horizon;
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

    /// <summary>
    /// 为带过程图的列车创建事件时刻、地点及锚进路选择变量，并绑定作业起讫、锚一致性和事件次序约束。
    /// </summary>
    /// <returns>按列车索引与事件标识索引的时刻变量，供结果解析使用。</returns>
    /// <remarks>每个事件地点及锚进路均为单选；次序默认要求最小间隔，严格次序同时添加上界以固定间隔。</remarks>
    private static Dictionary<(int TrainIndex, string EventId), Variable> BuildProcessConstraints(
        StationCapacitySolveSettings settings, Solver solver, IReadOnlyList<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts, IReadOnlyDictionary<string, Variable> ends,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables, double horizon)
    {
        var result = new Dictionary<(int TrainIndex, string EventId), Variable>();
        foreach (var group in movements.GroupBy(item => item.TrainIndex))
        {
            var process = group.First().Train.Process;
            if (process is null) continue;
            var contextsByMovement = group.ToDictionary(context => context.Movement);
            var locations = new Dictionary<(string EventId, string LocationId), Variable>();
            var anchorRoutes = new Dictionary<(string AnchorId, string RouteId), Variable>();
            foreach (var anchor in process.Anchors)
            {
                LinearExpr sum = new LinearExpr();
                foreach (var route in anchor.Routes)
                {
                    var routeId = route.Id;
                    var variable = solver.MakeBoolVar($"anchor_{group.Key}_{Sanitize(anchor.Id)}_{Sanitize(routeId)}");
                    anchorRoutes[(anchor.Id, routeId)] = variable;
                    sum += variable;
                    if (anchor.Movement is { } ownerMovement)
                    {
                        var owner = contextsByMovement[ownerMovement];
                        if (routeVariables.TryGetValue((owner.Key, routeId), out var selected)) solver.Add(variable == selected);
                        else solver.Add(variable == 0);
                    }
                }
                solver.Add(sum == 1);
            }
            foreach (var ev in process.Events)
            {
                var variable = solver.MakeNumVar(process.OriginSeconds, horizon, $"event_{group.Key}_{Sanitize(ev.Id)}");
                result[(group.Key, ev.Id)] = variable;
                if (ev.OriginalTimeSeconds is double originalTime)
                {
                    solver.Add(variable >= Math.Max(process.OriginSeconds, originalTime - settings.LeftShiftToleranceSeconds));
                    solver.Add(variable <= Math.Min(horizon, originalTime + settings.RightShiftToleranceSeconds));
                }
                if (ev.FixedTimeSeconds is double fixedTime) solver.Add(variable == fixedTime);
                if (ev.Locations.Count == 0) continue;
                for (var index = 0; index < ev.Locations.Count; index++)
                    locations[(ev.Id, ev.Locations[index].Key)] = solver.MakeBoolVar($"location_{group.Key}_{Sanitize(ev.Id)}_{index}");
                LinearExpr sum = locations[(ev.Id, ev.Locations[0].Key)];
                foreach (var location in ev.Locations.Skip(1)) sum += locations[(ev.Id, location.Key)];
                solver.Add(sum == 1);
                foreach (var location in ev.Locations)
                    if (location.Anchor is { } anchor && location.DwellingRoute is { } dwellingRoute)
                        solver.Add(locations[(ev.Id, location.Key)] <= anchorRoutes[(anchor.Id, dwellingRoute.Id)]);
            }
            foreach (var movement in group)
            {
                var startEvent = movement.Movement.StartEvent!;
                var endEvent = movement.Movement.EndEvent!;
                solver.Add(starts[movement.Key] == result[(group.Key, startEvent.Id)]);
                solver.Add(ends[movement.Key] == result[(group.Key, endEvent.Id)]);
                BindLocations(movement, startEvent, atStart: true);
                BindLocations(movement, endEvent, atStart: false);
            }
            foreach (var precedence in process.Precedences)
            {
                var elapsed = result[(group.Key, precedence.FollowingEvent.Id)] - result[(group.Key, precedence.LeadingEvent.Id)];
                solver.Add(elapsed >= precedence.IntervalSeconds);
                if (precedence.IsExact) solver.Add(elapsed <= precedence.IntervalSeconds);
            }

            // 将作业所选进路与起点或终点事件的允许地点绑定；没有允许地点的进路被禁止选择。
            void BindLocations(MovementContext movement, CapacityTrainEvent ev, bool atStart)
            {
                if (ev.Locations.Count == 0) return;
                foreach (var option in movement.Movement.RouteOptions)
                {
                    var permitted = atStart ? option.StartLocations : option.EndLocations;
                    var selected = routeVariables[(movement.Key, option.Route.Id)];
                    if (permitted.Count == 0) { solver.Add(selected == 0); continue; }
                    LinearExpr sum = locations[(ev.Id, permitted[0].Key)];
                    foreach (var location in permitted.Skip(1)) sum += locations[(ev.Id, location.Key)];
                    solver.Add(selected <= sum);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 为未使用过程事件图的列车添加作业时间衔接约束，使前一作业结束时刻等于后一作业开始时刻。
    /// </summary>
    /// <remarks>带过程事件图的列车由事件及次序约束控制时间关系，此处跳过。</remarks>
    private static void BuildTrainConservationConstraints(
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<string, Variable> starts,
        IReadOnlyDictionary<string, Variable> ends)
    {
        foreach (var trainMovements in movements.GroupBy(item => item.TrainIndex))
        {
            if (trainMovements.First().Train.Process is not null) continue;
            var ordered = trainMovements.OrderBy(item => item.Movement.Input.Sequence).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                solver.Add(ends[ordered[index - 1].Key] == starts[ordered[index].Key]);
            }
        }
    }

    /// <summary>
    /// 为未使用过程事件图的列车排除无法首尾衔接的相邻作业进路组合。
    /// </summary>
    /// <remarks>前一进路末轨道电路与后一进路首轨道电路必须引用同一领域对象；过程图列车由地点和锚约束控制衔接。</remarks>
    private static void BuildRouteConnectionConstraints(
        Solver solver,
        IEnumerable<MovementContext> movements,
        IReadOnlyDictionary<(string MovementKey, string RouteId), Variable> routeVariables)
    {
        foreach (var trainMovements in movements.GroupBy(item => item.TrainIndex))
        {
            if (trainMovements.First().Train.Process is not null) continue;
            var ordered = trainMovements.OrderBy(item => item.Movement.Input.Sequence).ToList();
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

    /// <summary>
    /// 将所选进路的轨道电路占用起讫时刻绑定为作业起讫时刻加相应占用偏移。
    /// </summary>
    /// <param name="solver">接收占用约束的求解器。</param>
    /// <param name="movements">待建模的作业上下文。</param>
    /// <param name="starts">按作业键索引的开始时刻变量。</param>
    /// <param name="ends">按作业键索引的结束时刻变量。</param>
    /// <param name="routeVariables">按作业键与进路标识索引的进路选择变量。</param>
    /// <param name="occupationStarts">按作业键与轨道电路标识索引的占用开始变量。</param>
    /// <param name="occupationEnds">按作业键与轨道电路标识索引的占用结束变量。</param>
    /// <param name="bigM">用于在进路未被选择时放松对应占用等式的大常数。</param>
    private static void BuildOccupationConstraints(
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
            foreach (var option in context.Movement.RouteOptions)
            {
                var routeSelected = routeVariables[(context.Key, option.Route.Id)];
                foreach (var window in option.Occupations)
                {
                    var cellId = window.Cell.Id;
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

    /// <summary>
    /// 为不同列车可能共用的轨道电路创建先后顺序变量，使双方实际使用该电路时的占用区间不重叠。
    /// </summary>
    /// <param name="solver">接收资源互斥约束的求解器。</param>
    /// <param name="movements">用于枚举不同列车作业对的上下文列表。</param>
    /// <param name="routeVariables">决定作业是否实际使用共享轨道电路的进路选择变量。</param>
    /// <param name="occupationStarts">各作业在轨道电路上的占用开始变量。</param>
    /// <param name="occupationEnds">各作业在轨道电路上的占用结束变量。</param>
    /// <param name="bigM">用于放松未选中先后分支或未共用电路时的互斥约束的大常数。</param>
    /// <remarks>同一列车的作业对不在本方法中施加互斥约束。</remarks>
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

                var sharedCells = first.CandidateCells.Intersect(second.CandidateCells);
                foreach (var cell in sharedCells)
                {
                    var cellId = cell.Id;
                    var firstCellRoutes = first.CandidateRoutes
                        .Where(route => route.Cells.Contains(cell))
                        .ToList();
                    LinearExpr firstUsesCell = routeVariables[(first.Key, firstCellRoutes[0].Id)];
                    foreach (var route in firstCellRoutes.Skip(1))
                    {
                        firstUsesCell += routeVariables[(first.Key, route.Id)];
                    }

                    var secondCellRoutes = second.CandidateRoutes
                        .Where(route => route.Cells.Contains(cell))
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

    /// <summary>
    /// 累加所有作业对应的目标项，并将求解器目标设为最小化该总和。
    /// </summary>
    /// <remarks>支持最短持续时间总和、最早起讫时刻总和，以及默认的最早结束时刻总和。</remarks>
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

    /// <summary>
    /// 根据目标名称生成单个作业的线性目标项：持续时间、起讫时刻之和，或默认的结束时刻。
    /// </summary>
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

    /// <summary>
    /// 校验时间范围、列车及进路标识、作业持续时间，以及过程图中的事件、锚、地点和次序引用。
    /// </summary>
    /// <param name="input">待校验的车站能力求解输入。</param>
    /// <exception cref="InvalidOperationException">输入缺少必要数据、存在重复标识、越界时间或无效的过程图关联。</exception>
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
                process.Events is null || process.Precedences is null || process.Anchors is null || process.Events.Count == 0 ||
                process.Events.Any(ev => ev is null || string.IsNullOrWhiteSpace(ev.Id)) ||
                process.Events.Select(ev => ev.Id).Distinct(StringComparer.Ordinal).Count() != process.Events.Count)
                throw new InvalidOperationException($"列车 {train.Id} 的过程事件图无效。");
            var events = process.Events.ToDictionary(ev => ev.Id, StringComparer.Ordinal);
            if (process.Anchors.Any(a => a is null || string.IsNullOrWhiteSpace(a.Id) || a.RouteIds is null || a.RouteIds.Count == 0 ||
                    a.RouteIds.Any(string.IsNullOrWhiteSpace) || a.RouteIds.Distinct(StringComparer.Ordinal).Count() != a.RouteIds.Count ||
                    (a.MovementId is not null && !train.Movements.Any(m => m.Id == a.MovementId))) ||
                process.Anchors.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != process.Anchors.Count)
                throw new InvalidOperationException($"列车 {train.Id} 的锚地点约束无效。");
            var anchors = process.Anchors.ToDictionary(a => a.Id, StringComparer.Ordinal);
            foreach (var ev in events.Values)
            {
                if (ev.OriginalTimeSeconds is double originalTime && (!double.IsFinite(originalTime) || originalTime < process.OriginSeconds || originalTime > horizon) ||
                    ev.FixedTimeSeconds is double fixedTime && (!double.IsFinite(fixedTime) || fixedTime < process.OriginSeconds || fixedTime > horizon) ||
                    ev.LocationIds is null || ev.LocationIds.Any(location => location is null) || ev.AnchorChoicesByLocation is null ||
                    ev.LocationIds.Distinct(StringComparer.Ordinal).Count() != ev.LocationIds.Count)
                    throw new InvalidOperationException($"列车 {train.Id} 的事件 {ev.Id} 时刻或地点无效。");
                foreach (var (location, choice) in ev.AnchorChoicesByLocation)
                    if (!ev.LocationIds.Contains(location) || choice is null || choice.AnchorId is null ||
                        !anchors.TryGetValue(choice.AnchorId, out var anchor) || !anchor.RouteIds.Contains(choice.RouteId))
                        throw new InvalidOperationException($"列车 {train.Id} 的事件 {ev.Id} 锚地点候选无效。");
            }
            foreach (var movement in train.Movements)
            {
                if (movement.StartEventId is null || movement.EndEventId is null ||
                    !events.ContainsKey(movement.StartEventId) || !events.ContainsKey(movement.EndEventId) ||
                    movement.StartEventId == movement.EndEventId || movement.CandidateRouteIds.Count == 0 ||
                    movement.CandidateRouteIds.Any(id => !routeIds.Contains(id)))
                    throw new InvalidOperationException($"列车 {train.Id} 的过程移动 {movement.Id} 缺少有效事件或候选进路。");
                ValidateLocations(movement.StartEventId, movement.StartLocationsByRoute);
                ValidateLocations(movement.EndEventId, movement.EndLocationsByRoute);
                // 检查地点候选及其元素非空；事件有地点要求时，确保作业的每条候选进路均提供地点候选。
                void ValidateLocations(string eventId, Dictionary<string, List<StationCapacityLocationInput>> choices)
                {
                    if (choices is null || choices.Values.Any(values => values is null || values.Any(location => location is null)))
                        throw new InvalidOperationException($"列车 {train.Id} 的过程移动 {movement.Id} 包含空地点候选。");
                    if (events[eventId].LocationIds.Count == 0) return;
                    if (movement.CandidateRouteIds.Any(id =>
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

    /// <summary>
    /// 判断两条非空进路能否衔接，即前一进路末轨道电路与后一进路首轨道电路是否为同一对象。
    /// </summary>
    private static bool RoutesConnect(CapacityRoute first, CapacityRoute second) =>
        first.Cells.Count > 0 && second.Cells.Count > 0 &&
        ReferenceEquals(first.Cells[^1], second.Cells[0]);

    /// <summary>
    /// 根据过程事件的最晚原始或固定时刻及右移容差扩展求解时域，并至少保留输入指定的时域。
    /// </summary>
    /// <remarks>事件推导的扩展值最多为七天；输入时域是否合法由输入校验负责。</remarks>
    private static double EffectiveHorizon(StationCapacitySolveInput input)
    {
        var referenceEnd = input.Trains.Where(train => train.ProcessConstraints is not null)
            .SelectMany(train => train.ProcessConstraints!.Events ?? new())
            .Where(ev => ev is not null).SelectMany(ev => new[] { ev.OriginalTimeSeconds, ev.FixedTimeSeconds })
            .Where(time => time.HasValue).Select(time => time.GetValueOrDefault())
            .DefaultIfEmpty(0).Max();
        return Math.Max(input.HorizonSeconds, Math.Min(MaximumHorizonSeconds,
            referenceEnd + Math.Max(0, input.Settings?.RightShiftToleranceSeconds ?? 50_400)));
    }

    /// <summary>
    /// 将求解时刻限制在给定上下界内，并四舍五入到小数点后七位以清理数值误差。
    /// </summary>
    private static double CleanSecond(double value, double lower, double upper) =>
        Math.Round(Math.Clamp(value, lower, upper), 7);

    /// <summary>
    /// 将秒数格式化为时分秒，保留有效的小数秒；负值添加负号，跨日值添加 D+天数前缀。
    /// </summary>
    private static string FormatTime(double seconds)
    {
        var sign = seconds < 0 ? "-" : string.Empty;
        var time = TimeSpan.FromSeconds(Math.Round(Math.Abs(seconds), 7));
        var clock = $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}";
        var fraction = (time.Ticks % TimeSpan.TicksPerSecond).ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        if (fraction.Length > 0) clock += "." + fraction;
        return time.Days > 0 ? $"{sign}D+{time.Days} {clock}" : sign + clock;
    }

    /// <summary>
    /// 将 OR-Tools 求解状态转换为结果协议使用的字符串，未匹配的状态统一记为 not-solved。
    /// </summary>
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

    /// <summary>
    /// 返回有限数值本身，将 NaN 或无穷大替换为零，便于结果序列化。
    /// </summary>
    private static double FiniteOrZero(double value) => double.IsFinite(value) ? value : 0;

    /// <summary>
    /// 保留字母和数字，将其他字符替换为下划线，生成求解器变量名片段。
    /// </summary>
    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) ? character : '_'));

    /// <summary>
    /// 单个列车作业的建模上下文，将领域对象、候选资源及求解变量标识关联起来；位置参数同时生成对应属性。
    /// </summary>
    /// <param name="Key">由列车索引和排序后的作业索引组成的唯一键，用于索引各类决策变量。</param>
    /// <param name="VariableId">由列车索引、作业索引及清理后的作业标识组成的变量名片段。</param>
    /// <param name="TrainIndex">列车在领域问题中的索引，用于区分列车并关联事件变量。</param>
    /// <param name="Train">作业所属的列车领域对象，包含原始输入及可选的过程事件图。</param>
    /// <param name="Movement">当前作业的领域对象，包含作业输入、进路选项及关联的起讫事件。</param>
    /// <param name="CandidateRoutes">该作业可以选择的进路集合。</param>
    /// <param name="CandidateCells">候选进路可能使用的轨道电路集合，用于创建占用变量和枚举资源冲突。</param>
    private sealed record MovementContext(
        string Key,
        string VariableId,
        int TrainIndex,
        CapacityTrain Train,
        CapacityMovement Movement,
        IReadOnlyList<CapacityRoute> CandidateRoutes,
        IReadOnlyList<CapacityCell> CandidateCells);
}
