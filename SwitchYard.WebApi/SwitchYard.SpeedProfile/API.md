# SpeedProfile 牵引计算 HTTP 接口

`POST /api/SpeedProfile/calculate` 使用现有 JWT 登录认证：`Authorization: Bearer <token>`。
请求与响应为 `application/json`、camelCase。此接口为纯计算，不读取或修改数据库。前端仅编辑参数、提交请求和展示响应，数值求解统一由 `SwitchYard.SpeedProfile.SpeedProfileCalculator` 完成。

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
    "resistanceA": 5,
    "resistanceB": 0.05,
    "resistanceC": 0.002
  },
  "signal": { "routeSettingS": 15, "reactionS": 3, "clearanceS": 5 },
  "options": {
    "mode": "through",
    "initialSpeedKmh": 0,
    "finalSpeedKmh": 0,
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
| `train.rotatingMassFactor` | 惯性质量/静质量，1～3。 |
| `train.maxSpeedKmh` | 车辆最高速度，0.001～1000 km/h。 |
| `train.maxBrakeForceKn` | 恒定最大常用制动力，0.001～10000000 kN；在坡度和阻力计入前的制动力。 |
| `train.tractionCurve` | 1～128 点，从 0 km/h 起；速度严格递增，力非负且非递增。线性插值；末点后保持末点力，单点代表恒力。无需覆盖车辆最高速度。 |
| `train.resistanceA/B/C` | Davis 运行阻力 `R(kN)=A+B*v+C*v²`，其中 `v` 为 km/h；系数非负，各不超过 10000000。 |
| `profile.lengthM` | 0.001～10000000 m。 |
| `profile.segments` | 1～10000 段，按运行方向顺序连续覆盖全进路；从 0 起，最后 `endM` 等于 `lengthM`。 |
| `segment.speedLimitKmh` | 0.001～1000 km/h。 |
| `segment.gradientPermille` | 默认 0，范围 ±300‰，正数为上坡。 |
| `segment.additionalResistanceKn` | 默认 0，范围 0～10000000 kN，正值为附加阻力。 |
| `profile.devices` | `{id,label,type,positionM}`；位置在进路内，元数据供请求完整描述线路。 |
| `profile.joints` | `{id,label,positionM}`；绝缘位置在进路内。 |
| `profile.cells` | `{id,label,startM,endM,entryBoundaryKnown?,exitBoundaryKnown?}`；起终点在进路内、终点大于起点，两个边界标志默认 true。 |
| `signal.routeSettingS/reactionS/clearanceS` | 进路办理、司机反应和释放延迟，各 0～86400 s。 |
| `options.mode` | `arrival`、`departure` 或 `through`。arrival 要求末速度为 0；departure 要求初速度为 0。不满足时返回 400。 |
| `options.initialSpeedKmh/finalSpeedKmh` | 指定边界速度，0～1000 km/h。超过车辆/线路限速或无法到达时返回 422。 |
| `options.stepM` | 默认 5 m，0.1～100 m。实际网格含线路分界、车尾限速解除点、停车点；每个子区间至少两个步长，因此不保证固定间距。 |
| `options.maxPointCount` | 默认及最大 100001，最小 2，限制所有输出采样点（含停时重复点）；超过返回 400，可增大步长或缩短进路。 |
| `options.stops` | 默认空数组，最多 1000 点；`positionM` 唯一且在进路内，`dwellS` 默认 0、范围 0～86400 s。起点/终点停车要求相应边界速度为 0。 |

`devices+joints+cells` 总数最多 20000；元数据字符串最长 256 字符；HTTP 请求体最大 8 MiB。所有对象拒绝未知 JSON 字段，尤其旧 `brakingCurve` 必须改为 `maxBrakeForceKn`，以免旧输入被静默忽略。数组不能为 null。

后端统一换算 SI 单位：质量 `t×1000`、力 `kN×1000`、速度 `km/h÷3.6`；Davis 系数转换为 `A×1000`、`B×3600`、`C×12960`。

## 响应

成功 `200`，响应直接为结果对象，不附加 envelope，兼容前端原 `TractionResult`：

| 字段 | 含义 |
| --- | --- |
| `samples` | `{positionM,speedKmh,limitKmh,timeS}` 数组；时间已包含办理及反应时间。停车有相同位置的到达、停时结束两个点，不能按位置去重。 |
| `jointTimes` | 保留 `{id,label,positionM}`，增加 `headTimeS`、`tailTimeS`、`releaseTimeS`。无法出清时后两者为 null。 |
| `cellOccupancies` | 保留电路区段与边界字段，增加 `entryTimeS`、`exitTimeS`、`releaseTimeS`；未知入口/出口边界对应时刻为 null，无法出清时出口/释放为 null。 |
| `lengthM/trainLengthM/mode` | 本次计算采用的进路长度、车长和模式。 |
| `motionStartS` | 运行曲线时间原点：办理时间 + 反应时间。若起点另有停时，实际开动还须等待该停时。 |
| `durationS` | 曲线总历时，包含停站时间。 |
| `runningTimeS/dwellTimeS` | 纯运行时间/停站时间，之和等于 `durationS`。 |
| `totalTimeS` | `motionStartS + durationS`，不含终点以外的车尾外延及释放延迟。 |
| `maxSpeedKmh` | 本次曲线实际最高速度。 |
| `engine` | 固定 `SwitchYard.SpeedProfile`。 |
| `warnings` | 下表中的提示代码，可组合出现。 |

| warning | 含义 |
| --- | --- |
| `cell-boundary-unknown` | 显示进路只覆盖物理电路的一部分，某一边界未知，不能计算对应时刻。 |
| `uncleared-after-stop` | 列车停车时仍压绝缘或电路，车尾无法在当前曲线内出清。 |
| `tail-extension-assumed` | departure/through 末速度大于 0 时，为求终点外车尾出清，假设列车继续保持末速度；这部分不属于计算出的线路速度曲线。 |
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
| 422 | `infeasible-profile` | 制动力/牵引力不足、边界速度不可达或当前网格无法满足核心力学约束。 |

计算错误响应均为 `{ "code": "...", "message": "..." }`；JSON 绑定错误另有可选 `errors` 字典（字段路径 → 错误消息数组，最多 20 字段/每字段 3 条），便于定位输入；核心返回的错误 message 可能为英文，前端应显示通用中文说明并保留详情。连接取消通过 `HttpContext.RequestAborted` 传到预处理与核心求解，前端取消请求后不回退到本地计算。

## 实现位置

- `SwitchYard.Service/Controllers/SpeedProfileController.cs`：JWT、HTTP 状态、仅本接口的 JSON 错误契约、请求体限制。
- `SwitchYard.Service/Models/SpeedProfileApiModels.cs`：请求/响应 DTO 与严格 JSON 字段定义。
- `SwitchYard.Service/Services/SpeedProfileCalculationService.cs`：验证、单位转换、限速预处理、核心调用和时序派生。
- `SwitchYard.SpeedProfile.ApiTests`：独立 Kestrel HTTP 集成测试；测试认证处理器只在测试宿主启用，不改变生产 JWT 配置。

核心算法及上游开源许可说明见本目录 `README.md` 和 `THIRD-PARTY-NOTICES.txt`。