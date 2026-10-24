# SwitchYard.SpeedProfile

独立的 .NET 8 列车牵引/制动速度曲线计算库，无第三方运行时依赖。已加入后端解决方案，并由 SwitchYard.Service 引用。类库调用入口为 `SpeedProfileCalculator.Calculate`。后端 Service 另提供带 JWT 认证的 `POST /api/SpeedProfile/calculate`，前端牵引模块已通过该接口计算；契约和单位转换见 [API.md](API.md)。

本组件选择性移植 [TrainRuns.jl v1.0.4](https://github.com/railtoolkit/TrainRuns.jl/tree/529cdd1d2a158077440ce4e3093a8b875a1191fb) 的反向制动递推、牵引力插值与运动学关系，并实现 C# 距离网格前向求解。不是完整 Julia 包的移植；候选项目、固定版本、算法边界见 [RESEARCH.md](RESEARCH.md)。

## 调用示例

```csharp
using SwitchYard.SpeedProfile;

var result = new SpeedProfileCalculator().Calculate(new SpeedProfileRequest
{
    Train = new TrainParameters
    {
        MassKg = 400_000,
        RotatingMassFactor = 1.08,
        MaxSpeedMps = 160 / 3.6,
        MaxBrakeForceNewtons = 320_000,
        ResistanceConstantNewtons = 5_000,
        ResistanceLinearNewtonsPerMps = 100,
        ResistanceQuadraticNewtonsPerMpsSquared = 8,
        TractionCurve =
        [
            new(0, 300_000),
            new(20, 300_000),
            new(160 / 3.6, 150_000)
        ]
    },
    Sections =
    [
        new() { LengthMeters = 3_000, SpeedLimitMps = 120 / 3.6, GradientPermille = 8 },
        new() { LengthMeters = 2_000, SpeedLimitMps = 80 / 3.6, GradientPermille = -5 },
        new() { LengthMeters = 5_000, SpeedLimitMps = 160 / 3.6 }
    ],
    Stops = [new(5_000, 60)],
    InitialSpeedMps = 0,
    FinalSpeedMps = 0,
    DistanceStepMeters = 5
});

Console.WriteLine($"运行 {result.RunningTimeSeconds:F2} 秒，含停站 {result.TotalTimeSeconds:F2} 秒");
foreach (var point in result.Points)
    Console.WriteLine($"{point.PositionMeters:F1} m, {point.SpeedMps * 3.6:F2} km/h, {point.TimeSeconds:F2} s");
```

计算器无可变共享状态，可复用并并发调用。支持传入 `CancellationToken`；调用过程中不要修改请求的集合。

## 输入与输出约定

- 长度 m、速度 m/s、时间 s、质量 kg、力 N；km/h 输入请先除以 3.6。
- 线路区段按行进顺序连续排列，坡度单位 ‰，正值上坡。采用铁路小坡度近似：坡阻为 `mass × 9.80665 × gradient / 1000`。
- 总阻力为 `基本阻力表插值 + 附加阻力 + 坡阻`。旧调用未提供阻力表时，先按 Davis 公式每 10 km/h 生成到最高速度，再使用离散表；内核 Davis 系数以 m/s 标定，不能直接填入 km/h 系数。
- 惯性质量为 `MassKg × RotatingMassFactor`，旋转质量系数至少为 1；重力仍使用静质量。
- 牵引力表从零速开始，速度严格递增、力非负且非递增；支持只有一个恒力点。区间线性插值，超过表尾沿用末项力。
- 制动与基本阻力表同样从零速开始，速度严格递增，力可升可降且非负，分段线性插值，超过表尾沿用末点力。已有表优先于标量生成参数；缺失制动表的旧调用按恒定制动力生成。净制动计入阻力、坡度及回转质量，并要求各段全运行速度范围内净制动能力为正。
- 初、末速度是必须满足的边界值。中间停车用 `Stops` 表达，不用长度大于零的零限速区段表达。停车位置唯一，可无序输入，也可在起终点；起终点有停车时对应边界速度必须为零。
- `Points` 包含里程、累计时间、速度、当地限速、加速度、工况。加速度和工况描述“到达该点”的前一段；第一点为 `Initial`。停时以同里程的第二个零速点表示，模式为 `Dwell`。
- `SpeedLimitMps` 是线路与车辆上限的较小值，不是反向制动包络。全程时间包含停时；运行时间不含停时。峰速、总距离也随结果返回。
- `MaxPointCount` 默认 200,001，允许范围 2..1,000,001，包含停时产生的额外点；超过预算会拒绝计算。

## 算法和精度

1. 把所有区段及停车边界准确加入距离网格，并按指定最大步长细分。相邻事件间至少两格，因此极短的起停区间仍能表达先加速后制动。
2. 从终点和停车点向前传播制动速度上限，在降限速位置之前完成减速。
3. 按牵引曲线、阻力、坡度正向求可达速度，同时遵守反向上限。牵引和制动均检查每格速度范围内的端点与曲线拐点，使用最小可用力；用最多 40 次有界二分求解。
4. 验证每格的恒加速度都位于整个速度范围内的牵引和制动能力之间，按 `2 × distance / (v0 + v1)` 累加时间。

这是保守的离散最大牵引运行近似。减小步长一般提高精度和耗时；建议用 5 m 起步，再用 1 m 或更细网格对关键线路做收敛比较。恒力平坡案例有解析测试，但不保证任意车辆与线路达到统一误差。

若有 N 个网格点、K 个牵引采样点、S 个停车点，K 表示三类表合并节点数，固定二分迭代数下求解最坏为 O(N K)，通常每格只跨越少量节点，停车排序另需 O(S log S)，内存为 O(N + K + S + 区段数)。K 固定时求解随网格数量线性增长。

本版采用单质点列车；未建模车长造成的解除限速延迟和跨坡段质量分布、黏着变化、制动建立/传播时间、冲动约束、再生制动、信号系统或节能惰行优化。工况标签为每格代表工况，恒速格统一标为 `Cruising`，其保持速度可能需要牵引或制动。本版不计算能耗，也不用于列控安全制动曲线。

## 错误处理

非法参数、非有限数、错误牵引表、重复停车点或网格超预算抛 `ArgumentException`（包括其子类）；取消抛 `OperationCanceledException`。

`InfeasibleProfileException` 表示该模型与当前网格无法满足请求，例如起速无法满足下游限制、终速无法达到、无法起动。它也用于保守模型不支持的下坡条件，或步长过大导致的失速/力约束检查失败；不能把此异常一概解释为连续真实列车物理上不可行。网格相关失败应先缩小 `DistanceStepMeters` 复算，仍失败时检查车辆及线路参数。

## 验证与许可

从仓库根目录运行：

```powershell
dotnet build SwitchYard.WebApi/SwitchYard.SpeedProfile/SwitchYard.SpeedProfile.csproj -c Release
dotnet run --project SwitchYard.WebApi/SwitchYard.SpeedProfile.Tests/SwitchYard.SpeedProfile.Tests.csproj -c Release
```

测试不依赖 NuGet 测试框架，失败会返回非零退出码；覆盖解析解、边界、整格力学约束、停站、非法输入和步长收敛，并报告 100 km 线路性能。性能是本机测量，不是与其他项目的对比结论。


本机基准记录（2026-10-23）：Intel Core Ultra 7 255H，16 个逻辑处理器，Windows X64，.NET 8.0.25，Release；每种工况预热 3 次、测量 10 次，取中位数。100 km、5 m 步长、3 个中间停车点（每站停 30 s）均产生 20,004 个输出点。

| 工况 | 计算中位耗时 |
| --- | ---: |
| 恒定牵引、平坡、无 Davis 阻力 | 1.755 ms |
| 分段牵引曲线、Davis 阻力及分段坡度 | 10.221 ms |

测量仅覆盖 `Calculate` 调用，不含进程启动、网络或 JSON；具体输入保存在测试项目中。25 组测试通过，包括 1 m 网格下恒力三角/梯形解析结果相对误差不超过 0.1%，以及速度相关牵引的网格收敛检查；这些是测试场景结果，不是任意工况的误差保证。

移植部分保留 ISC 许可与原作者版权，新增代码适用仓库 MIT 许可。完整来源及 ISC 原文见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)。构建和发布会把声明复制到 `licenses/SwitchYard.SpeedProfile/`，NuGet 包也包含许可文本；单独复制 DLL 时应一并保留对应许可文件。
