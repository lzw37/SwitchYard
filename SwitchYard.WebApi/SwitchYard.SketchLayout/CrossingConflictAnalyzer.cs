namespace SwitchYard.SketchLayout;

// Unregulated planned occupancies, before queuing or priority rules change arrival times.
// The reported probability is for a randomly selected passage at this saved crossing.
public static class CrossingConflictAnalyzer
{
    private sealed class Traffic
    {
        public long Count;
        public double WeightedSeconds;
        public HashSet<(string PlanId, string ChainId)> Chains = [];
        public bool RepeatedVisits;
    }

    public static List<CrossingRisk> Analyze(SketchLayoutScheme document)
    {
        var settings = document.ResolveAnalysisSettings();
        var links = document.Links.ToDictionary(l => l.Id);
        var traffic = new Dictionary<string, Traffic>();
        foreach (var plan in document.Plans) foreach (var chain in plan.Chains)
            foreach (var step in chain.Steps.Where(s => s.IncomingLinkId != null))
            {
                var id = step.IncomingLinkId!;
                if (!traffic.TryGetValue(id, out var t)) traffic[id] = t = new();
                t.Count += plan.Quantity;
                t.WeightedSeconds += plan.Quantity * step.TravelSeconds;
                if (!t.Chains.Add((plan.Id, chain.Id))) t.RepeatedVisits = true;
            }

        var result = new List<CrossingRisk>();
        var totalBudget = 5_000_000;
        var queueEventBudget = 8_000_000;
        foreach (var crossing in document.Crossings)
        {
            var seenChains = new HashSet<(string, string)>();
            var correlated = false;
            var routes = crossing.SourceLinks.Select(source =>
            {
                var t = traffic.GetValueOrDefault(source.LinkId);
                if (t != null)
                {
                    correlated |= t.RepeatedVisits || t.Chains.Any(c => !seenChains.Add(c));
                    seenChains.UnionWith(t.Chains);
                }
                var count = t?.Count ?? 0;
                return new CrossingRouteRisk(source.LinkId,
                    links.GetValueOrDefault(source.LinkId)?.Name ?? source.Name,
                    !links.ContainsKey(source.LinkId), count, count * 3600d / settings.HorizonSeconds,
                    source.OccupancySeconds ?? (count > 0 ? t!.WeightedSeconds / count : null),
                    source.OccupancySeconds == null, null);
            }).ToList();
            var total = routes.Sum(r => r.Passages);
            var status = routes.Count < 2 ? "missing-sources" : routes.Any(r => r.MissingLink) ? "missing-links" : total == 0 ? "no-traffic" : "ready";
            double? probability = null;
            if (status == "ready")
            {
                // Every recorded source route at a marker is treated as mutually conflicting.
                var initialBudget = Math.Min(1_000_000, totalBudget);
                var budget = initialBudget;
                try
                {
                    for (var i = 0; i < routes.Count; i++)
                    {
                        var current = routes[i];
                        if (current.Passages == 0) continue;
                        var others = routes.Where((r, j) => j != i && r.Passages > 0).ToList();
                        var p = settings.CrossingModel == "uniform-fixed"
                            ? UniformConditional(current.OccupancySeconds!.Value, others.Select(r => (r.Passages, r.OccupancySeconds!.Value)).ToList(), settings.HorizonSeconds, ref budget)
                            : PoissonConditional(current.OccupancySeconds!.Value, others.Select(r => (r.Passages / settings.HorizonSeconds, r.OccupancySeconds!.Value)), settings.CrossingModel == "poisson-exponential");
                        routes[i] = current with { ConflictProbability = p };
                    }
                    probability = Math.Clamp(routes.Sum(r => r.Passages / (double)total * (r.ConflictProbability ?? 0)), 0, 1);
                }
                catch (CalculationLimitException)
                {
                    status = "calculation-limit";
                    routes = routes.Select(r => r with { ConflictProbability = null }).ToList();
                }
                finally { totalBudget -= initialBudget - Math.Max(0, budget); }
            }
            result.Add(new(crossing.Id, crossing.Name, settings.CrossingModel, status, total, probability, routes, correlated)
            {
                Queue = CrossingQueueAnalyzer.Analyze(routes, settings.CrossingModel, settings.HorizonSeconds, ref queueEventBudget)
            });
        }
        return result;
    }

    public static double PoissonConditional(double ownMean, IEnumerable<(double Rate, double Mean)> others, bool exponential)
    {
        var arrivals = 0d; var priorOccupancy = 0d;
        foreach (var (rate, mean) in others) { arrivals += rate; priorOccupancy += rate * mean; }
        // Past active jobs: P(empty) = exp(-sum(lambda_j E[S_j])) in stationary M/G/infinity.
        // Future starts during S_i: E[exp(-sum(lambda_j) S_i)]. Independent service marks
        // cannot be replaced with one shared random S_j for the entire opposing stream.
        var logNoConflict = -priorOccupancy - (exponential ? Math.Log(1 + arrivals * ownMean) : arrivals * ownMean);
        return Math.Clamp(1 - Math.Exp(logNoConflict), 0, 1);
    }

    private sealed class CalculationLimitException : Exception { }
    public static double UniformConditional(double ownDuration, IReadOnlyList<(long Count, double Duration)> others, double horizon)
    {
        var budget = 1_000_000;
        return UniformConditional(ownDuration, others, horizon, ref budget);
    }
    private static double UniformConditional(double ownDuration, IReadOnlyList<(long Count, double Duration)> others, double horizon, ref int budget)
    {
        if (others.Count == 0) return 0;
        var remaining = budget;
        var own = Math.Min(1, ownDuration / horizon);
        var targets = others.Select(r => (r.Count, Duration: Math.Min(1, r.Duration / horizon))).ToList();
        // Condition on the selected passage start u before multiplying independent misses.
        // Averaging a pair probability before raising it to Count is not valid at boundaries.
        double NoConflict(double u)
        {
            remaining -= targets.Count;
            if (remaining < 0) throw new CalculationLimitException();
            var log = 0d;
            foreach (var target in targets)
            {
                var window = Math.Clamp(Math.Min(1, u + own) - Math.Max(0, u - target.Duration), 0, 1);
                if (window >= 1) return 0;
                log += target.Count * Math.Log(1 - window);
            }
            return Math.Exp(log);
        }
        var boundaries = targets.Select(t => t.Duration).Append(0).Append(1).Append(1 - own).Distinct().Order().ToArray();
        var integral = 0d;
        try
        {
            for (var i = 1; i < boundaries.Length; i++)
            {
                var left = boundaries[i - 1]; var right = boundaries[i]; var mid = (left + right) / 2;
                var fl = NoConflict(left); var fm = NoConflict(mid); var fr = NoConflict(right);
                var whole = (right - left) * (fl + 4 * fm + fr) / 6;
                integral += Integrate(NoConflict, left, right, fl, fm, fr, whole, 1e-9 * (right - left), 30);
            }
        }
        finally { budget = remaining; }
        return Math.Clamp(1 - integral, 0, 1);
    }
    private static double Integrate(Func<double, double> f, double a, double b, double fa, double fm, double fb, double whole, double tolerance, int depth)
    {
        var m = (a + b) / 2; var fl = f((a + m) / 2); var fr = f((m + b) / 2);
        var left = (m - a) * (fa + 4 * fl + fm) / 6;
        var right = (b - m) * (fm + 4 * fr + fb) / 6;
        var difference = left + right - whole;
        if (Math.Abs(difference) <= 15 * tolerance) return left + right + difference / 15;
        if (depth == 0) throw new CalculationLimitException();
        return Integrate(f, a, m, fa, fl, fm, left, tolerance / 2, depth - 1) + Integrate(f, m, b, fm, fr, fb, right, tolerance / 2, depth - 1);
    }
}
