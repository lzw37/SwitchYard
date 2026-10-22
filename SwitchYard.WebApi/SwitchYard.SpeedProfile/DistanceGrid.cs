namespace SwitchYard.SpeedProfile;

internal sealed record DistanceGrid(
    double[] Positions, double[] SpeedLimits, int[] CellSections, bool[] StopNodes, double[] DwellSeconds)
{
    internal static DistanceGrid Build(ValidatedInput input, SpeedProfileRequest request, CancellationToken cancellationToken)
    {
        var positions = new List<double> { 0 };
        var limits = new List<double> { Math.Min(input.Train.MaxSpeedMps, input.Sections[0].SpeedLimitMps) };
        var cellSections = new List<int>();
        var stopNodes = new List<bool> { false };
        var dwell = new List<double> { 0 };
        int stopIndex = 0;
        int dwellPointCount = input.Stops.Count(s => s.DwellSeconds > 0);
        if (input.Stops.Length > 0 && input.Stops[0].PositionMeters == 0)
        {
            stopNodes[0] = true;
            dwell[0] = input.Stops[0].DwellSeconds;
            stopIndex++;
        }

        double sectionStart = 0;
        for (int sectionIndex = 0; sectionIndex < input.Sections.Length; sectionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var section = input.Sections[sectionIndex];
            double end = sectionStart + section.LengthMeters;
            double cursor = sectionStart;
            double limit = Math.Min(input.Train.MaxSpeedMps, section.SpeedLimitMps);
            // Both adjacent section limits apply at a discontinuity.
            limits[^1] = Math.Min(limits[^1], limit);
            while (cursor < end)
            {
                bool endsAtStop = stopIndex < input.Stops.Length && input.Stops[stopIndex].PositionMeters <= end;
                double target = endsAtStop ? input.Stops[stopIndex].PositionMeters : end;
                double span = target - cursor;
                double countDouble = Math.Max(2, Math.Ceiling(span / request.DistanceStepMeters));
                if (!double.IsFinite(countDouble) ||
                    countDouble > request.MaxPointCount - positions.Count - dwellPointCount)
                    throw new ArgumentException("The distance grid exceeds MaxPointCount. Increase DistanceStepMeters or MaxPointCount.");
                int count = (int)countDouble;
                for (int step = 1; step <= count; step++)
                {
                    if ((step & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double position = step == count ? target : cursor + span * ((double)step / count);
                    if (position <= positions[^1])
                        throw new ArgumentException("The distance grid cannot resolve these section/stop positions.");
                    positions.Add(position);
                    limits.Add(limit);
                    cellSections.Add(sectionIndex);
                    stopNodes.Add(false);
                    dwell.Add(0);
                }
                if (endsAtStop)
                {
                    stopNodes[^1] = true;
                    dwell[^1] = input.Stops[stopIndex].DwellSeconds;
                    stopIndex++;
                }
                cursor = target;
            }
            sectionStart = end;
        }
        return new(positions.ToArray(), limits.ToArray(), cellSections.ToArray(), stopNodes.ToArray(), dwell.ToArray());
    }
}
