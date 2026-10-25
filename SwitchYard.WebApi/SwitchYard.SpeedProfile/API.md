# SpeedProfile 牵引计算 HTTP 接口

`POST /api/SpeedProfile/calculate` 使用现有 JWT 登录认证：`Authorization: Bearer <token>`。
请求与响应为 `application/json`、camelCase。此接口为纯计算，不读取或修改数据库。前端仅编辑参数、提交请求和展示响应，数值求解统一由 `SwitchYard.SpeedProfile.SpeedProfileCalculator` 完成。

## 接车、通过入口前制动预留段

options.mode 为 "arrival" 或 "through" 时，`options.brakingDistanceM` 为已填写的非负数值则直接使用（不按 stepM 取整）；缺省或 null 时，后端先用有效制动段最高速度 → 0 自动计算制动距离，再在原进路入口前追加有效距离。显式 0 表示不追加，清空输入恢复 null 后使用自动计算。两种模式采用相同的预留规则，复用核心 Calculate 的反向制动包络步进，使用相同的质量、回转质量因子、制动及阻力离散表；入口外坡度和附加阻力沿用原进路首段，预留段限速为 `min(options.brakingMaxSpeedKmh ?? train.maxSpeedKmh, train.maxSpeedKmh)`。自动距离按 stepM 向上取整；手动、自动预留的完整运行曲线均受 maxPointCount 限制。

请求仍使用原进路坐标，不预先延长。响应 approachDistanceM 为追加距离（发车为 0），lengthM 为追加后的全长；原进路入口在响应坐标 approachDistanceM，停车点、设备、绝缘及轨道电路统一平移。追加段不创建虚构的轨道电路。initialSpeedKmh 与 finalSpeedKmh 分别作为运行曲线起终点的期望上限，实际速度可因限速、动力和停车要求降低；通过模式不要求终点停车。信号办理及反应时间仍按现有时间偏移处理。占用图以车头进入整个预留范围的时刻作为各区段共同的接近起点；占用回填从开始办理进路计至各区段出清释放，站内进路起止事件按平移后的原进路边界计算。

保存快照保留原请求和延长后的结果；重复计算不会累加延长距离。历史结果不自动重算，重新点击“计算”后应用此规则。

## 发车、通过末端一离去

`options.departureSectionLengthM` 是用户输入的一离去长度，默认 0，范围 0～100000 m，在 departure 或 through 模式且大于 0 时生效。请求 profile 仍为原进路，后端在末端追加同名轨道电路及线路段，沿用原末段坡度、附加阻力，限速为 `min(options.departureSectionMaxSpeedKmh ?? train.maxSpeedKmh, train.maxSpeedKmh)`，不移动原设备和停车点。终点期望速度应用到追加区段末端；响应 `departureSectionLengthM` 返回实际追加长度，`lengthM`、运行曲线、历时和区段占用包含此段。旧请求保持原行为，arrival 模式不追加；through 同时保留入口前 approachDistanceM 制动预留段，两段分别计长。追加电路的标识以 `__first-departure__` 开始并避开已有标识，不作为站场 CellID 回填；原进路的区段回填继续采用原进路边界作为相对时间基准。

## 调车

`options.mode="shunting"` 时只计算原 profile，`approachDistanceM` 和 `departureSectionLengthM` 均为 0，原区段和绝缘坐标不平移，结果不增加电路。界面默认起终点期望速度为 0，但请求允许独立设置两项期望上限；制动距离、一离去长度和两段最高速度即使有已保存值也不作用于调车。末速为 0 且车尾未出清时释放时刻保持 null；末速大于 0 时，沿用实际末速求车尾通过原末端的时刻，不增加车头曲线里程。

## 请求示例

```json
{
  "profile": {
    "lengthM": 1000,
    "segments": [
      { "startM": 0, "endM": 500, "speedLimitKmh": 60, "gradientPermille": 0, "additionalResistanceKn": 0 },
      { "startM": 500, "endM": 1000, "speedLimitKmh": 100, "gradientPermille": 2, "additionalResistanceKn": 0 }
    ],
    "devices": [],
    "joints": [{ "id": "joint-1", "label": "绝缘1", "positionM": 500 }],
    "cells": [{ "id": "cell-1", "label": "轨道区段1", "startM": 0, "endM": 500, "entryBoundaryKnown": true, "exitBoundaryKnown": true }]
  },
  "train": {
    "lengthM": 200,
    "massT": 440,
    "rotatingMassFactor": 1.08,
    "maxSpeedKmh": 160,
    "maxBrakeForceKn": 310,
    "tractionCurve": [
      { "speedKmh": 0, "forceKn": 300 },
      { "speedKmh": 40, "forceKn": 300 },
      { "speedKmh": 80, "forceKn": 210 },
      { "speedKmh": 120, "forceKn": 140 },
      { "speedKmh": 160, "forceKn": 105 }
    ],
    "brakingCurve": [{ "speedKmh": 0, "forceKn": 310 }, { "speedKmh": 160, "forceKn": 310 }],
    "resistanceCurve": [{ "speedKmh": 0, "forceKn": 5 }, { "speedKmh": 80, "forceKn": 21.8 }, { "speedKmh": 160, "forceKn": 64.2 }],
    "resistanceA": 5,
    "resistanceB": 0.05,
    "resistanceC": 0.002
  },
  "signal": { "routeSettingS": 15, "reactionS": 3, "clearanceS": 5 },
  "options": {
    "mode": "through",
    "initialSpeedKmh": 0,
    "finalSpeedKmh": 0,
    "brakingDistanceM": null,
    "brakingMaxSpeedKmh": null,
    "departureSectionMaxSpeedKmh": null,
    "stepM": 5,
    "maxPointCount": 100001,
    "stops": [{ "positionM": 700, "dwellS": 30 }]
  }
}
```

## 参数和单位

全部距离为 m，时间为 s，质量为 t，速度为 km/h，力为 kN。所有数值必须有限。除明确标注有默认值的字段外，示例中的字段均必须提供；`devices`、`joints`、`cells` 默认为空数组。

| 字段 | 说明与约束 |
| --- | --- |
| `train.lengthM` | 列车长度，0.001～100000 m；用于整列限速和车尾出清时间。 |
| `train.massT` | 静质量，0.001～10000000 t。 |
| `train.rotatingMassFactor` | 惯性质量/静质量倍率 `1 + γ`，1～3。界面直接输入回转质量系数 `γ`（0～2），提交前加 1；例如界面 0.1 对应本字段 1.1。 |
| `train.maxSpeedKmh` | 车辆最高速度，0.001～1000 km/h。 |
| `train.maxBrakeForceKn` | 生成制动表使用的恒定常用制动力，0.001～10000000 kN；在坡度和阻力计入前的制动力。 |
| `train.tractionCurve` | 1～128 点，从 0 km/h 起；速度严格递增，力非负且非递增。线性插值；末点后保持末点力，单点代表恒力。无需覆盖车辆最高速度。 |
| `train.brakingCurve/resistanceCurve` | 速度—力离散表；各 1～128 点，从 0 起且速度严格递增，力可升可降且非负。速度不超过 10000 km/h，力不超过 10000000 kN。线性插值，表尾维持末点力。兼容旧请求：未提供或 null 时按标量生成每 10 km/h 至最高速度的表格，空数组非法。 |
| `train.resistanceA/B/C` | 生成阻力表的 Davis 公式 `R(kN)=A+B*v+C*v²`，其中 `v` 为 km/h；系数非负，各不超过 10000000。 |
| `profile.lengthM` | 0.001～10000000 m。 |
| `profile.segments` | 1～10000 段，按运行方向顺序连续覆盖全进路；从 0 起，最后 `endM` 等于 `lengthM`。 |
| `segment.speedLimitKmh` | 0.001～1000 km/h。 |
| `segment.gradientPermille` | 默认 0，范围 ±300‰，正数为上坡。 |
| `segment.additionalResistanceKn` | 默认 0，范围 0～10000000 kN，正值为附加阻力。 |
| `profile.devices` | `{id,label,type,positionM}`；位置在进路内，元数据供请求完整描述线路。 |
| `profile.joints` | `{id,label,positionM}`；绝缘位置在进路内。 |
| `profile.cells` | `{id,label,startM,endM,entryBoundaryKnown?,exitBoundaryKnown?}`；起终点在进路内、终点大于起点，两个边界标志默认 true。 |
| `signal.routeSettingS/reactionS/clearanceS` | 进路办理、司机反应和释放延迟，各 0～86400 s。 |
| `options.mode` | `arrival`、`departure`、`through` 或 `shunting`。arrival 要求末速度为 0；departure 要求初速度为 0；shunting 的起终点期望速度由界面默认 0，均允许设置为其他非负值。不满足时返回 400。 |
| `options.initialSpeedKmh/finalSpeedKmh` | 起终点期望速度上限，0～1000 km/h；实际端点速度可低于期望值，取响应 samples 首末点的 speedKmh。超过限速或终速达不到期望值时自动降低，不因此返回 422。 |
| `options.brakingDistanceM` | 可选，默认 null；0～10000000 m 的有限数值。接车、通过优先使用输入值，缺省或 null 时自动计算，0 表示无入口前预留段；发车、调车不生效。 |
| `options.brakingMaxSpeedKmh` | 可选，默认 null，正有限数值 0.001～1000 km/h；接车、通过制动段最高速度，同时用于自动制动距离的起始速度。缺省或 null 采用车辆最高速度，显式值仍受车辆最高速度约束。 |
| `options.departureSectionMaxSpeedKmh` | 可选，默认 null，正有限数值 0.001～1000 km/h；发车、通过一离去最高速度。缺省或 null 采用车辆最高速度，显式值仍受车辆最高速度和车尾未出清的前段限速约束。 |
| `options.departureSectionLengthM` | 默认 0，0～100000 m。发车或通过末端的一离去区段长度，正值时追加；接车、调车模式不生效。 |
| `options.stepM` | 默认 5 m，0.1～100 m。实际网格含线路分界、车尾限速解除点、停车点；每个子区间至少两个步长，因此不保证固定间距。 |
| `options.maxPointCount` | 默认及最大 100001，最小 2，限制所有输出采样点（含停时重复点）；超过返回 400，可增大步长或缩短进路。 |
| `options.stops` | 默认空数组，最多 1000 点；`positionM` 唯一且在进路内，`dwellS` 默认 0、范围 0～86400 s。停车要求优先于期望速度，对应实际速度为 0。 |

`devices+joints+cells` 总数最多 20000；元数据字符串最长 256 字符；HTTP 请求体最大 8 MiB。所有对象拒绝未知 JSON 字段。提供制动或阻力离散表时，该表优先于恒定制动力或阻力公式，标量只保留为表格生成参数。除上述兼容字段外数组不能为 null。

后端统一换算 SI 单位：质量 `t×1000`、力 `kN×1000`、速度 `km/h÷3.6`；Davis 系数转换为 `A×1000`、`B×3600`、`C×12960`。

## 响应

成功 `200`，响应直接为结果对象，不附加 envelope，兼容前端原 `TractionResult`：

| 字段 | 含义 |
| --- | --- |
| `samples` | `{positionM,speedKmh,limitKmh,timeS}` 数组；时间已包含办理及反应时间。停车有相同位置的到达、停时结束两个点，不能按位置去重。 |
| `jointTimes` | 保留 `{id,label,positionM}`，增加 `headTimeS`、`tailTimeS`、`releaseTimeS`。无法出清时后两者为 null。 |
| `cellOccupancies` | 保留电路区段与边界字段，增加 `entryTimeS`、`exitTimeS`、`releaseTimeS`；未知入口/出口边界对应时刻为 null，无法出清时出口/释放为 null。 |
| `lengthM/trainLengthM/mode` | 本次计算采用的进路长度、车长和模式。 |
| `approachDistanceM/departureSectionLengthM` | 入口前制动预留长度/发车或通过末端一离去长度，未追加时为 0；`lengthM` 为原进路加相应追加段的全长。 |
| `motionStartS` | 运行曲线时间原点：办理时间 + 反应时间。若起点另有停时，实际开动还须等待该停时。 |
| `durationS` | 曲线总历时，包含停站时间。 |
| `runningTimeS/dwellTimeS` | 纯运行时间/停站时间，之和等于 `durationS`。 |
| `totalTimeS` | `motionStartS + durationS`，不含终点以外的车尾外延及释放延迟。 |
| `maxSpeedKmh` | 本次曲线实际最高速度。 |
| `engine` | 固定 `SwitchYard.SpeedProfile`。 |
| `warnings` | 下表中的提示代码，可组合出现。 |

| warning | 含义 |
| --- | --- |
| `initial-speed-reduced` | 起点实际速度低于期望值，已按限速、制动及停车条件降低。 |
| `final-speed-reduced` | 终点实际速度低于期望值，已按限速、牵引及停车条件降低。 |
| `cell-boundary-unknown` | 显示进路只覆盖物理电路的一部分，某一边界未知，不能计算对应时刻。 |
| `uncleared-after-stop` | 列车停车时仍压绝缘或电路，车尾无法在当前曲线内出清。 |
| `tail-extension-assumed` | departure/through/shunting 末速度大于 0 时，为求终点外车尾出清，假设列车继续保持末速度；这部分不属于计算出的线路速度曲线。 |
| `point-mass-gradient` | 路线含非零坡度；核心是单质点模型，坡度按车头所在区段计算，不按车长均布质量积分。 |

限速约束按整列车处理：原区段 `[startM,endM]` 的限速保持至车头到达 `endM+train.lengthM`；核心计算在这些事件位置重新分段。坡度和附加阻力取车头所在原区段。边界处保守采用相邻区段的较低限速。

区间内的设备/边界时刻依据核心相邻采样点的恒加速度运动学反算，并非线性时间插值。目标位置恰好是停车点时取首次到达时刻；位置越过停车点后，插值起点为停时结束的采样点。绝缘车尾时刻等于车头到达 `positionM+train.lengthM` 的时刻，电路出口同理。释放时间为对应车尾/出口时刻加 `clearanceS`。

## 错误与取消

| HTTP | 错误代码 | 含义 |
| --- | --- | --- |
| 400 | `invalid-input` | JSON、缺失/未知字段、参数范围、模式边界、重复停车点或网格超限。 |
| 400 | `invalid-route` | 不连续路线、错误设备/电路位置或路线数量限制。 |
| 400 | `invalid-curve` | 非法牵引特性表。 |
| 401 | — | 未登录或 JWT 无效，沿用现有认证流程。 |
| 413 | — | HTTP 请求体超出上限，由服务器拒绝。 |
| 422 | `infeasible-profile` | 车辆无法完成进路、制动力不足或当前网格无法满足核心力学约束；仅期望速度达不到不会触发此错误。 |

计算错误响应均为 `{ "code": "...", "message": "..." }`；JSON 绑定错误另有可选 `errors` 字典（字段路径 → 错误消息数组，最多 20 字段/每字段 3 条），便于定位输入；核心返回的错误 message 可能为英文，前端应显示通用中文说明并保留详情。连接取消通过 `HttpContext.RequestAborted` 传到预处理与核心求解，前端取消请求后不回退到本地计算。

## 实现位置

- `SwitchYard.Service/Controllers/SpeedProfileController.cs`：JWT、HTTP 状态、仅本接口的 JSON 错误契约、请求体限制。
- `SwitchYard.Service/Models/SpeedProfileApiModels.cs`：请求/响应 DTO 与严格 JSON 字段定义。
- `SwitchYard.Service/Services/SpeedProfileCalculationService.cs`：验证、单位转换、限速预处理、核心调用和时序派生。
- `SwitchYard.SpeedProfile.ApiTests`：独立 Kestrel HTTP 集成测试；测试认证处理器只在测试宿主启用，不改变生产 JWT 配置。
- `SwitchYard.SpeedProfile.RegressionTests`：直接运行真实计算服务、数值库及快照校验，覆盖通过制动预留、时刻、停车点平移、旧快照兼容、预算和取消，不连接业务数据库。

核心算法及上游开源许可说明见本目录 `README.md` 和 `THIRD-PARTY-NOTICES.txt`。
