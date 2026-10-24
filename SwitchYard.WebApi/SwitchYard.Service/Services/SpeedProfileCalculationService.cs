using SwitchYard.Service.Models;
using SwitchYard.SpeedProfile;

namespace SwitchYard.Service.Services;

/// <summary>Validates the HTTP contract, translates human units, and derives signalling times from the core result.</summary>
public sealed class SpeedProfileCalculationService
{
    private const double PositionTolerance = 1e-7;
    public const int MaximumPointCount = 100_001;

    public SpeedProfileCalculationResponse Calculate(SpeedProfileCalculationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);
        var train = TrainCharacteristicTables.Normalize(request.Train);
        var profile = request.Profile;
        var options = request.Options;
        var signal = request.Signal;
        var coreTrain = new TrainParameters
        {
            MassKg = train.MassT * 1000,
            RotatingMassFactor = train.RotatingMassFactor,
            MaxSpeedMps = train.MaxSpeedKmh / 3.6,
            MaxBrakeForceNewtons = train.MaxBrakeForceKn * 1000,
            ResistanceConstantNewtons = train.ResistanceA * 1000,
            ResistanceLinearNewtonsPerMps = train.ResistanceB * 3600,
            ResistanceQuadraticNewtonsPerMpsSquared = train.ResistanceC * 12960,
            BrakingCurve = train.BrakingCurve!.Select(p => new TractionPoint(p.SpeedKmh / 3.6, p.ForceKn * 1000)).ToArray(),
            ResistanceCurve = train.ResistanceCurve!.Select(p => new TractionPoint(p.SpeedKmh / 3.6, p.ForceKn * 1000)).ToArray(),
            TractionCurve = train.TractionCurve.Select(p => new TractionPoint(p.SpeedKmh / 3.6, p.ForceKn * 1000)).ToArray()
        };
        var calculator = new SpeedProfileCalculator();
        double approachDistance = 0;
        if (options.Mode == "arrival")
        {
            var entry = profile.Segments[0];
            approachDistance = calculator.CalculateBrakingDistance(coreTrain, new TrackSection {
                GradientPermille = entry.GradientPermille,
                AdditionalResistanceNewtons = entry.AdditionalResistanceKn * 1000
            }, options.StepM, options.MaxPointCount, cancellationToken);
            profile = PrependArrivalApproach(profile, approachDistance, train.MaxSpeedKmh);
        }
        var sections = BuildSections(profile, train, cancellationToken);
        var coreRequest = new SpeedProfileRequest
        {
            Train = coreTrain,
            Sections = sections,
            Stops = options.Stops.Select(s => new TrainStop(s.PositionM + approachDistance, s.DwellS)).ToArray(),
            InitialSpeedMps = options.InitialSpeedKmh / 3.6,
            FinalSpeedMps = options.FinalSpeedKmh / 3.6,
            DistanceStepMeters = options.StepM,
            MaxPointCount = options.MaxPointCount
        };
        // This is the only numerical speed-profile solver. No browser solver is reproduced here.
        var calculated = calculator.Calculate(coreRequest, cancellationToken);
        var motionStart = signal.RouteSettingS + signal.ReactionS;
        var samples = calculated.Points.Select(p => new SpeedProfileSample(
            p.PositionMeters, p.SpeedMps * 3.6, p.SpeedLimitMps * 3.6, p.TimeSeconds + motionStart)).ToArray();
        var joints = new List<SpeedProfileJointTime>(profile.Joints.Count);
        foreach (var joint in profile.Joints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tail = HeadTimeAt(samples, options.Mode, joint.PositionM + train.LengthM);
            joints.Add(new SpeedProfileJointTime
            {
                Id = joint.Id, Label = joint.Label, PositionM = joint.PositionM,
                HeadTimeS = HeadTimeAt(samples, options.Mode, joint.PositionM)!.Value,
                TailTimeS = tail, ReleaseTimeS = tail + signal.ClearanceS
            });
        }
        var cells = new List<SpeedProfileCellOccupancy>(profile.Cells.Count);
        foreach (var cell in profile.Cells)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exit = cell.ExitBoundaryKnown ? HeadTimeAt(samples, options.Mode, cell.EndM + train.LengthM) : null;
            cells.Add(new SpeedProfileCellOccupancy
            {
                Id = cell.Id, Label = cell.Label, StartM = cell.StartM, EndM = cell.EndM,
                EntryBoundaryKnown = cell.EntryBoundaryKnown, ExitBoundaryKnown = cell.ExitBoundaryKnown,
                EntryTimeS = cell.EntryBoundaryKnown ? HeadTimeAt(samples, options.Mode, cell.StartM) : null,
                ExitTimeS = exit, ReleaseTimeS = exit + signal.ClearanceS
            });
        }
        var warnings = new List<string>();
        if (cells.Any(c => !c.EntryBoundaryKnown || !c.ExitBoundaryKnown)) warnings.Add("cell-boundary-unknown");
        if (joints.Any(j => j.TailTimeS is null) || cells.Any(c => c.ExitBoundaryKnown && c.ExitTimeS is null))
            warnings.Add("uncleared-after-stop");
        if (options.Mode != "arrival" && samples[^1].SpeedKmh > 0 &&
            (profile.Joints.Any(j => j.PositionM + train.LengthM > profile.LengthM) ||
             profile.Cells.Any(c => c.ExitBoundaryKnown && c.EndM + train.LengthM > profile.LengthM)))
            warnings.Add("tail-extension-assumed");
        if (profile.Segments.Any(s => s.GradientPermille != 0)) warnings.Add("point-mass-gradient");
        return new SpeedProfileCalculationResponse
        {
            Samples = samples, JointTimes = joints, CellOccupancies = cells,
            LengthM = profile.LengthM, ApproachDistanceM = approachDistance, TrainLengthM = train.LengthM, Mode = options.Mode,
            DurationS = calculated.TotalTimeSeconds, MotionStartS = motionStart,
            TotalTimeS = motionStart + calculated.TotalTimeSeconds,
            RunningTimeS = calculated.RunningTimeSeconds,
            DwellTimeS = calculated.TotalTimeSeconds - calculated.RunningTimeSeconds,
            MaxSpeedKmh = calculated.MaxSpeedMps * 3.6, Warnings = warnings
        };
    }

    private static SpeedProfileRouteInput PrependArrivalApproach(
        SpeedProfileRouteInput route, double distance, double maximumSpeedKmh)
    {
        var entry = route.Segments[0];
        return route with {
            LengthM = route.LengthM + distance,
            Segments = new[] { entry with { StartM = 0, EndM = distance, SpeedLimitKmh = maximumSpeedKmh } }
                .Concat(route.Segments.Select(s => s with { StartM = s.StartM + distance, EndM = s.EndM + distance })).ToArray(),
            Devices = route.Devices.Select(d => d with { PositionM = d.PositionM + distance }).ToArray(),
            Joints = route.Joints.Select(j => j with { PositionM = j.PositionM + distance }).ToArray(),
            Cells = route.Cells.Select(c => c with { StartM = c.StartM + distance, EndM = c.EndM + distance }).ToArray()
        };
    }

    /// <summary>Keep a limit until the tail leaves its section. Grade and extra resistance use the head's section.</summary>
    private static IReadOnlyList<TrackSection> BuildSections(SpeedProfileRouteInput profile, SpeedProfileTrainInput train,
        CancellationToken cancellationToken)
    {
        var boundaries = new SortedSet<double> { 0, profile.LengthM };
        foreach (var section in profile.Segments)
        {
            boundaries.Add(section.EndM);
            if (section.EndM + train.LengthM < profile.LengthM) boundaries.Add(section.EndM + train.LengthM);
        }
        var positions = boundaries.ToArray();
        var result = new List<TrackSection>(positions.Length - 1);
        // Expired entries are discarded lazily; each section enters and leaves this heap once.
        var activeLimits = new PriorityQueue<(double End, double Limit), double>();
        int nextLimit = 0, headSection = 0;
        for (int i = 0; i < positions.Length - 1; i++)
        {
            if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            var start = positions[i];
            while (nextLimit < profile.Segments.Count && profile.Segments[nextLimit].StartM <= start + PositionTolerance)
            {
                var segment = profile.Segments[nextLimit++];
                activeLimits.Enqueue((segment.EndM + train.LengthM, segment.SpeedLimitKmh), segment.SpeedLimitKmh);
            }
            while (activeLimits.TryPeek(out var item, out _) && item.End <= start) activeLimits.Dequeue();
            while (headSection + 1 < profile.Segments.Count && profile.Segments[headSection].EndM <= start)
                headSection++;
            var head = profile.Segments[headSection];
            result.Add(new TrackSection
            {
                LengthMeters = positions[i + 1] - start,
                SpeedLimitMps = Math.Min(train.MaxSpeedKmh, activeLimits.Peek().Limit) / 3.6,
                GradientPermille = head.GradientPermille,
                AdditionalResistanceNewtons = head.AdditionalResistanceKn * 1000
            });
        }
        return result;
    }

    /// <summary>
    /// First arrival at an exact stop; intervals after a stop start after its dwell point.
    /// Outside the route a moving departure/through train continues at terminal speed.
    /// </summary>
    private static double? HeadTimeAt(IReadOnlyList<SpeedProfileSample> samples, string mode, double position)
    {
        var last = samples[^1];
        if (position > last.PositionM + PositionTolerance)
            return mode != "arrival" && last.SpeedKmh > 0
                ? last.TimeS + (position - last.PositionM) / (last.SpeedKmh / 3.6) : null;
        int low = 0, high = samples.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (samples[middle].PositionM < position - PositionTolerance) low = middle + 1;
            else high = middle;
        }
        if (low >= samples.Count) return last.TimeS;
        var to = samples[low];
        if (Math.Abs(to.PositionM - position) <= PositionTolerance || low == 0) return to.TimeS;
        var from = samples[low - 1];
        double distance = position - from.PositionM;
        double startSpeed = from.SpeedKmh / 3.6, endSpeed = to.SpeedKmh / 3.6;
        double acceleration = (endSpeed * endSpeed - startSpeed * startSpeed) / (2 * (to.PositionM - from.PositionM));
        double speed = Math.Sqrt(Math.Max(0, startSpeed * startSpeed + 2 * acceleration * distance));
        return startSpeed + speed > 0 ? from.TimeS + 2 * distance / (startSpeed + speed) : null;
    }

    private static void Validate(SpeedProfileCalculationRequest request)
    {
        if (request is null || request.Profile is null || request.Train is null || request.Signal is null || request.Options is null)
            Invalid("invalid-input", "必须提供 profile、train、signal 和 options 对象。");
        var profile = request!.Profile;
        var train = request.Train;
        var signal = request.Signal;
        var options = request.Options;
        Range(train.LengthM, "列车长度 (m)", 0.001, 100_000);
        Range(train.MassT, "列车质量 (t)", 0.001, 10_000_000);
        Range(train.RotatingMassFactor, "旋转质量系数", 1, 3);
        Range(train.MaxSpeedKmh, "列车最高速度 (km/h)", 0.001, 1000);
        Range(train.MaxBrakeForceKn, "最大常用制动力 (kN)", 0.001, 10_000_000);
        Range(train.ResistanceA, "Davis 阻力 A", 0, 10_000_000);
        Range(train.ResistanceB, "Davis 阻力 B", 0, 10_000_000);
        Range(train.ResistanceC, "Davis 阻力 C", 0, 10_000_000);
        Range(signal.RouteSettingS, "进路办理时间 (s)", 0, 86_400);
        Range(signal.ReactionS, "反应时间 (s)", 0, 86_400);
        Range(signal.ClearanceS, "出清时间 (s)", 0, 86_400);
        Range(options.InitialSpeedKmh, "初速度 (km/h)", 0, 1000);
        Range(options.FinalSpeedKmh, "末速度 (km/h)", 0, 1000);
        Range(options.StepM, "计算步长 (m)", 0.1, 100);
        if (options.Mode is not ("arrival" or "departure" or "through"))
            Invalid("invalid-input", "运行方式必须为 arrival、departure 或 through。");
        if (options.Mode == "departure" && options.InitialSpeedKmh != 0)
            Invalid("invalid-input", "出发模式的初速度必须为 0 km/h。");
        if (options.Mode == "arrival" && options.FinalSpeedKmh != 0)
            Invalid("invalid-input", "到达模式的末速度必须为 0 km/h。");
        if (options.MaxPointCount is < 2 or > MaximumPointCount)
            Invalid("invalid-input", $"maxPointCount 必须在 2 至 {MaximumPointCount} 之间（包含停站重复点）。");
        if (train.TractionCurve is null || train.TractionCurve.Count is < 1 or > 128)
            Invalid("invalid-curve", "牵引特性表需要 1 至 128 个点。");
        double previousSpeed = -1, previousForce = double.PositiveInfinity;
        foreach (var point in train.TractionCurve!)
        {
            if (point is null || !double.IsFinite(point.SpeedKmh) || !double.IsFinite(point.ForceKn) ||
                point.SpeedKmh < 0 || point.SpeedKmh > 10_000 || point.ForceKn < 0 || point.ForceKn > 10_000_000 ||
                point.SpeedKmh <= previousSpeed || point.ForceKn > previousForce || (previousSpeed < 0 && point.SpeedKmh != 0))
                Invalid("invalid-curve", "牵引特性须从 0 km/h 开始，速度严格递增、牵引力非递增，所有数值须为非负有限数。");
            previousSpeed = point!.SpeedKmh;
            previousForce = point.ForceKn;
        }
        foreach (var (curve, name) in new[] { (train.BrakingCurve, "制动"), (train.ResistanceCurve, "阻力") }) {
            if (curve is null) continue;
            if (curve.Count is < 1 or > 128) Invalid("invalid-curve", name + "特性表须包含 1 至 128 个数据点。");
            double speed = -1;
            foreach (var point in curve) {
                if (point is null || !double.IsFinite(point.SpeedKmh) || point.SpeedKmh < 0 || point.SpeedKmh > 10000 || !double.IsFinite(point.ForceKn) || point.ForceKn < 0 || point.ForceKn > 10000000 || (speed < 0 && point.SpeedKmh != 0) || point.SpeedKmh <= speed)
                    Invalid("invalid-curve", name + "特性须从 0 km/h 开始，速度严格递增，力为非负有限数。");
                speed = point!.SpeedKmh;
            }
        }
        Range(profile.LengthM, "进路长度 (m)", 0.001, 10_000_000, "invalid-route");
        if (profile.Segments is null || profile.Segments.Count is < 1 or > 10_000)
            Invalid("invalid-route", "进路须包含 1 至 10000 个连续线路区段。");
        if (profile.Joints is null || profile.Devices is null || profile.Cells is null ||
            (long)profile.Joints.Count + profile.Devices.Count + profile.Cells.Count > 20_000)
            Invalid("invalid-route", "joints、devices、cells 必须为数组，且总数不得超过 20000。");
        double cursor = 0;
        foreach (var segment in profile.Segments!)
        {
            if (segment is null || !double.IsFinite(segment.StartM) || !double.IsFinite(segment.EndM) ||
                segment.StartM < 0 || Math.Abs(segment.StartM - cursor) > PositionTolerance ||
                segment.EndM <= segment.StartM || segment.EndM <= cursor || segment.EndM > profile.LengthM)
                Invalid("invalid-route", "线路区段须按位置连续覆盖全进路，起点为 0，末端等于进路总长。");
            Range(segment!.SpeedLimitKmh, "区段限速 (km/h)", 0.001, 1000, "invalid-route");
            Range(segment.GradientPermille, "区段坡度 (‰)", -300, 300, "invalid-route");
            Range(segment.AdditionalResistanceKn, "附加阻力 (kN)", 0, 10_000_000, "invalid-route");
            cursor = segment.EndM;
        }
        // Require the same terminal coordinate so core endpoint stops cannot fall beyond a rounded route.
        if (cursor != profile.LengthM) Invalid("invalid-route", "线路区段末端与进路总长不一致。");
        foreach (var joint in profile.Joints!)
        {
            if (joint is null) Invalid("invalid-route", "钢轨绝缘不能为 null。");
            Label(joint!.Id); Label(joint.Label);
            Range(joint.PositionM, "钢轨绝缘位置 (m)", 0, profile.LengthM, "invalid-route");
        }
        foreach (var device in profile.Devices!)
        {
            if (device is null) Invalid("invalid-route", "设备不能为 null。");
            Label(device!.Id); Label(device.Label); Label(device.Type);
            Range(device.PositionM, "设备位置 (m)", 0, profile.LengthM, "invalid-route");
        }
        foreach (var cell in profile.Cells!)
        {
            if (cell is null) Invalid("invalid-route", "轨道电路不能为 null。");
            Label(cell!.Id); Label(cell.Label);
            Range(cell.StartM, "轨道电路起点 (m)", 0, profile.LengthM, "invalid-route");
            Range(cell.EndM, "轨道电路终点 (m)", 0, profile.LengthM, "invalid-route");
            if (cell.EndM <= cell.StartM) Invalid("invalid-route", "轨道电路终点必须大于起点。");
        }
        if (options.Stops is null || options.Stops.Count > 1000)
            Invalid("invalid-input", "stops 必须为数组，最多支持 1000 个停车点。");
        var stopPositions = new HashSet<double>();
        foreach (var stop in options.Stops!)
        {
            if (stop is null) Invalid("invalid-input", "停车点不能为 null。");
            Range(stop!.PositionM, "停车位置 (m)", 0, profile.LengthM);
            Range(stop.DwellS, "停站时间 (s)", 0, 86_400);
            if (!stopPositions.Add(stop.PositionM)) Invalid("invalid-input", "停车位置不可重复。");
            if ((stop.PositionM == 0 && options.InitialSpeedKmh != 0) ||
                (stop.PositionM == profile.LengthM && options.FinalSpeedKmh != 0))
                Invalid("invalid-input", "起终点停车要求对应边界速度为 0 km/h。");
        }
    }
    private static void Range(double value, string label, double minimum, double maximum, string code = "invalid-input")
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            Invalid(code, $"{label}必须是 {minimum} 至 {maximum} 范围内的有限数值。");
    }
    private static void Label(string value)
    {
        if (value is null || value.Length > 256) Invalid("invalid-route", "设备、绝缘和轨道电路的标识、名称、类型须为不超过 256 字符的字符串。");
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid(string code, string message) => throw new SpeedProfileInputException(code, message);
}

public sealed class SpeedProfileInputException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}