
namespace SwitchYard.Capacity;

/// <summary>One job's object graph. Shared definitions and per-train execution state have separate identities.</summary>
public sealed partial class CapacityProblem
{
    private CapacityProblem(StationCapacitySolveInput input)
    {
        Input = input;
        Infrastructure = CapacityInfrastructure.Build(input);
    }

    public StationCapacitySolveInput Input { get; }
    public string InstanceId => Input.InstanceId;
    public string StationSchemeId => Input.StationSchemeId;
    public string OperationPlanId => Input.OperationPlanId;
    public StationCapacitySolveSettings Settings => Input.Settings;
    public CapacityInfrastructure Infrastructure { get; }
    public IReadOnlyDictionary<string, CapacityRoute> Routes => Infrastructure.Routes;
    public IReadOnlyList<CapacityReferenceIssue> Issues => Infrastructure.Issues;
    public IReadOnlyDictionary<string, CapacityOperationProcess> OperationProcesses { get; private set; } = null!;
    public IReadOnlyList<CapacityTrain> Trains { get; private set; } = null!;
    public IReadOnlyDictionary<string, CapacityTrain> TrainsById { get; private set; } = null!;

    /// <summary>从强类型输入组装单次求解的完整领域对象图，统一解析过程引用并关联列车、进路、事件及资源。</summary>
    /// <param name="input">调用方已完成反序列化的车站能力输入。</param>
    /// <returns>可直接传给求解器的完整能力问题对象。</returns>
    public static CapacityProblem Build(StationCapacitySolveInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        StationCapacityProcessReferenceResolver.Resolve(input);
        var problem = new CapacityProblem(input);
        problem.LinkDefinitions();
        problem.LinkTrains();
        return problem;
    }
}

public sealed class CapacityOperationProcess(StationCapacityOperationProcessInput input)
{
    public StationCapacityOperationProcessInput Input { get; } = input;
    public string Id => Input.Id;
    public string Name => Input.Name;
    public string Description => Input.Description;
    public int Revision => Input.Revision;
    public IReadOnlyDictionary<string, CapacityActivity> Activities { get; internal set; } = null!;
    public IReadOnlyDictionary<string, CapacityProcessEvent> Events { get; internal set; } = null!;
    public IReadOnlyDictionary<string, CapacityAnchor> Anchors { get; internal set; } = null!;
    public IReadOnlyList<CapacityPrecedence> Precedences { get; internal set; } = [];
    public IReadOnlyList<CapacityRouteAnchors> RouteAnchors { get; internal set; } = [];
}

public sealed class CapacityActivity(CapacityOperationProcess process, StationCapacityProcessActivityInput input)
{
    public CapacityOperationProcess Process { get; } = process;
    public StationCapacityProcessActivityInput Input { get; } = input;
    public string Id => Input.Id;
    public string Name => Input.Name;
    public string Type => Input.Type;
    public CapacityProcessEvent? StartEvent { get; internal set; }
    public CapacityProcessEvent? EndEvent { get; internal set; }
    public IReadOnlyList<CapacityRoute> Routes { get; internal set; } = [];
    public CapacityRoute? SelectedRoute { get; internal set; }
}

public sealed class CapacityProcessEvent(CapacityOperationProcess process, StationCapacityProcessEventInput input)
{
    public CapacityOperationProcess Process { get; } = process;
    public StationCapacityProcessEventInput Input { get; } = input;
    public string Id => Input.Id;
    public string Name => Input.Name;
    public CapacityNode? Node { get; internal set; }
    public IReadOnlyList<CapacityNode> Nodes { get; internal set; } = [];
    public IReadOnlyList<CapacityAnchor> Anchors { get; internal set; } = [];
    public CapacityAnchor? SelectedAnchor { get; internal set; }
    public IReadOnlyList<CapacityAnchor> AutomaticAnchors { get; internal set; } = [];
    public IReadOnlyList<CapacityActivity> StartingActivities { get; internal set; } = [];
    public IReadOnlyList<CapacityActivity> EndingActivities { get; internal set; } = [];
}

public sealed class CapacityAnchor(CapacityOperationProcess process, StationCapacityProcessAnchorInput input)
{
    public CapacityOperationProcess Process { get; } = process;
    public StationCapacityProcessAnchorInput Input { get; } = input;
    public string Id => Input.Id;
    public string Name => Input.Name;
    public CapacityActivity? DwellingActivity { get; internal set; }
    public IReadOnlyList<CapacityRoute> Routes { get; internal set; } = [];
}

public sealed class CapacityPrecedence(StationCapacityProcessPrecedenceInput input)
{
    public StationCapacityProcessPrecedenceInput Input { get; } = input;
    public string Id => Input.Id;
    public CapacityProcessEvent? LeadingEvent { get; internal set; }
    public CapacityProcessEvent? FollowingEvent { get; internal set; }
}

public sealed class CapacityRouteAnchors(StationCapacityProcessRouteAnchorsInput input)
{
    public StationCapacityProcessRouteAnchorsInput Input { get; } = input;
    public CapacityRoute? Route { get; internal set; }
    public CapacityAnchor? StartAnchor { get; internal set; }
    public CapacityAnchor? EndAnchor { get; internal set; }
}

public sealed class CapacityTrain(StationCapacityTrainInput input, int index)
{
    public StationCapacityTrainInput Input { get; } = input;
    public string Id => Input.Id;
    public int Index { get; } = index;
    public CapacityParameterSet? ParameterSet { get; internal set; }
    public CapacityOperationProcess? OperationProcess { get; internal set; }
    public CapacityTrainProcess? Process { get; internal set; }
    public IReadOnlyList<CapacityMovement> Movements { get; internal set; } = [];
    public IReadOnlyDictionary<string, CapacityMovement> MovementsById { get; internal set; } = null!;
}

public sealed class CapacityMovement(CapacityTrain train, StationCapacityMovementInput input)
{
    public CapacityTrain Train { get; } = train;
    public StationCapacityMovementInput Input { get; } = input;
    public string Id => Input.Id;
    public CapacityActivity? Activity { get; internal set; }
    public CapacityTrainEvent? StartEvent { get; internal set; }
    public CapacityTrainEvent? EndEvent { get; internal set; }
    public IReadOnlyList<CapacityRoute> CandidateRoutes { get; internal set; } = [];
    public IReadOnlyList<CapacityCell> CandidateCells { get; internal set; } = [];
    public IReadOnlyList<CapacityRouteOption> RouteOptions { get; internal set; } = [];
    public IReadOnlyDictionary<CapacityRoute, IReadOnlyList<CapacityLocation>> StartLocationsByRoute { get; internal set; } = null!;
    public IReadOnlyDictionary<CapacityRoute, IReadOnlyList<CapacityLocation>> EndLocationsByRoute { get; internal set; } = null!;
    public IReadOnlyList<CapacityMovementOccupation> OccupationOverrides { get; internal set; } = [];
}

public sealed class CapacityRouteOption(CapacityRoute route)
{
    public CapacityRoute Route { get; } = route;
    public IReadOnlyList<CapacityLocation> StartLocations { get; internal set; } = [];
    public IReadOnlyList<CapacityLocation> EndLocations { get; internal set; } = [];
    public IReadOnlyList<CapacityOccupationWindow> Occupations { get; internal set; } = [];
}

public sealed class CapacityMovementOccupation(StationCapacityRouteOccupationInput input)
{
    public StationCapacityRouteOccupationInput Input { get; } = input;
    public CapacityRoute? Route { get; internal set; }
    public CapacityCell? Cell { get; internal set; }
}

public sealed class CapacityOccupationWindow(CapacityCell cell, double start, double end)
{
    public CapacityCell Cell { get; } = cell;
    public double StartOccupationShiftSeconds { get; } = start;
    public double EndOccupationShiftSeconds { get; } = end;
    public CapacityRouteOccupation? Source { get; internal set; }
    public CapacityMovementOccupation? Override { get; internal set; }
}

public sealed class CapacityTrainProcess(CapacityTrain train, StationCapacityProcessInput input)
{
    public CapacityTrain Train { get; } = train;
    public StationCapacityProcessInput Input { get; } = input;
    public CapacityOperationProcess? Template => Train.OperationProcess;
    public double OriginSeconds => Input.OriginSeconds;
    public IReadOnlyList<CapacityTrainEvent> Events { get; internal set; } = [];
    public IReadOnlyDictionary<string, CapacityTrainEvent> EventsById { get; internal set; } = null!;
    public IReadOnlyList<CapacityTrainAnchor> Anchors { get; internal set; } = [];
    public IReadOnlyDictionary<string, CapacityTrainAnchor> AnchorsById { get; internal set; } = null!;
    public IReadOnlyList<CapacityTrainPrecedence> Precedences { get; internal set; } = [];
}

public sealed class CapacityTrainEvent(CapacityTrainProcess process, StationCapacityEventInput input)
{
    public CapacityTrainProcess Process { get; } = process;
    public StationCapacityEventInput Input { get; } = input;
    public string Id => Input.Id;
    public double? OriginalTimeSeconds => Input.OriginalTimeSeconds;
    public double? FixedTimeSeconds => Input.FixedTimeSeconds;
    public CapacityProcessEvent? Template { get; internal set; }
    public IReadOnlyList<CapacityLocation> Locations { get; internal set; } = [];
}

public sealed class CapacityTrainAnchor(CapacityTrainProcess process, StationCapacityAnchorInput input)
{
    public CapacityTrainProcess Process { get; } = process;
    public StationCapacityAnchorInput Input { get; } = input;
    public string Id => Input.Id;
    public CapacityAnchor? Template { get; internal set; }
    public IReadOnlyList<CapacityRoute> Routes { get; internal set; } = [];
    public CapacityMovement? Movement { get; internal set; }
}

public sealed class CapacityTrainPrecedence(StationCapacityPrecedenceInput input, CapacityTrainEvent leading, CapacityTrainEvent following)
{
    public StationCapacityPrecedenceInput Input { get; } = input;
    public CapacityTrainEvent LeadingEvent { get; } = leading;
    public CapacityTrainEvent FollowingEvent { get; } = following;
    public double IntervalSeconds => Input.IntervalSeconds;
    public bool IsExact => Input.IsExact;
}

public sealed class CapacityLocation(string key, StationCapacityLocationInput input)
{
    public string Key { get; } = key;
    public StationCapacityLocationInput Input { get; } = input;
    public CapacityNode? Node { get; internal set; }
    public CapacityTrainAnchor? Anchor { get; internal set; }
    public CapacityRoute? DwellingRoute { get; internal set; }
}
