using SwitchYard.Capacity;
using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

internal static class StationCapacityOperationProcessInputBuilder
{
    public static List<StationCapacityOperationProcessInput> Build(IEnumerable<OperationProcessTemplate> processes) =>
        processes.Select(process => new StationCapacityOperationProcessInput
        {
            Id = process.Id,
            Name = process.Name,
            Description = process.Description,
            Revision = process.Revision,
            Activities = process.Activities.Select(activity => new StationCapacityProcessActivityInput
            {
                Id = activity.Id,
                Name = activity.Name,
                Type = activity.Type,
                MinDuration = activity.MinDuration,
                MaxDuration = activity.MaxDuration,
                StartEvent = activity.StartEvent,
                EndEvent = activity.EndEvent,
                RouteList = activity.RouteList.ToList(),
                SelectedRoute = activity.SelectedRoute,
                X = activity.X,
                Y = activity.Y
            }).ToList(),
            Events = process.Events.Select(ev => new StationCapacityProcessEventInput
            {
                Id = ev.Id,
                Name = ev.Name,
                Time = ev.Time,
                NodeId = ev.NodeID,
                NodeList = ev.NodeList.ToList(),
                AnchorList = ev.AnchorList.ToList(),
                SelectedAnchor = ev.SelectedAnchor,
                AutomaticAnchorIds = ev.AutomaticAnchorIDs.ToList()
            }).ToList(),
            Precedences = process.Precedences.Select(precedence => new StationCapacityProcessPrecedenceInput
            {
                Id = precedence.Id,
                LeadingEvent = precedence.LeadingEvent,
                FollowingEvent = precedence.FollowingEvent,
                Interval = precedence.Interval,
                IsExact = precedence.IsExact
            }).ToList(),
            Anchors = process.Anchors.Select(anchor => new StationCapacityProcessAnchorInput
            {
                Id = anchor.Id,
                Name = anchor.Name,
                DwellingActivityId = anchor.DwellingActivityID,
                RouteIds = anchor.RouteIDs.ToList()
            }).ToList(),
            RouteAnchors = process.RouteAnchors.Select(route => new StationCapacityProcessRouteAnchorsInput
            {
                RouteId = route.RouteID,
                StartAnchor = route.StartAnchor,
                EndAnchor = route.EndAnchor
            }).ToList()
        }).ToList();
}
