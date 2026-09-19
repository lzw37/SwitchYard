# 作业过程编排接口集成测试

运行：

```powershell
dotnet build SwitchYard.WebApi/SwitchYard.OperationProcess.Tests -m:1
dotnet run --project SwitchYard.WebApi/SwitchYard.OperationProcess.Tests --no-build
```

测试通过真实 MVC 路由、JSON 绑定和授权中间件访问本机随机端口，使用临时 SQLite 数据库和仅在测试宿主生效的身份验证。不会读取或改写应用数据库。覆盖模板持久化、重启后加载、四类对象增删查改、站场作用域和所有权、多个角色中的管理员权限、并发乐观锁冲突、活动和次序时长、跨对象和跨站场引用、循环依赖、可留空的进路端点锚，以及旧进路类型归一化。

还通过原作业计划接口验证复制计划时携带过程模板、副本独立编辑、计划编号修改后的作用域一致性，以及删除计划时清理模板。

批量生成回归覆盖：追加全部五类活动、独立约束快照、按原平均时间槽定位列车基准时刻，并在每列车内部生成满足完整约束的最早事件时刻。覆盖零间隔衔接、非零间隔、并行与孤立事件、最大时长及固定后端事件的反向约束、活动数组顺序独立性、节点／锚联合候选、小数秒和跨日；通过真实 HTTP 生成和重新读取，核对实际移动时间与快照端点均直接衔接。不可行与写入失败时全部回滚；快照在原过程修改／删除后保留，并随列车及计划生命周期维护。

批量删车回归覆盖：列车、移动和过程快照原子清理，其他范围及模板保持完整，授权和参数校验、缺失列车冲突、区分大小写的去重、超过 400 个 ID 的分批删除，以及 SQLite `ABORT`／`IGNORE` 触发器导致的完整事务回滚。

CellOccupancy CSV 导入回归覆盖：BOM、CSV 引号与逗号、完整表头、Cell 自动及人工匹配、现有进路复用与缺失进路新建、Dw 停留类型、占用起止秒精确还原与负偏移提前锁闭；预览不写入、默认新计划与追加、重复车次拒绝、文件或站场变更后预览失效、重复提交拒绝、所有权和站场隔离、引号参数化，以及 SQLite `ABORT`／`IGNORE` 导致的原子回滚。全部样本和站场均写入本次运行新建的临时测试库。

如需同时运行真实求解器及饱和计划保存／重新加载回归，可在仓库根目录使用独立输出目录，避免与正在运行的业务服务互相锁定：

```powershell
dotnet build SwitchYard.WebApi/SwitchYard.CapacityAgent --no-restore -m:1 -p:OutDir=D:\SwitchYard\SwitchYard.WebApi\artifacts\process-plan-worker\
dotnet build SwitchYard.WebApi/SwitchYard.OperationProcess.Tests --no-restore -m:1 -p:OutDir=D:\SwitchYard\SwitchYard.WebApi\artifacts\process-actual-tests\
$env:SWITCHYARD_PROCESS_TEST_WORKER = 'D:\SwitchYard\SwitchYard.WebApi\artifacts\process-plan-worker\SwitchYard.CapacityAgent.exe'
dotnet D:\SwitchYard\SwitchYard.WebApi\artifacts\process-actual-tests\SwitchYard.OperationProcess.Tests.dll
```

Worker 通过临时请求文件离线求解，不登录计算代理、不连接业务数据库。设置上述环境变量时执行完整 Worker 回归；未设置时跳过该部分。每次运行会在最后报告实际通过的断言数。
