namespace SwitchYard.SketchLayout;

// One exclusive crossing resource, FCFS, unlimited waiting room. Includes same-route
// queueing; the separate conflict probability only measures cross-route overlap.
public static class CrossingQueueAnalyzer
{
    private readonly record struct Arrival(double Time, double Service);

    public static CrossingQueueMetrics Analyze(IReadOnlyList<CrossingRouteRisk> routes, string distribution, double horizon, ref int eventBudget)
    {
        var model = distribution == "uniform-fixed" ? "finite-uniform-fcfs" : "mg1-fcfs";
        if (routes.Count < 2) return new(model, "missing-sources", null, null, null);
        if (routes.Any(r => r.MissingLink)) return new(model, "missing-links", null, null, null);
        var active = routes.Where(r => r.Passages > 0).OrderBy(r => r.LinkId, StringComparer.Ordinal).ToList();
        var count = active.Sum(r => r.Passages);
        if (count == 0) return new(model, "no-traffic", 0, 0, 0);
        var load = active.Sum(r => r.Passages / horizon * r.OccupancySeconds!.Value);
        if (distribution != "uniform-fixed")
        {
            if (load >= 1) return new(model, "unstable", load, null, null);
            // P-K: Wq = sum(lambda_i E[S_i^2]) / (2(1-rho)). Route means may
            // differ, so the combined service law is a mixture, not one exponential.
            var secondMomentRate = active.Sum(r => r.Passages / horizon * Math.Pow(r.OccupancySeconds!.Value, 2))
                * (distribution == "poisson-exponential" ? 2 : 1);
            var delay = secondMomentRate / (2 * (1 - load));
            return new(model, "ready", load, delay, count / horizon * delay);
        }

        // Fixed N independent U[0,H] arrivals with an empty queue at time zero.
        // Delay follows every arrival until service starts, even after H. Lq is
        // the time-average WAITING queue over [0,H], excluding the job in service.
        if (count == 1) return new(model, "ready", load, 0, 0);
        var runs = (int)Math.Min(2048, Math.Min(2_000_000, eventBudget) / count);
        if (count > 20_000 || runs < 64) return new(model, "calculation-limit", load, null, null);
        eventBudget -= (int)count * runs;
        var random = new Random(20261009);
        var arrivals = new Arrival[(int)count];
        var meanDelay = 0d; var delayM2 = 0d; var meanQueue = 0d; var queueM2 = 0d;
        for (var run = 1; run <= runs; run++)
        {
            var index = 0;
            foreach (var route in active)
                for (var n = 0L; n < route.Passages; n++)
                    arrivals[index++] = new(random.NextDouble() * horizon, route.OccupancySeconds!.Value);
            Array.Sort(arrivals, (a, b) => a.Time.CompareTo(b.Time));
            var available = 0d; var totalDelay = 0d; var queueArea = 0d;
            foreach (var arrival in arrivals)
            {
                var start = Math.Max(available, arrival.Time);
                totalDelay += start - arrival.Time;
                queueArea += Math.Max(0, Math.Min(start, horizon) - arrival.Time);
                available = start + arrival.Service;
            }
            var delay = totalDelay / count; var queue = queueArea / horizon;
            var delayDelta = delay - meanDelay; meanDelay += delayDelta / run;
            delayM2 += delayDelta * (delay - meanDelay);
            var queueDelta = queue - meanQueue; meanQueue += queueDelta / run;
            queueM2 += queueDelta * (queue - meanQueue);
        }
        return new(model, "ready", load, meanDelay, meanQueue, runs,
            Math.Sqrt(Math.Max(0, delayM2) / (runs - 1) / runs),
            Math.Sqrt(Math.Max(0, queueM2) / (runs - 1) / runs));
    }
}
