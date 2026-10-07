using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Maintains anchors in editable templates; never rewrites execution snapshots.</summary>
public static class OperationProcessDwellingAnchors
{
    public static void Refresh(OperationProcessTemplate template, OperationProcessTemplate? original = null)
    {
        // Leave malformed documents to the ordinary graph validator.
        if (template.Activities is null || template.Events is null || template.Anchors is null ||
            template.Precedences is null || template.RouteAnchors is null ||
            template.Activities.Any(a => a is null || a.Id is null || a.RouteList is null) ||
            template.Events.Any(e => e is null || e.Id is null || e.AnchorList is null) ||
            template.Anchors.Any(a => a is null) || template.Precedences.Any(p => p is null) ||
            template.RouteAnchors.Any(r => r is null)) return;

        // Keep ownership authoritative even if an older client omits the derived metadata.
        if (original is not null)
        {
            foreach (var old in original.Anchors.Where(a => a.DwellingActivityID is not null))
            {
                var anchor = template.Anchors.FirstOrDefault(a => a.Id == old.Id);
                if (anchor is null)
                    template.Anchors.Add(new() { Id = old.Id, Name = old.Name, RouteIDs = old.RouteIDs.ToList(), DwellingActivityID = old.DwellingActivityID });
                else anchor.DwellingActivityID = old.DwellingActivityID;
            }
            foreach (var ev in template.Events)
            {
                var old = original.Events.FirstOrDefault(item => item.Id == ev.Id);
                ev.AutomaticAnchorIDs = (ev.AutomaticAnchorIDs ?? new()).Concat(old?.AutomaticAnchorIDs ?? new()).Distinct(StringComparer.Ordinal).ToList();
            }
        }

        var dwellings = template.Activities.Where(a => a.Type == "Dwelling").ToList();
        var dwellingIDs = dwellings.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var anchor in template.Anchors.Where(a => a.DwellingActivityID is not null && !dwellingIDs.Contains(a.DwellingActivityID)).ToList())
        {
            template.Anchors.Remove(anchor);
            foreach (var ev in template.Events)
            {
                ev.AnchorList.RemoveAll(id => id == anchor.Id);
                ev.AutomaticAnchorIDs?.RemoveAll(id => id == anchor.Id);
                if (ev.SelectedAnchor == anchor.Id) ev.SelectedAnchor = null;
            }
            foreach (var binding in template.RouteAnchors)
            {
                if (binding.StartAnchor == anchor.Id) binding.StartAnchor = null;
                if (binding.EndAnchor == anchor.Id) binding.EndAnchor = null;
            }
        }

        var owned = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var activity in dwellings)
        {
            var endpoints = template.Events.Where(e => e.Id == activity.StartEvent || e.Id == activity.EndEvent).ToList();
            var anchor = template.Anchors.FirstOrDefault(a => a.DwellingActivityID == activity.Id);
            if (anchor is null)
            {
                anchor = template.Anchors.FirstOrDefault(a => a.DwellingActivityID is null && a.RouteIDs is { Count: > 0 } &&
                    a.RouteIDs.Distinct(StringComparer.Ordinal).Count() == a.RouteIDs.Count && a.RouteIDs.All(id => activity.RouteList.Contains(id)) && endpoints.Count == 2 &&
                    endpoints.All(ev => ev.AnchorList.Count == 1 && ev.AnchorList[0] == a.Id) &&
                    !dwellings.Any(other => other.Id != activity.Id && template.Events.Any(ev =>
                        (ev.Id == other.StartEvent || ev.Id == other.EndEvent) && ev.AnchorList.Contains(a.Id))));
                if (anchor is not null) anchor.DwellingActivityID = activity.Id;
                else
                {
                    var name = activity.Name ?? "";
                    anchor = new() { Id = "anchor-" + Guid.NewGuid(), Name = name[..Math.Min(199, name.Length)] + "锚", DwellingActivityID = activity.Id };
                    template.Anchors.Add(anchor);
                }
            }
            anchor.RouteIDs = activity.RouteList.ToList();
            foreach (var ev in endpoints)
            {
                if (!owned.TryGetValue(ev.Id, out var ids)) owned[ev.Id] = ids = new();
                if (!ids.Contains(anchor.Id)) ids.Add(anchor.Id);
            }
        }
        var inherited = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var activityEvents = template.Activities.SelectMany(a => new[] { a.StartEvent, a.EndEvent }).ToHashSet(StringComparer.Ordinal);
        foreach (var link in template.Precedences)
        {
            Connect(link.LeadingEvent, link.FollowingEvent);
            Connect(link.FollowingEvent, link.LeadingEvent);
        }
        foreach (var ev in template.Events)
        {
            var manual = ev.AnchorList.Where(id => ev.AutomaticAnchorIDs?.Contains(id) != true).ToList();
            var isDwelling = owned.TryGetValue(ev.Id, out var derived);
            derived = isDwelling
                ? derived!.Concat(inherited.GetValueOrDefault(ev.Id, new())).Distinct(StringComparer.Ordinal).ToList()
                : inherited.GetValueOrDefault(ev.Id, new()).Where(id => !manual.Contains(id)).ToList();
            ev.AnchorList = isDwelling ? derived.ToList() : manual.Concat(derived).ToList();
            ev.AutomaticAnchorIDs = derived.ToList();
            if (isDwelling || derived.Count > 0 || (ev.SelectedAnchor is not null && !ev.AnchorList.Contains(ev.SelectedAnchor))) ev.SelectedAnchor = null;
        }
        // Old bindings cannot override automatic events or retain removed dwelling candidates.
        var events = template.Events.GroupBy(ev => ev.Id).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var required = new Dictionary<(string RouteID, bool Start), HashSet<string>>();
        foreach (var activity in template.Activities)
            foreach (var (eventID, start) in new[] { (activity.StartEvent, true), (activity.EndEvent, false) })
            {
                var automatic = eventID is null ? null : events.GetValueOrDefault(eventID)?.AutomaticAnchorIDs;
                if (automatic is not { Count: > 0 }) continue;
                foreach (var routeID in activity.RouteList.Where(id => id is not null))
                    foreach (var side in activity.Type == "Dwelling" ? new[] { true, false } : new[] { start })
                    {
                        var key = (routeID, side);
                        if (required.TryGetValue(key, out var prior)) prior.IntersectWith(automatic);
                        else required[key] = automatic.ToHashSet(StringComparer.Ordinal);
                    }
            }
        var managed = template.Anchors.Where(a => a.DwellingActivityID is not null).Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var binding in template.RouteAnchors)
        {
            if (Clear(binding.RouteID, true, binding.StartAnchor)) binding.StartAnchor = null;
            if (Clear(binding.RouteID, false, binding.EndAnchor)) binding.EndAnchor = null;
        }
        bool Clear(string routeID, bool start, string? bound) => bound is not null &&
            (required.TryGetValue((routeID, start), out var allowed) ? !allowed.Contains(bound) : managed.Contains(bound));

        void Connect(string? sourceID, string? targetID)
        {
            if (sourceID is null || targetID is null || !activityEvents.Contains(targetID) || !owned.TryGetValue(sourceID, out var anchors)) return;
            if (!inherited.TryGetValue(targetID, out var ids)) inherited[targetID] = ids = new();
            foreach (var id in anchors) if (!ids.Contains(id)) ids.Add(id);
        }
    }
}
