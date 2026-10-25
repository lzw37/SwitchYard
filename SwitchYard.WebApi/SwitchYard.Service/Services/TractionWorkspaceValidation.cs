using SwitchYard.Service.Models;

namespace SwitchYard.Service.Services;

internal static class TractionWorkspaceValidation
{
    internal const long MaximumRevision = 9_007_199_254_740_990;
    internal static void Scope(TractionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ID(scope.InstanceID, 50); ID(scope.StationSchemeID, 50); ID(scope.CalculationSetID, 50); ID(scope.RouteCaseID, 50, empty: true);
    }
    internal static void Revision(long revision)
    {
        if (revision < 0 || revision > MaximumRevision) throw new ArgumentException("Invalid workspace revision.");
    }
    internal static void Text(string? value, int maximum, bool empty = false)
    {
        if (value is null || (!empty && string.IsNullOrWhiteSpace(value)) || value.EnumerateRunes().Count() > maximum || value.Any(char.IsControl))
            throw new ArgumentException("A traction text field is missing, too long or contains control characters.");
    }
    internal static void ID(string? value, int maximum = 200, bool empty = false)
    {
        Text(value, maximum, empty);
        if (value != value!.Trim()) throw new ArgumentException("Identifiers must not contain surrounding whitespace.");
    }
    internal static void Range(double value, double minimum, double maximum, string field)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum) throw new ArgumentException($"Invalid {field}.");
    }
    internal static void Workspace(TractionWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        Revision(workspace.Revision);
        Revision(workspace.CatalogRevision);
        Catalog(workspace.Catalog);
        var settings = workspace.Settings ?? throw new ArgumentException("Missing workspace settings.");
        ID(settings.SelectedRouteId, empty: true); ID(settings.ContinuationId, empty: true);
        if (settings.ContinuationId.Length > 0 && (settings.SelectedRouteId.Length == 0 || settings.SelectedRouteId == settings.ContinuationId)) throw new ArgumentException("Invalid continuation route.");
        if (settings.DistanceMode is not ("auto" or "manual")) throw new ArgumentException("Invalid distance mode.");
        Range(settings.MetersPerUnit, 0.000001, 1_000_000, "distance scale");
        Range(settings.DefaultLimit, 0.001, 1000, "default speed limit");
        Signal(settings.Signal); Options(settings.Run);
        if (workspace.SpeedLimitPoints is null || workspace.SpeedLimitPoints.Count > 1000) throw new ArgumentException("允许速度点最多 1000 个。");
        double previousLimitPosition = -1;
        foreach (var point in workspace.SpeedLimitPoints) {
            if (point is null) throw new ArgumentException("允许速度点不能为空。");
            Range(point.PositionM, 0, 10_000_000, "speed limit position"); Range(point.SpeedLimitKmh, .001, 1000, "speed limit");
            if ((previousLimitPosition < 0 && point.PositionM != 0) || point.PositionM <= previousLimitPosition) throw new ArgumentException("允许速度点必须从 0 m 开始，按里程严格递增。");
            previousLimitPosition = point.PositionM;
        }
        if (workspace.Overrides is null || workspace.Overrides.Count > 10_000) throw new ArgumentException("Too many line overrides.");
        var keys = new HashSet<(string, string, string, string, int, string)>();
        foreach (var item in workspace.Overrides)
        {
            if (item is null) throw new ArgumentException("A line override cannot be null.");
            ID(item.RouteId); ID(item.RouteStartNodeId); ID(item.RouteEndNodeId); ID(item.ContinuationId, empty: true); ID(item.SegmentId);
            if (item.RouteStartNodeId == item.RouteEndNodeId || item.ContinuationId == item.RouteId || item.SegmentIndex is < 0 or >= 10_000
                || !keys.Add((item.RouteId, item.RouteStartNodeId, item.RouteEndNodeId, item.ContinuationId, item.SegmentIndex, item.SegmentId))) throw new ArgumentException("Invalid or duplicate line override scope.");
            if (item.SpeedLimitKmh is double speed) Range(speed, 0.001, 1000, "override speed");
            if (item.GradientPermille is double gradient) Range(gradient, -300, 300, "override gradient");
            if (item.AdditionalResistanceKn is double resistance) Range(resistance, 0, 10_000_000, "override resistance");
        }
    }
    internal static void Catalog(TractionTrainPresetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Version != 1 || catalog.Presets is null || catalog.Presets.Count is < 1 or > 1000) throw new ArgumentException("Invalid train catalog version or count.");
        ID(catalog.SelectedId);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var englishNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in catalog.Presets)
        {
            if (preset is null) throw new ArgumentException("A train preset cannot be null.");
            ID(preset.Id); Text(preset.Name, 80);
            if (!ids.Add(preset.Id) || !names.Add(preset.Name.Trim())) throw new ArgumentException("Duplicate train preset ID or name.");
            if (preset.NameEn is not null) { Text(preset.NameEn, 80); if (!englishNames.Add(preset.NameEn.Trim())) throw new ArgumentException("Duplicate English train preset name."); }
            Train(preset.Parameters);
        }
        if (!ids.Contains(catalog.SelectedId)) throw new ArgumentException("Selected train preset does not exist.");
    }
    internal static void Train(SpeedProfileTrainInput train)
    {
        if (train is null) throw new ArgumentException("Missing train parameters.");
        Range(train.LengthM, .001, 100_000, "train length"); Range(train.MassT, .001, 10_000_000, "train mass");
        Range(train.RotatingMassFactor, 1, 3, "rotating mass"); Range(train.MaxSpeedKmh, .001, 1000, "maximum speed");
        Range(train.MaxBrakeForceKn, .001, 10_000_000, "brake force");
        Range(train.ResistanceA, 0, 10_000_000, "resistance A"); Range(train.ResistanceB, 0, 10_000_000, "resistance B"); Range(train.ResistanceC, 0, 10_000_000, "resistance C");
        ForceTable(train.BrakingCurve, "braking curve"); ForceTable(train.ResistanceCurve, "resistance curve");
        if (train.TractionCurve is null || train.TractionCurve.Count is < 1 or > 128) throw new ArgumentException("Invalid traction curve size.");
        double speed = -1, force = double.PositiveInfinity;
        foreach (var point in train.TractionCurve)
        {
            if (point is null) throw new ArgumentException("A traction point cannot be null.");
            Range(point.SpeedKmh, 0, 10_000, "traction speed"); Range(point.ForceKn, 0, 10_000_000, "traction force");
            if ((speed < 0 && point.SpeedKmh != 0) || point.SpeedKmh <= speed || point.ForceKn > force) throw new ArgumentException("Invalid traction curve order.");
            speed = point.SpeedKmh; force = point.ForceKn;
        }
    }
    private static void ForceTable(IReadOnlyList<SpeedProfileForceInput>? curve, string name)
    {
        if (curve is null) return; // Legacy generator-only requests.
        if (curve.Count is < 1 or > 128) throw new ArgumentException("Invalid " + name + " size.");
        double previous = -1;
        foreach (var point in curve) {
            if (point is null) throw new ArgumentException("Null " + name + " point.");
            Range(point.SpeedKmh, 0, 10000, name + " speed"); Range(point.ForceKn, 0, 10000000, name + " force");
            if ((previous < 0 && point.SpeedKmh != 0) || point.SpeedKmh <= previous) throw new ArgumentException("Invalid " + name + " speed order.");
            previous = point.SpeedKmh;
        }
    }
    internal static void Signal(SpeedProfileSignalInput signal)
    {
        if (signal is null) throw new ArgumentException("Missing signal parameters.");
        Range(signal.RouteSettingS, 0, 86400, "route setting time"); Range(signal.ReactionS, 0, 86400, "reaction time"); Range(signal.ClearanceS, 0, 86400, "clearance time");
    }
    internal static void Options(SpeedProfileOptionsInput options, double length = 10_000_000)
    {
        if (options is null) throw new ArgumentException("Missing run options.");
        if (options.Mode is not ("arrival" or "departure" or "through" or "shunting")) throw new ArgumentException("Invalid run mode.");
        Range(options.InitialSpeedKmh, 0, 1000, "initial speed"); Range(options.FinalSpeedKmh, 0, 1000, "final speed");
        Range(options.DepartureSectionLengthM, 0, 100_000, "first departure section length");
        if (options.BrakingDistanceM is double brakingDistance) Range(brakingDistance, 0, 10_000_000, "braking distance");
        if (options.BrakingMaxSpeedKmh is double brakingSpeed) Range(brakingSpeed, .001, 1000, "braking maximum speed");
        if (options.DepartureSectionMaxSpeedKmh is double departureSpeed) Range(departureSpeed, .001, 1000, "departure section maximum speed");
        if ((options.Mode == "arrival" && options.FinalSpeedKmh != 0) || (options.Mode == "departure" && options.InitialSpeedKmh != 0)) throw new ArgumentException("Invalid boundary speed for run mode.");
        Range(options.StepM, .1, 100, "distance step");
        if (options.MaxPointCount is < 2 or > SpeedProfileCalculationService.MaximumPointCount || options.Stops is null || options.Stops.Count > 1000) throw new ArgumentException("Invalid sample or stop count.");
        var positions = new HashSet<double>();
        foreach (var stop in options.Stops)
        {
            if (stop is null) throw new ArgumentException("A stop cannot be null.");
            Range(stop.PositionM, 0, length, "stop position"); Range(stop.DwellS, 0, 86400, "stop dwell");
            if (!positions.Add(stop.PositionM)) throw new ArgumentException("Duplicate stop position.");
        }
    }
    internal static void Request(SpeedProfileCalculationRequest request)
    {
        if (request is null || request.Profile is null) throw new ArgumentException("Missing calculation request.");
        Train(request.Train); Signal(request.Signal);
        var profile = request.Profile;
        Range(profile.LengthM, .001, 10_000_000, "route length"); Options(request.Options, profile.LengthM);
        if (profile.Segments is null || profile.Segments.Count is < 1 or > 10_000 || profile.Devices is null || profile.Joints is null || profile.Cells is null
            || (long)profile.Devices.Count + profile.Joints.Count + profile.Cells.Count > 20_000) throw new ArgumentException("Invalid profile collections.");
        double cursor = 0;
        foreach (var segment in profile.Segments)
        {
            if (segment is null || !double.IsFinite(segment.StartM) || !double.IsFinite(segment.EndM) || Math.Abs(segment.StartM - cursor) > 1e-7 || segment.StartM < 0 || segment.EndM <= segment.StartM || segment.EndM > profile.LengthM) throw new ArgumentException("Invalid profile coverage.");
            Range(segment.SpeedLimitKmh, .001, 1000, "segment speed"); Range(segment.GradientPermille, -300, 300, "segment gradient"); Range(segment.AdditionalResistanceKn, 0, 10_000_000, "segment resistance");
            cursor = segment.EndM;
        }
        if (cursor != profile.LengthM) throw new ArgumentException("Profile does not cover route.");
        foreach (var device in profile.Devices) { if (device is null) throw new ArgumentException("Null device."); Label(device.Id); Label(device.Label); Label(device.Type); Range(device.PositionM, 0, profile.LengthM, "device position"); }
        foreach (var joint in profile.Joints) { if (joint is null) throw new ArgumentException("Null joint."); Label(joint.Id); Label(joint.Label); Range(joint.PositionM, 0, profile.LengthM, "joint position"); }
        foreach (var cell in profile.Cells) { if (cell is null) throw new ArgumentException("Null cell."); Label(cell.Id); Label(cell.Label); Range(cell.StartM, 0, profile.LengthM, "cell start"); Range(cell.EndM, 0, profile.LengthM, "cell end"); if (cell.EndM <= cell.StartM) throw new ArgumentException("Invalid cell boundaries."); }
    }
    private static void Label(string value) => Text(value, 256, empty: true);
    internal static void Matches(TractionWorkspace workspace, SpeedProfileCalculationRequest request)
    {
        if (workspace.SpeedLimitPoints.Count > 0) {
            var points = workspace.SpeedLimitPoints; var index = 0;
            foreach (var segment in request.Profile.Segments) {
                while (index + 1 < points.Count && points[index + 1].PositionM <= segment.StartM) index++;
                if (segment.SpeedLimitKmh != points[index].SpeedLimitKmh || (index + 1 < points.Count && points[index + 1].PositionM < segment.EndM))
                    throw new ArgumentException("计算限速分段与已保存的允许速度点不一致。");
            }
        }
        var expected = TrainCharacteristicTables.Normalize(workspace.Catalog.Presets.Single(p => p.Id == workspace.Catalog.SelectedId).Parameters);
        var train = TrainCharacteristicTables.Normalize(request.Train);
        if (workspace.Settings.SelectedRouteId.Length == 0 || train.LengthM != expected.LengthM || train.MassT != expected.MassT || train.RotatingMassFactor != expected.RotatingMassFactor || train.MaxSpeedKmh != expected.MaxSpeedKmh || train.MaxBrakeForceKn != expected.MaxBrakeForceKn || train.ResistanceA != expected.ResistanceA || train.ResistanceB != expected.ResistanceB || train.ResistanceC != expected.ResistanceC
            || !train.BrakingCurve!.SequenceEqual(expected.BrakingCurve!) || !train.ResistanceCurve!.SequenceEqual(expected.ResistanceCurve!)
            || !train.TractionCurve.SequenceEqual(expected.TractionCurve) || request.Signal != workspace.Settings.Signal
            || request.Options.Mode != workspace.Settings.Run.Mode || request.Options.InitialSpeedKmh != workspace.Settings.Run.InitialSpeedKmh || request.Options.FinalSpeedKmh != workspace.Settings.Run.FinalSpeedKmh
            || request.Options.DepartureSectionLengthM != workspace.Settings.Run.DepartureSectionLengthM
            || request.Options.BrakingDistanceM != workspace.Settings.Run.BrakingDistanceM
            || request.Options.BrakingMaxSpeedKmh != workspace.Settings.Run.BrakingMaxSpeedKmh
            || request.Options.DepartureSectionMaxSpeedKmh != workspace.Settings.Run.DepartureSectionMaxSpeedKmh
            || request.Options.StepM != workspace.Settings.Run.StepM || request.Options.MaxPointCount != workspace.Settings.Run.MaxPointCount || !request.Options.Stops.SequenceEqual(workspace.Settings.Run.Stops))
            throw new ArgumentException("The calculation inputs do not match the saved workspace revision.");
    }
    internal static void Result(SpeedProfileCalculationRequest request, SpeedProfileCalculationResponse result)
    {
        var departureLength = request.Options.Mode is "departure" or "through" ? request.Options.DepartureSectionLengthM : 0;
        var expectedProfile = SpeedProfileCalculationService.AppendDepartureSection(request.Profile, departureLength,
            Math.Min(request.Train.MaxSpeedKmh, request.Options.DepartureSectionMaxSpeedKmh ?? request.Train.MaxSpeedKmh));
        if (result is null || result.Samples is null || result.JointTimes is null || result.CellOccupancies is null || result.Warnings is null || result.Samples.Count < 2 || result.Samples.Count > request.Options.MaxPointCount || result.JointTimes.Count != expectedProfile.Joints.Count || result.CellOccupancies.Count != expectedProfile.Cells.Count || result.Warnings.Count > 1000) throw new ArgumentException("Invalid calculation result collections.");
        if (result.Engine != "SwitchYard.SpeedProfile" || result.Mode != request.Options.Mode || result.DepartureSectionLengthM != departureLength || result.LengthM != expectedProfile.LengthM + result.ApproachDistanceM || result.TrainLengthM != request.Train.LengthM) throw new ArgumentException("Mismatched calculation result.");
        foreach (var value in new[] { result.DurationS, result.MotionStartS, result.TotalTimeS, result.RunningTimeS, result.DwellTimeS, result.MaxSpeedKmh }) Nonnegative(value);
        Nonnegative(result.ApproachDistanceM);
        if ((result.Mode is "departure" or "shunting") && result.ApproachDistanceM != 0) throw new ArgumentException("Unexpected braking approach for this mode.");
        if ((result.Mode is "arrival" or "through") && request.Options.BrakingDistanceM is double brakingDistance
            && result.ApproachDistanceM != brakingDistance) throw new ArgumentException("The result does not use the entered braking distance.");
        double position = -1, time = -1;
        foreach (var sample in result.Samples) {
            if (sample is null || sample.PositionM < position || sample.TimeS < time) throw new ArgumentException("Invalid result samples.");
            Range(sample.PositionM, 0, result.LengthM, "sample position"); Range(sample.SpeedKmh, 0, 1000, "sample speed"); Range(sample.LimitKmh, 0, 1000, "sample limit"); Nonnegative(sample.TimeS); position = sample.PositionM; time = sample.TimeS;
        }
        if (result.Samples[0].PositionM != 0 || result.Samples[^1].PositionM != result.LengthM || result.Samples[0].TimeS != result.MotionStartS || result.Samples[^1].TimeS != result.TotalTimeS) throw new ArgumentException("Invalid calculation endpoints.");
        if (result.Samples[0].SpeedKmh > request.Options.InitialSpeedKmh + 1e-6 || result.Samples[^1].SpeedKmh > request.Options.FinalSpeedKmh + 1e-6)
            throw new ArgumentException("Actual boundary speeds exceed the desired upper bounds.");
        for (int i = 0; i < result.JointTimes.Count; i++) {
            var item = result.JointTimes[i]; var expected = request.Profile.Joints[i];
            if (item is null || item.Id != expected.Id || item.Label != expected.Label || item.PositionM != expected.PositionM + result.ApproachDistanceM) throw new ArgumentException("Mismatched joint result.");
            Nonnegative(item.HeadTimeS); NullableTime(item.TailTimeS); NullableTime(item.ReleaseTimeS);
            if ((item.TailTimeS is null) != (item.ReleaseTimeS is null) || item.TailTimeS < item.HeadTimeS || item.ReleaseTimeS < item.TailTimeS) throw new ArgumentException("Invalid joint times.");
        }
        for (int i = 0; i < result.CellOccupancies.Count; i++) {
            var item = result.CellOccupancies[i]; var expected = expectedProfile.Cells[i];
            if (item is null || item.Id != expected.Id || item.Label != expected.Label || item.StartM != expected.StartM + result.ApproachDistanceM || item.EndM != expected.EndM + result.ApproachDistanceM || item.EntryBoundaryKnown != expected.EntryBoundaryKnown || item.ExitBoundaryKnown != expected.ExitBoundaryKnown) throw new ArgumentException("Mismatched cell result.");
            NullableTime(item.EntryTimeS); NullableTime(item.ExitTimeS); NullableTime(item.ReleaseTimeS);
            if ((!item.EntryBoundaryKnown && item.EntryTimeS is not null) || (!item.ExitBoundaryKnown && (item.ExitTimeS is not null || item.ReleaseTimeS is not null)) || (item.ExitTimeS is null) != (item.ReleaseTimeS is null) || item.ExitTimeS < item.EntryTimeS || item.ReleaseTimeS < item.ExitTimeS) throw new ArgumentException("Invalid cell times.");
        }
        foreach (var warning in result.Warnings) Text(warning, 1000);
    }
    private static void Nonnegative(double value) { if (!double.IsFinite(value) || value < 0) throw new ArgumentException("Nonfinite or negative time/value."); }
    private static void NullableTime(double? value) { if (value is double number) Nonnegative(number); }
}
