# HTTP 接口清单

[文档目录](README.md) · [English](../en/appendix-api-routes.md) · [接口与数据说明](10-api-data-reference.md)

下表列出 2026-10-11 主服务控制器定义的全部 175 个 HTTP 操作。方法和路径按实际路由展开，`{...}` 为路径参数，查询参数和请求正文见操作链接对应的方法签名与 DTO。相同路径可能对应不同方法。此清单不包含 SignalR 自动协商、Swagger 或静态文件端点。

这些操作并非都可匿名使用，也不是可直接自动执行的测试列表。认证、范围、revision、错误处理和示例见接口与数据说明。

## 认证与会话

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| POST | `/api/Auth/login` | [Login](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs#L38) |
| POST | `/api/Auth/refresh` | [Refresh](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs#L108) |
| POST | `/api/Auth/logout` | [Logout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs#L163) |
| POST | `/api/Auth/createuser` | [CreateUser](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs#L191) |
| GET | `/api/Auth/userinfo` | [GetUserInfo](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs#L295) |
| PUT | `/api/Auth/userinfo` | [UpdateUserInfo](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs#L333) |
| POST | `/api/Auth/changepassword` | [ChangePassword](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AuthController.cs#L378) |

## 用户管理

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| GET | `/api/Admin/users` | [GetUsers](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L31) |
| GET | `/api/Admin/users/paged` | [GetUsersPaged](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L57) |
| POST | `/api/Admin/users` | [CreateUser](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L89) |
| PUT | `/api/Admin/users/{id}` | [UpdateUser](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L175) |
| POST | `/api/Admin/users/{id}/reset-password` | [ResetUserPassword](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L266) |
| DELETE | `/api/Admin/users/{id}` | [DeleteUser](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L301) |
| GET | `/api/Admin/users/import-template` | [DownloadImportTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L350) |
| POST | `/api/Admin/users/import` | [ImportUsers](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/AdminController.cs#L397) |

## 系统信息

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| GET | `/api/System/version` | [GetVersion](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/SystemController.cs#L16) |

## 课程资源

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| GET | `/Course/GetVideoManifest` | [GetVideoManifest](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs#L41) |
| GET | `/Course/GetTeachingManifest` | [GetTeachingManifest](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs#L62) |
| GET | `/Course/StreamVideo` | [StreamVideo](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs#L83) |
| GET | `/Course/StreamTeachingFile` | [StreamTeachingFile](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CourseController.cs#L95) |

## 驼峰

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| GET | `/Hump/GetInstances` | [GetInstances](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L282) |
| GET | `/Hump/GetInstancePage` | [GetInstancePage](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L304) |
| POST | `/Hump/CreateInstance` | [CreateInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L357) |
| POST | `/Hump/CopyHumpInstance` | [CopyHumpInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L440) |
| PUT | `/Hump/EditInstance` | [EditInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L511) |
| DELETE | `/Hump/DeleteInstance` | [DeleteInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L572) |
| GET | `/Hump/GetSlopeLines` | [GetSlopeLines](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L619) |
| POST | `/Hump/CreateSlopeLine` | [CreateSlopeLine](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L640) |
| PUT | `/Hump/EditSlopeLine` | [EditSlopeLine](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L671) |
| DELETE | `/Hump/DeleteSlopeLine` | [DeleteSlopeLine](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L707) |
| POST | `/Hump/CopySlopeLine` | [CopySlopeLine](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L759) |
| GET | `/Hump/GetFlatLayout` | [GetFlatLayout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1269) |
| PUT | `/Hump/EditFlatLayout` | [EditFlatLayout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1366) |
| DELETE | `/Hump/DeleteFlatLayout` | [DeleteFlatLayout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1447) |
| GET | `/Hump/GetWagonConcept` | [GetWagonConcept](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1481) |
| POST | `/Hump/CreateWagonConcept` | [CreateWagonConcept](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1503) |
| PUT | `/Hump/EditWagonConcept` | [EditWagonConcept](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1569) |
| DELETE | `/Hump/DeleteWagonConcept` | [DeleteWagonConcept](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1635) |
| GET | `/Hump/GetOperationConditions` | [GetOperationConditions](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1716) |
| POST | `/Hump/CreateOperationCondition` | [CreateOperationCondition](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1747) |
| PUT | `/Hump/EditOperationCondition` | [EditOperationCondition](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1804) |
| DELETE | `/Hump/DeleteOperationCondition` | [DeleteOperationCondition](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1860) |
| GET | `/Hump/GetSlopeLayout` | [GetSlopeLayout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1911) |
| POST | `/Hump/CreateSlopeLayout` | [CreateSlopeLayout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L1989) |
| PUT | `/Hump/EditSlopeLayout` | [EditSlopeLayout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2056) |
| DELETE | `/Hump/DeleteSlopeLayout` | [DeleteSlopeLayout](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2126) |
| POST | `/Hump/ExecuteEnergyHeightCalculation` | [ExecuteEnergyHeightCalculation](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2159) |
| POST | `/Hump/GetKineticEnergyHeight` | [GetKineticEnergyHeight](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2264) |
| POST | `/Hump/GetResistanceEnergyHeight` | [GetResistanceEnergyHeight](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2345) |
| POST | `/Hump/GetResistanceEnergyHeightDetail` | [GetResistanceEnergyHeightDetail](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2394) |
| POST | `/Hump/GetBreakingEnergyHeight` | [GetBreakingEnergyHeight](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2427) |
| POST | `/Hump/GetVelocityCurve` | [GetVelocityCurve](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2525) |
| POST | `/Hump/GetTimeCurve` | [GetTimeCurve](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2549) |
| GET | `/Hump/GetHumpSchemes` | [GetHumpSchemes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2608) |
| POST | `/Hump/CreateHumpScheme` | [CreateHumpScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2632) |
| PUT | `/Hump/EditHumpScheme` | [EditHumpScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2753) |
| DELETE | `/Hump/DeleteHumpScheme` | [DeleteHumpScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2798) |
| POST | `/Hump/CopyHumpScheme` | [CopyHumpScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2849) |
| GET | `/Hump/GetHumpCalculations` | [GetHumpCalculations](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2930) |
| POST | `/Hump/CreateHumpCalculation` | [CreateHumpCalculation](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L2973) |
| PUT | `/Hump/EditHumpCalculation` | [EditHumpCalculation](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3028) |
| DELETE | `/Hump/DeleteHumpCalculation` | [DeleteHumpCalculation](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3086) |
| GET | `/Hump/GetHumpCalculationById` | [GetHumpCalculationById](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3138) |
| GET | `/Hump/GetHeadwayCheckSchemes` | [GetHeadwayCheckSchemes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3438) |
| POST | `/Hump/CreateHeadwayCheckScheme` | [CreateHeadwayCheckScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3462) |
| PUT | `/Hump/EditHeadwayCheckScheme` | [EditHeadwayCheckScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3531) |
| DELETE | `/Hump/DeleteHeadwayCheckScheme` | [DeleteHeadwayCheckScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3606) |
| GET | `/Hump/GetHeadwayCheckSchemeById` | [GetHeadwayCheckSchemeById](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3673) |
| GET | `/Hump/CalculateSpeedProfile` | [CalculateSpeedProfile](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3703) |
| GET | `/Hump/CalculateRunningTime` | [CalculateRunningTime](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/HumpController.cs#L3764) |

## 能力实例

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| GET | `/Capacity/GetInstances` | [GetInstances](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityController.cs#L29) |
| GET | `/Capacity/GetInstancePage` | [GetInstancePage](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityController.cs#L58) |
| POST | `/Capacity/CreateInstance` | [CreateInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityController.cs#L109) |
| POST | `/Capacity/CopyCapacityInstance` | [CopyCapacityInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityController.cs#L166) |
| PUT | `/Capacity/EditInstance` | [EditInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityController.cs#L246) |
| DELETE | `/Capacity/DeleteInstance` | [DeleteInstance](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityController.cs#L308) |

## 站场与进路

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| POST | `/StationLayout/GetOperationPlanChartResources` | [GetOperationPlanChartResources](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutChartDataController.cs#L9) |
| POST | `/StationLayout/RepairJson` | [RepairJson](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L62) |
| POST | `/StationLayout/SaveJson` | [SaveJson](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L79) |
| POST | `/StationLayout/GetJson` | [GetJson](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L119) |
| POST | `/StationLayout/SearchRoutes` | [SearchRoutes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L150) |
| GET | `/StationLayout/GetStationRoutes` | [GetStationRoutes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L331) |
| POST | `/StationLayout/GenerateStationRouteInterruptCells` | [GenerateStationRouteInterruptCells](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L366) |
| POST | `/StationLayout/CreateStationRoute` | [CreateStationRoute](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L438) |
| GET | `/StationLayout/GetStationRouteTimes` | [GetStationRouteTimes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L510) |
| POST | `/StationLayout/CreateStationRouteTimes` | [CreateStationRouteTimes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L561) |
| PUT | `/StationLayout/SaveStationRouteTimes` | [SaveStationRouteTimes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L622) |
| PUT | `/StationLayout/BatchSetStationRouteTimes` | [BatchSetStationRouteTimes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L698) |
| PUT | `/StationLayout/EditStationRoute` | [EditStationRoute](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L796) |
| POST | `/StationLayout/GenerateStationRouteDescription` | [GenerateStationRouteDescription](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L893) |
| DELETE | `/StationLayout/DeleteStationRoute` | [DeleteStationRoute](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L943) |
| GET | `/StationLayout/GetStationRouteEnds` | [GetStationRouteEnds](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1001) |
| POST | `/StationLayout/CreateStationRouteEnd` | [CreateStationRouteEnd](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1036) |
| PUT | `/StationLayout/EditStationRouteEnd` | [EditStationRouteEnd](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1102) |
| DELETE | `/StationLayout/DeleteStationRouteEnd` | [DeleteStationRouteEnd](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1181) |
| GET | `/StationLayout/GetStationSchemes` | [GetStationSchemes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1228) |
| POST | `/StationLayout/CreateStationScheme` | [CreateStationScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1256) |
| POST | `/StationLayout/CopyStationScheme` | [CopyStationScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1302) |
| PUT | `/StationLayout/EditStationScheme` | [EditStationScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1325) |
| DELETE | `/StationLayout/DeleteStationScheme` | [DeleteStationScheme](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L1371) |
| POST | `/StationLayout/ExtractDwgFile` | [ExtractDwgFile](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.cs#L3494) |
| POST | `/StationLayout/GenerateStationDwellingRoutes` | [GenerateStationDwellingRoutes](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/StationLayoutController.DwellingRoutes.cs#L9) |

## 作业过程

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| GET | `/OperationProcess/GetTemplates` | [GetTemplates](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L24) |
| GET | `/OperationProcess/GetTemplate` | [GetTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L34) |
| GET | `/OperationProcess/GetCatalog` | [GetCatalog](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L38) |
| POST | `/OperationProcess/CreateTemplate` | [CreateTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L41) |
| PUT | `/OperationProcess/UpdateTemplate` | [UpdateTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L70) |
| DELETE | `/OperationProcess/DeleteTemplate` | [DeleteTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L82) |
| GET | `/OperationProcess/{templateID}/{collection}` | [GetEntities](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L94) |
| GET | `/OperationProcess/{templateID}/{collection}/{id}` | [GetEntity](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L98) |
| POST | `/OperationProcess/{templateID}/{collection}` | [CreateEntity](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L106) |
| PUT | `/OperationProcess/{templateID}/{collection}/{id}` | [UpdateEntity](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L110) |
| DELETE | `/OperationProcess/{templateID}/{collection}/{id}` | [DeleteEntity](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationProcessController.cs#L114) |

## 作业计划

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| POST | `/OperationPlan/DeleteTrains` | [DeleteTrains](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanBatchDeletionController.cs#L9) |
| POST | `/OperationPlan/PreviewCellOccupancyImport` | [PreviewCellOccupancyImport](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanCellOccupancyController.cs#L19) |
| POST | `/OperationPlan/ImportCellOccupancy` | [ImportCellOccupancy](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanCellOccupancyController.cs#L24) |
| POST | `/OperationPlan/RecalculateCellOccupations` | [RecalculateCellOccupations](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanCellOccupationRecalculationController.cs#L10) |
| GET | `/OperationPlan/GetOperationPlans` | [GetOperationPlans](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L29) |
| POST | `/OperationPlan/CreateOperationPlan` | [CreateOperationPlan](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L59) |
| PUT | `/OperationPlan/EditOperationPlan` | [EditOperationPlan](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L101) |
| POST | `/OperationPlan/CopyOperationPlan` | [CopyOperationPlan](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L163) |
| DELETE | `/OperationPlan/DeleteOperationPlan` | [DeleteOperationPlan](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L251) |
| GET | `/OperationPlan/GetTrainTemplates` | [GetTrainTemplates](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L310) |
| POST | `/OperationPlan/CreateTrainTemplate` | [CreateTrainTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L340) |
| PUT | `/OperationPlan/EditTrainTemplate` | [EditTrainTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L390) |
| DELETE | `/OperationPlan/DeleteTrainTemplate` | [DeleteTrainTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L484) |
| GET | `/OperationPlan/GetMovementTemplates` | [GetMovementTemplates](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L545) |
| POST | `/OperationPlan/CreateMovementTemplate` | [CreateMovementTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L592) |
| PUT | `/OperationPlan/EditMovementTemplate` | [EditMovementTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L664) |
| PUT | `/OperationPlan/UpdateMovementTemplateOrder` | [UpdateMovementTemplateOrder](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L753) |
| DELETE | `/OperationPlan/DeleteMovementTemplate` | [DeleteMovementTemplate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L804) |
| GET | `/OperationPlan/GetTrainOperationPlan` | [GetTrainOperationPlan](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L871) |
| POST | `/OperationPlan/GenerateTrainOperationPlan` | [GenerateTrainOperationPlan](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L902) |
| POST | `/OperationPlan/CreateTrain` | [CreateTrain](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L950) |
| PUT | `/OperationPlan/EditTrain` | [EditTrain](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L994) |
| DELETE | `/OperationPlan/DeleteTrain` | [DeleteTrain](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1038) |
| POST | `/OperationPlan/CreateMovement` | [CreateMovement](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1124) |
| PUT | `/OperationPlan/EditMovement` | [EditMovement](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1183) |
| PUT | `/OperationPlan/UpdateMovementOrder` | [UpdateMovementOrder](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1223) |
| DELETE | `/OperationPlan/DeleteMovement` | [DeleteMovement](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1273) |
| GET | `/OperationPlan/GetBottleneckSummaryCategories` | [GetBottleneckSummaryCategories](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1343) |
| PUT | `/OperationPlan/SaveBottleneckSummaryCategories` | [SaveBottleneckSummaryCategories](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1374) |
| GET | `/OperationPlan/GetOperationAnalysisResult` | [GetOperationAnalysisResult](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1409) |
| PUT | `/OperationPlan/SaveOperationAnalysisResult` | [SaveOperationAnalysisResult](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1440) |
| GET | `/OperationPlan/GetOperationOccupationTimeSubTables` | [GetOperationOccupationTimeSubTables](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1476) |
| PUT | `/OperationPlan/SaveOperationOccupationTimeSubTables` | [SaveOperationOccupationTimeSubTables](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanController.cs#L1511) |
| POST | `/OperationPlan/GenerateTrainOperationPlanFromProcess` | [GenerateTrainOperationPlanFromProcess](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanFromProcessController.cs#L13) |
| PUT | `/OperationPlan/EditMovements` | [EditMovements](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanMovementBatchController.cs#L12) |
| POST | `/OperationPlan/GenerateTrainTemplateFromProcess` | [GenerateTrainTemplateFromProcess](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanProcessGenerationController.cs#L11) |
| POST | `/OperationPlan/PreviewProcessTrain` | [PreviewProcessTrain](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanProcessTrainCreationController.cs#L12) |
| POST | `/OperationPlan/CreateProcessTrain` | [CreateProcessTrain](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanProcessTrainCreationController.cs#L15) |
| GET | `/OperationPlan/GetResourceOccupancyCharts` | [GetResourceOccupancyCharts](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanResourceOccupancyController.cs#L12) |
| PUT | `/OperationPlan/SaveResourceOccupancyCharts` | [SaveResourceOccupancyCharts](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanResourceOccupancyController.cs#L35) |
| GET | `/OperationPlan/GetStationPlanCharts` | [GetStationPlanCharts](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanStationChartsController.cs#L12) |
| PUT | `/OperationPlan/SaveStationPlanCharts` | [SaveStationPlanCharts](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanStationChartsController.cs#L41) |
| GET | `/OperationPlan/GetStationPlanViewSettings` | [GetStationPlanViewSettings](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanStationViewController.cs#L12) |
| PUT | `/OperationPlan/SaveStationPlanViewSettings` | [SaveStationPlanViewSettings](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanStationViewController.cs#L35) |
| POST | `/OperationPlan/CreateTrainWithMovements` | [CreateTrainWithMovements](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/OperationPlanTrainCreationController.cs#L10) |

## 计算代理与任务

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| POST | `/api/capacity-agents/authenticate` | [Authenticate](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L44) |
| GET | `/api/capacity-agents` | [GetAgents](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L68) |
| GET | `/api/capacity-agents/presets` | [GetPresets](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L78) |
| POST | `/api/capacity-agents/presets` | [CreatePreset](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L88) |
| PUT | `/api/capacity-agents/presets/{presetId}` | [UpdatePreset](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L95) |
| DELETE | `/api/capacity-agents/presets/{presetId}` | [DeletePreset](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L102) |
| POST | `/api/capacity-agents/preview-input` | [PreviewInput](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L115) |
| POST | `/api/capacity-agents/jobs` | [SubmitJob](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L147) |
| POST | `/api/capacity-agents/saturated-plan-jobs` | [SubmitSaturatedPlanJob](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L180) |
| GET | `/api/capacity-agents/jobs` | [GetJobs](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L262) |
| GET | `/api/capacity-agents/jobs/{jobId}` | [GetJob](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/CapacityAgentsController.cs#L270) |

## 布置图分析

| 方法 | 路径 | 实现操作 |
| --- | --- | --- |
| GET | `/SketchLayout/schemes` | [List](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/SketchLayoutController.cs#L26) |
| GET | `/SketchLayout/schemes/{id}` | [Get](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/SketchLayoutController.cs#L27) |
| POST | `/SketchLayout/schemes` | [Create](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/SketchLayoutController.cs#L28) |
| PUT | `/SketchLayout/schemes/{id}` | [Update](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/SketchLayoutController.cs#L34) |
| DELETE | `/SketchLayout/schemes/{id}` | [Delete](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/SketchLayoutController.cs#L49) |
| POST | `/SketchLayout/schemes/{id}/analysis` | [Analyze](../../SwitchYard.WebApi/SwitchYard.Service/Controllers/SketchLayoutController.cs#L55) |

## 独立车站布置图模块

独立模块的 `MapSwitchYardStationLayout` 另外定义 10 个可挂载操作，默认前缀 `/StationLayout`：`GET GetStationSchemes`、`POST CreateStationScheme`、`POST CopyStationScheme`、`PUT EditStationScheme`、`DELETE DeleteStationScheme`、`POST GetJson`、`POST RepairJson`、`POST SaveJson`、`POST SearchRoutes`、`POST ExtractDwgFile`。

当前主服务通过旧控制器与适配服务承载这些路径，不能在相同前缀下再次注册模块端点。独立宿主接入与契约差异见[开发与接入](09-development-integration.md)和[端点定义](../../SwitchYard.StationLayout/backend/StationLayoutEndpointExtensions.cs)。

## 非 HTTP 操作

`/hubs/capacity-agent` 是 SignalR Hub。代理调用 `RegisterAgent`、`ReportStatus`、`ReportProgress`、`CompleteJob`，服务端向代理发送 `Solve`。这些 Hub 方法不应按普通 `POST /方法名` 调用。
