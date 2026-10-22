# 牵引计算前后端接入

运行曲线由后端 `SwitchYard.SpeedProfile` 计算。前端保留站场拓扑展开、参数编辑、图形渲染及服务端采样点的光标插值；不再生成牵引/制动速度曲线或绝缘、轨道电路占用结果，服务不可用时不回退到浏览器求解。

## 接口

`POST /api/SpeedProfile/calculate`，沿用应用已有 JWT 登录。请求为 `{ profile, train, signal, options }`，返回速度采样、车头/车尾通过时刻、占用/释放时刻及运行时间。完整契约、单位、错误格式及示例见 [后端 API 文档](../../../SwitchYard.WebApi/SwitchYard.SpeedProfile/API.md)。

- 前端输入采用 m、s、t、kN、km/h；Davis 系数使用 `R(kN)=A+B*v(km/h)+C*v(km/h)^2`。全部 SI 换算在后端完成，尤其 `B*3600`、`C*12960`。
- 牵引表速度从 0 严格递增、力非负非递增；制动输入为恒定常用制动力 kN。
- 每段可编辑坡度、限速及附加阻力 kN；可增加沿进路里程的停车点和停时。
- 初末速度、步长和点数预算均传入后端。接车末速固定 0，发车初速固定 0；预算包含停车产生的重复里程点。
- 车长用于车尾出清及解除限速。坡度/附加阻力按车头所在段的单质点模型处理，非零坡度结果显示该假设。
- `durationS` 含停车时间；`runningTimeS` 是纯运行时间；`totalTimeS` 还包括办理进路及反应时间。停站处的重复里程点表示到达和停时结束。

## 交互与参数迁移

参数变化立即清除旧结果，350 ms 防抖后请求。切换进路、实例、方案，隐藏或卸载模块时取消请求；版本检查防止迟到响应覆盖新结果。错误可重试且不会展示上一次计算结果，导出只针对当前已完成请求。

本机保存改用 `switchyard.traction.v2:<实例>:<方案>`，兼容读取 v1。旧恒定制动曲线映射为标量；旧可变曲线取最小制动力作保守转换并明确提示核对，迁移提示随参数保存。切换进路或接续进路会清空停车点并提示，避免误套上一条线路的停车里程。

## 验证

在前端目录执行 `npm run test:traction` 和 `npm run build`。

后端接口测试使用隔离 Kestrel 进程，调用真实控制器、适配服务和计算库；测试认证仅存在于测试项目，不加载业务数据库：

```powershell
dotnet run --project SwitchYard.WebApi/SwitchYard.SpeedProfile.ApiTests/SwitchYard.SpeedProfile.ApiTests.csproj -c Release
```

核心数值测试继续由 `SwitchYard.SpeedProfile.Tests` 负责。接口测试覆盖认证、JSON 校验、SI 换算、模式边界、整列限速、停车及区段占用。前端回归覆盖参数快照、服务调用、取消、错误不回退及停站图表插值。
