using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

/// <summary>Selects one complete, continuous chain for the sequential train-template generator.</summary>
public static class OperationRouteSequenceSelector
{
    public static List<string> Select(ProcessCatalog catalog, IReadOnlyList<List<string>> alternatives, int trainIndex)
    {
        var routes = catalog.Routes.ToDictionary(route => route.Id, StringComparer.OrdinalIgnoreCase);
        var viable = new List<List<ProcessCatalogRoute>>(alternatives.Count);
        foreach (var ids in alternatives)
        {
            var choices = ids.Select(id => routes.GetValueOrDefault(id)).Where(route => route is not null)
                .Select(route => route!).ToList();
            if (choices.Count == 0) throw new ArgumentException("作业模板没有有效的备选进路，无法生成连续进路。");
            viable.Add(Enumerable.Range(0, choices.Count).Select(i => choices[(trainIndex + i) % choices.Count]).ToList());
        }
        // Prune from the end so an attractive early choice cannot strand a later movement.
        for (var index = viable.Count - 2; index >= 0; index--)
            viable[index].RemoveAll(route => !viable[index + 1].Any(next => Connects(route, next)));
        if (viable.Any(choices => choices.Count == 0))
            throw new ArgumentException("作业模板的备选进路无法连续衔接，请检查接车、停留和发车进路。");
        var selected = new List<ProcessCatalogRoute>();
        foreach (var choices in viable)
            selected.Add(choices.First(route => selected.Count == 0 || Connects(selected[^1], route)));
        return selected.Select(route => route.Id).ToList();
    }

    private static bool Connects(ProcessCatalogRoute first, ProcessCatalogRoute second)
    {
        var ends = first.Type.Equals("Dwelling", StringComparison.OrdinalIgnoreCase)
            ? new[] { first.StartNodeID, first.EndNodeID } : new[] { first.EndNodeID };
        var starts = second.Type.Equals("Dwelling", StringComparison.OrdinalIgnoreCase)
            ? new[] { second.StartNodeID, second.EndNodeID } : new[] { second.StartNodeID };
        return ends.Any(node => !string.IsNullOrWhiteSpace(node) && starts.Contains(node, StringComparer.Ordinal));
    }
}
