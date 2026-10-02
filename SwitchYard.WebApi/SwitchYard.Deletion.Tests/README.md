# 删除关联数据回归

从仓库根目录运行 `dotnet run --project SwitchYard.WebApi/SwitchYard.Deletion.Tests`。测试使用新建的临时 SQLite 数据库和固定测试身份，直接调用真实控制器和服务，覆盖作用域隔离、父子数据清理、引用冲突、结果失效、可选表缺失及事务回滚。数据库和临时文件在结束时删除，不读取应用配置，不操作业务数据库。

HTTP 路由和绑定的相关回归另运行 `SwitchYard.OperationProcess.Tests`；独立站场适配器另运行 `SwitchYard.Service.StationLayout.IntegrationTests`。删除策略见 [删除与关联数据维护](../../docs/deletion-lifecycle.md)。
