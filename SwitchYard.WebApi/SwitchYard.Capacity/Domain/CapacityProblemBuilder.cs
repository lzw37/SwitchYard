
namespace SwitchYard.Capacity;

public sealed partial class CapacityProblem
{
    private void LinkDefinitions()
    {
        var definitions = Index(Input.OperationProcesses ?? [], item => item.Id,
            item => new CapacityOperationProcess(item), "operationProcesses");
        OperationProcesses = definitions;
        foreach (var process in definitions.Values)
        {
            var path = $"operationProcesses[{process.Id}]";
            var allowDraftErrors = !Input.Trains.Any(train => train.OperationProcessId == process.Id);
            process.Activities = Index(process.Input.Activities ?? [], item => item.Id,
                item => new CapacityActivity(process, item), path + ".activities", allowDraftErrors);
            process.Events = Index(process.Input.Events ?? [], item => item.Id,
                item => new CapacityProcessEvent(process, item), path + ".events", allowDraftErrors);
            process.Anchors = Index(process.Input.Anchors ?? [], item => item.Id,
                item => new CapacityAnchor(process, item), path + ".anchors", allowDraftErrors);
            foreach (var activity in process.Activities.Values)
            {
                var itemPath = path + $".activities[{activity.Id}]";
                activity.StartEvent = Find(process.Events, activity.Input.StartEvent, itemPath + ".startEvent", "event");
                activity.EndEvent = Find(process.Events, activity.Input.EndEvent, itemPath + ".endEvent", "event");
                activity.Routes = ResolveMany(activity.Input.RouteList, id => Infrastructure.ResolveRoute(id, itemPath + ".routeList"));
                activity.SelectedRoute = Route(activity.Input.SelectedRoute, itemPath + ".selectedRoute");
            }
            foreach (var anchor in process.Anchors.Values)
            {
                var itemPath = path + $".anchors[{anchor.Id}]";
                anchor.DwellingActivity = Find(process.Activities, anchor.Input.DwellingActivityId, itemPath + ".dwellingActivityId", "activity");
                anchor.Routes = ResolveMany(anchor.Input.RouteIds, id => Infrastructure.ResolveRoute(id, itemPath + ".routeIds"));
            }
            foreach (var ev in process.Events.Values)
            {
                var itemPath = path + $".events[{ev.Id}]";
                ev.Node = Node(ev.Input.NodeId, itemPath + ".nodeId");
                ev.Nodes = ResolveMany(ev.Input.NodeList, id => Infrastructure.ResolveNode(id, itemPath + ".nodeList"));
                ev.Anchors = ResolveMany(ev.Input.AnchorList, id => Find(process.Anchors, id, itemPath + ".anchorList", "anchor"));
                ev.SelectedAnchor = Find(process.Anchors, ev.Input.SelectedAnchor, itemPath + ".selectedAnchor", "anchor");
                ev.AutomaticAnchors = ResolveMany(ev.Input.AutomaticAnchorIds, id => Find(process.Anchors, id, itemPath + ".automaticAnchorIds", "anchor"));
                ev.StartingActivities = process.Activities.Values.Where(activity => ReferenceEquals(activity.StartEvent, ev)).ToList();
                ev.EndingActivities = process.Activities.Values.Where(activity => ReferenceEquals(activity.EndEvent, ev)).ToList();
            }
            process.Precedences = ValidItems(process.Input.Precedences ?? [], path + ".precedences", allowDraftErrors).Select(item => new CapacityPrecedence(item)
            {
                LeadingEvent = Find(process.Events, item.LeadingEvent, path + $".precedences[{item.Id}].leadingEvent", "event"),
                FollowingEvent = Find(process.Events, item.FollowingEvent, path + $".precedences[{item.Id}].followingEvent", "event")
            }).ToList();
            process.RouteAnchors = ValidItems(process.Input.RouteAnchors ?? [], path + ".routeAnchors", allowDraftErrors).Select(item => new CapacityRouteAnchors(item)
            {
                Route = Route(item.RouteId, path + ".routeAnchors.routeId"),
                StartAnchor = Find(process.Anchors, item.StartAnchor, path + ".routeAnchors.startAnchor", "anchor"),
                EndAnchor = Find(process.Anchors, item.EndAnchor, path + ".routeAnchors.endAnchor", "anchor")
            }).ToList();
        }
    }

    private void LinkTrains()
    {
        var trains = Input.Trains.Select((input, index) => new CapacityTrain(input, index)).ToList();
        if (trains.Select(train => train.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != trains.Count)
            throw new InvalidOperationException("列车 ID 重复，无法建立对象引用。");
        Trains = trains;
        TrainsById = trains.ToDictionary(train => train.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var train in trains)
        {
            var path = $"trains[{train.Id}]";
            train.ParameterSet = Infrastructure.ResolveParameterSet(train.Input.CalculationParameterSetId, path + ".calculationParameterSetId");
            train.OperationProcess = Find(OperationProcesses, train.Input.OperationProcessId, path + ".operationProcessId", "operationProcess");
            var movements = Index(train.Input.Movements, item => item.Id,
                item => new CapacityMovement(train, item), path + ".movements");
            train.MovementsById = movements;
            train.Movements = movements.Values.ToList();
            foreach (var movement in train.Movements)
            {
                var itemPath = path + $".movements[{movement.Id}]";
                if (train.OperationProcess is { } template)
                    movement.Activity = Require(template.Activities, movement.Input.ActivityId, itemPath + ".activityId");
                movement.CandidateRoutes = movement.Input.CandidateRouteIds.Count > 0
                    ? ResolveMany(movement.Input.CandidateRouteIds.Distinct(StringComparer.OrdinalIgnoreCase),
                        id => Infrastructure.ResolveRoute(id, itemPath + ".candidateRouteIds"))
                        .Where(route => route.Input is not null).ToList()
                    : Infrastructure.SolverRoutes.Where(route => movement.Input.RequiredRouteTags.All(tag =>
                        route.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))).ToList();
                if (movement.CandidateRoutes.Count == 0)
                    throw new InvalidOperationException($"列车 {train.Id} 的作业 {movement.Id} 没有可行进路。");
                movement.CandidateCells = movement.CandidateRoutes.SelectMany(route => route.Cells).Distinct().ToList();
                movement.OccupationOverrides = movement.Input.CellOccupationOverrides.Select(item => new CapacityMovementOccupation(item)
                {
                    Route = Route(item.RouteId, itemPath + ".cellOccupationOverrides.routeId"),
                    Cell = Infrastructure.ResolveCell(item.CellId, itemPath + ".cellOccupationOverrides.cellId")
                }).ToList();
            }
            LinkExecution(train);
            foreach (var movement in train.Movements)
                movement.RouteOptions = movement.CandidateRoutes.Select(route => new CapacityRouteOption(route)
                {
                    StartLocations = FilterLocations(movement.StartLocationsByRoute, route, movement.StartEvent),
                    EndLocations = FilterLocations(movement.EndLocationsByRoute, route, movement.EndEvent),
                    Occupations = route.Cells.Select(cell => OccupationWindow(movement, route, cell)).ToList()
                }).ToList();
        }
    }

    private void LinkExecution(CapacityTrain train)
    {
        var path = $"trains[{train.Id}]";
        var locations = new Dictionary<(CapacityTrainEvent? Event, string Key), CapacityLocation>();
        if (train.Input.ProcessConstraints is { } input)
        {
            var process = new CapacityTrainProcess(train, input);
            train.Process = process;
            process.EventsById = Index(input.Events, item => item.Id, item => new CapacityTrainEvent(process, item), path + ".events");
            process.Events = process.EventsById.Values.ToList();
            process.AnchorsById = Index(input.Anchors, item => item.Id, item => new CapacityTrainAnchor(process, item), path + ".anchors");
            process.Anchors = process.AnchorsById.Values.ToList();
            foreach (var anchor in process.Anchors)
            {
                anchor.Template = train.OperationProcess?.Anchors.GetValueOrDefault(anchor.Id);
                anchor.Routes = ResolveMany(anchor.Input.RouteIds, id => Infrastructure.ResolveRoute(id, path + $".anchors[{anchor.Id}].routeIds"));
                if (anchor.Input.MovementId is { } movementId)
                    anchor.Movement = Require(train.MovementsById, movementId, path + $".anchors[{anchor.Id}].movementId");
                if (anchor.Routes.Count != anchor.Input.RouteIds.Count)
                    throw new InvalidOperationException($"{path} 的锚 {anchor.Id} 引用了不存在的进路。");
            }
            foreach (var ev in process.Events)
            {
                ev.Template = train.OperationProcess?.Events.GetValueOrDefault(ev.Id);
                ev.Locations = ev.Input.LocationIds.Select(key => Location(ev, key,
                    StationCapacityLocationInput.FromLegacyToken(key))).ToList();
                // Legacy inline jobs keep the anchor choice separately from the location key.
                foreach (var choice in ev.Input.AnchorChoicesByLocation)
                {
                    var location = ev.Locations.FirstOrDefault(item => item.Key == choice.Key)
                        ?? throw new InvalidOperationException($"{path} 的事件 {ev.Id} 引用了不存在的地点。");
                    location.Anchor = Require(process.AnchorsById, choice.Value.AnchorId, path + ".anchorChoices.anchorId");
                    location.DwellingRoute = Route(choice.Value.RouteId, path + ".anchorChoices.routeId")
                        ?? throw new InvalidOperationException($"{path} 的地点引用了不存在的停留进路。");
                }
            }
            process.Precedences = input.Precedences.Select(item => new CapacityTrainPrecedence(item,
                Require(process.EventsById, item.LeadingEventId, path + ".precedences.leadingEvent"),
                Require(process.EventsById, item.FollowingEventId, path + ".precedences.followingEvent"))).ToList();
            foreach (var movement in train.Movements)
            {
                movement.StartEvent = Require(process.EventsById, movement.Input.StartEventId, path + $".movements[{movement.Id}].startEventId");
                movement.EndEvent = Require(process.EventsById, movement.Input.EndEventId, path + $".movements[{movement.Id}].endEventId");
            }
        }
        foreach (var movement in train.Movements)
        {
            movement.StartLocationsByRoute = Map(movement.Input.StartLocationsByRoute, movement.StartEvent, path + $".movements[{movement.Id}].startLocationsByRoute");
            movement.EndLocationsByRoute = Map(movement.Input.EndLocationsByRoute, movement.EndEvent, path + $".movements[{movement.Id}].endLocationsByRoute");
        }

        CapacityLocation Location(CapacityTrainEvent? ev, string key, StationCapacityLocationInput data)
        {
            if (locations.TryGetValue((ev, key), out var existing)) return existing;
            var location = new CapacityLocation(key, data)
            {
                Node = Node(data.NodeId, path + ".locations.nodeId"),
                DwellingRoute = Route(data.DwellingRouteId, path + ".locations.dwellingRouteId")
            };
            if (data.AnchorId is { } anchorId && train.Process is { } execution)
                location.Anchor = Require(execution.AnchorsById, anchorId, path + ".locations.anchorId");
            locations.Add((ev, key), location);
            return location;
        }

        IReadOnlyDictionary<CapacityRoute, IReadOnlyList<CapacityLocation>> Map(
            Dictionary<string, List<StationCapacityLocationInput>> source, CapacityTrainEvent? ev, string mapPath)
        {
            var result = new Dictionary<CapacityRoute, IReadOnlyList<CapacityLocation>>();
            foreach (var pair in source)
            {
                var route = Infrastructure.ResolveRoute(pair.Key, mapPath);
                if (route is null) continue;
                result[route] = pair.Value.Select(item => Location(ev, item.ToSolverKey(), item)).Distinct().ToList();
            }
            return result;
        }
    }

    private CapacityOccupationWindow OccupationWindow(CapacityMovement movement, CapacityRoute route, CapacityCell cell)
    {
        // Overrides retain the existing exact-ID match; parameter-set namespaces remain case-sensitive.
        var originalCellId = route.Input?.CellIds.FirstOrDefault(id => string.Equals(id, cell.Id, StringComparison.OrdinalIgnoreCase)) ?? cell.Id;
        var local = movement.OccupationOverrides.FirstOrDefault(item => item.Input.RouteId == route.Id && item.Input.CellId == originalCellId);
        if (local is not null) return new(cell, local.Input.StartOccupationShiftSeconds, local.Input.EndOccupationShiftSeconds) { Override = local };
        var candidates = Infrastructure.SolverOccupations.Where(item => ReferenceEquals(item.Route, route) && ReferenceEquals(item.Cell, cell) &&
            (movement.Train.ParameterSet is not null ? ReferenceEquals(item.ParameterSet, movement.Train.ParameterSet) :
                item.Input?.CalculationParameterSetId == movement.Train.Input.CalculationParameterSetId)).ToList();
        var selected = candidates.FirstOrDefault(item => string.Equals(item.TrainTypeId, movement.Train.Input.TrainType, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.TrainTypeId));
        if (selected is null)
            throw new InvalidOperationException($"参数集 {movement.Train.Input.CalculationParameterSetId} 的进路 {route.Id}、车种 {movement.Train.Input.TrainType}、轨道电路 {cell.Id} 缺少占用时间参数。");
        return new(cell, selected.StartOccupationShiftSeconds, selected.EndOccupationShiftSeconds) { Source = selected };
    }

    private static IReadOnlyList<CapacityLocation> FilterLocations(
        IReadOnlyDictionary<CapacityRoute, IReadOnlyList<CapacityLocation>> map, CapacityRoute route, CapacityTrainEvent? ev) =>
        map.TryGetValue(route, out var candidates)
            ? (ev is null ? candidates : candidates.Where(ev.Locations.Contains).ToList()) : [];

    private CapacityRoute? Route(string? id, string path) => string.IsNullOrWhiteSpace(id) ? null : Infrastructure.ResolveRoute(id, path);
    private CapacityNode? Node(string? id, string path) => string.IsNullOrWhiteSpace(id) ? null : Infrastructure.ResolveNode(id, path);

    private T? Find<T>(IReadOnlyDictionary<string, T> map, string? id, string path, string type) where T : class
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (map.TryGetValue(id, out var value)) return value;
        Infrastructure.Issues.Add(new(path, type, id));
        return null;
    }

    private static T Require<T>(IReadOnlyDictionary<string, T> map, string? id, string path) where T : class =>
        id is not null && map.TryGetValue(id, out var value) ? value :
            throw new InvalidOperationException($"{path} 引用了不存在的对象 {id ?? "<null>"}。");

    private static IReadOnlyList<T> ResolveMany<T>(IEnumerable<string>? ids, Func<string, T?> resolve) where T : class =>
        (ids ?? []).Select(resolve).OfType<T>().Distinct().ToList();

    private Dictionary<string, T> Index<TInput, T>(IEnumerable<TInput> items, Func<TInput, string> id,
        Func<TInput, T> create, string path, bool allowDraftErrors = false) where TInput : class where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
            if (item is null || string.IsNullOrWhiteSpace(id(item)) || !result.TryAdd(id(item), create(item)))
            {
                if (allowDraftErrors)
                {
                    Infrastructure.Issues.Add(new(path, typeof(T).Name, item is null ? "" : id(item)));
                    continue;
                }
                throw new InvalidOperationException($"{path} 包含空对象、空 ID 或重复 ID。");
            }
        return result;
    }

    private IEnumerable<T> ValidItems<T>(IEnumerable<T> items, string path, bool allowDraftErrors) where T : class
    {
        foreach (var item in items)
        {
            if (item is not null) { yield return item; continue; }
            if (!allowDraftErrors) throw new InvalidOperationException($"{path} 包含空对象。");
            Infrastructure.Issues.Add(new(path, typeof(T).Name, ""));
        }
    }
}
