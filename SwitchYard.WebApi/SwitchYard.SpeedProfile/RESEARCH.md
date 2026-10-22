# 列车速度曲线算法来源与选型

核查日期：2026-10-23。核查对象为 GitHub 官方仓库的源码、固定提交和许可证，不以 README 中的功能描述代替源码确认。以下比较针对已给定线路、车辆和限制条件的单列车运行计算。

## 已核查候选

| 项目与固定版本 | 已核查实现 | 许可证及移植成本 | 本次结论 |
| --- | --- | --- | --- |
| [TrainRuns.jl v1.0.4](https://github.com/railtoolkit/TrainRuns.jl/tree/529cdd1d2a158077440ce4e3093a8b875a1191fb)；提交 `529cdd1d2a158077440ce4e3093a8b875a1191fb` | `secureBrakingBehavior!` 倒序传播可制动的区间入口速度；`calculateTractiveEffort` 对牵引力表作分段线性插值；`formulary.jl` 提供力、加速度及恒加速度运动学函数。 | ISC，保留版权和许可声明即可按其条款使用、修改和分发。核心关系独立于 Julia 数据加载层，适合小范围迁移。 | 选用其制动约束递推、牵引插值及力学公式，构建 C# 计算组件。 |
| [OSRD](https://github.com/OpenRailAssociation/osrd/tree/283a61b691d50a67ad0fed4bbe1ffea5d13dd192)；提交 `283a61b691d50a67ad0fed4bbe1ffea5d13dd192` | `MaxSpeedEnvelope.kt` 从降速目标和停车点反向生成制动包络，并通过约束包络合成；该管线还包含 ETCS 分支。 | `REUSE.toml` 标注相关源码为 LGPL-3.0-only。复制或翻译实现需保留适用 LGPL/GPL 文本与版权；分发修改库需满足相应源码义务，组合分发还涉及用户修改库和重新链接的条件。 | 功能更完整，但包络、路径和 ETCS 依赖范围较大，本次未复制代码。不能用“引用算法”代替复制源码时的许可义务。 |
| [ALTRIOS](https://github.com/NREL/altrios/tree/bc4a8e6ca54c9fcc5cee29a4034598629794a98d)；提交 `bc4a8e6ca54c9fcc5cee29a4034598629794a98d` | `BrakingPoints.recalc` 根据制动力和列车阻力逐步反向计算速度约束；`calc_speeds` 利用制动建立时间进行前瞻；`SpeedLimitTrainSim` 连接车辆、线路和动力系统状态。 | LICENSE.md 为 BSD-3-Clause：源码保留版权、条件及免责声明，二进制分发在文档或其他材料中复现；不得未经许可使用权利人或贡献者名称背书。 | 宽松许可且物理模型更丰富，可作后续货运及动力系统扩展候选；依赖 TrainState、TrainRes、PathTpc 和单位类型等，本次未移植。 |

TrainRuns.jl 核查时的主分支 HEAD 为 `7ca94cb81230c09153102532ba32225785a9ea42`；本次有意采用固定发布版 v1.0.4，避免依赖浮动主分支。v1.0.4 是附注标签，其标签对象不是源码提交；已通过 peeled tag 核实上表提交。

## 移植边界和算法

[behavior.jl](https://github.com/railtoolkit/TrainRuns.jl/blob/529cdd1d2a158077440ce4e3093a8b875a1191fb/src/behavior.jl) 的 `secureBrakingBehavior!` 从末段开始，先限制区间出口速度，再由制动距离关系求入口速度，最后取与本段限速的较小值。该顺序使下游限制向上游传播，避免只在到达限速点后才制动。

[calc.jl](https://github.com/railtoolkit/TrainRuns.jl/blob/529cdd1d2a158077440ce4e3093a8b875a1191fb/src/calc.jl) 的 `calculateTractiveEffort` 在相邻速度点之间线性插值，超过表格最高速度时采用末项牵引力。迁移保留这些核心关系，并由 C# 输入验证和有序表查找处理边界。

[formulary.jl](https://github.com/railtoolkit/TrainRuns.jl/blob/529cdd1d2a158077440ce4e3093a8b875a1191fb/src/formulary.jl) 的核心关系包括：
- 以千分数表示的线路阻力系数换算力：`F = coefficient / 1000 × mass × g`。
- 考虑旋转质量的加速度：`a = (traction - resistance) / (mass × rotationFactor)`。
- 恒加速度空间步进：`v1² = v0² + 2 × a × distance`。
- 给定制动能力和目标速度，反求允许的入口速度或制动距离。

C# 距离网格、类型化输入输出、前向求解、验证和项目集成为新实现，不是把上游 Julia 状态机或整个包逐行转换。上游采用给定的恒定净制动减速度；本组件把制动扩展为制动力、列车阻力与坡度共同作用。因此仅保留列出的算法和公式来源，不宣称与 TrainRuns.jl 完整数值等价。

本次针对受牵引、制动和限速约束的快速运行曲线；经济驾驶、全局最小能耗、驾驶策略搜索、空气制动传播、冲动限制和信号交互不是由这些移植函数自动提供的能力。

## 复杂度与数值限制

设网格点数量为 N、牵引力表长度为 K。上游恒减速度制动约束递推遍历区间一次；固定网格的前后向递推也各遍历网格一次。若每点对牵引表作二分查找，则该求解阶段可按 O(N log K) 估算，固定 K 时随 N 线性增长，保存曲线需要 O(N) 空间。网格边界排序、区间查询、边界细分或额外迭代应另计，不能把完整计算无条件标为 O(N)。

空间离散把单步内的力学条件作近似。步长影响速度及运行时间误差；坡度或限速突变应成为显式边界，不能靠跨越边界的大步长处理。对于速度相关牵引/阻力及变化坡度，双向网格计算不构成连续最优控制问题的精确解。牵引不足、无法满足入口/出口条件或下坡制动不足必须报告当前模型或网格无法满足条件，不能通过截断负根号等方式伪造可行轨迹。

本文件没有提供或推定任何实测速度优势。运行耗时应注明硬件、运行时、线路长度、网格数量、牵引表规模、预热方式及重复次数；精度应通过解析简单案例、边界案例和步长收敛结果单独验证。

## 许可证交付

直接迁移部分适用 [TrainRuns.jl 的固定 ISC 原文](https://github.com/railtoolkit/TrainRuns.jl/blob/529cdd1d2a158077440ce4e3093a8b875a1191fb/LICENSE)。仓库保存原文于 `docs/legal/licenses/trainruns-ISC.txt`，组件 `THIRD-PARTY-NOTICES.txt` 包含固定提交、源文件、改动边界和完整许可。包含移植实现的源码和二进制发行应随附该声明，不能只保留一个外部链接。独立 SwitchYard 代码的许可不因该说明改变。

其他候选的核查依据：
- [OSRD MaxSpeedEnvelope.kt](https://github.com/OpenRailAssociation/osrd/blob/283a61b691d50a67ad0fed4bbe1ffea5d13dd192/core/envelope-sim/src/main/kotlin/fr/sncf/osrd/envelope_sim/pipelines/MaxSpeedEnvelope.kt)、[REUSE.toml](https://github.com/OpenRailAssociation/osrd/blob/283a61b691d50a67ad0fed4bbe1ffea5d13dd192/REUSE.toml)、[LGPL-3.0-only](https://github.com/OpenRailAssociation/osrd/blob/283a61b691d50a67ad0fed4bbe1ffea5d13dd192/LICENSES/LGPL-3.0-only.txt)。
- [ALTRIOS braking_point.rs](https://github.com/NREL/altrios/blob/bc4a8e6ca54c9fcc5cee29a4034598629794a98d/altrios-core/src/train/braking_point.rs)、[speed_limit_train_sim.rs](https://github.com/NREL/altrios/blob/bc4a8e6ca54c9fcc5cee29a4034598629794a98d/altrios-core/src/train/speed_limit_train_sim.rs)、[LICENSE.md](https://github.com/NREL/altrios/blob/bc4a8e6ca54c9fcc5cee29a4034598629794a98d/LICENSE.md)。