# SwitchYard.StationLayout 接入指南

本文说明其他程序如何复用 `SwitchYard.StationLayout` 的前端页面和后端业务能力。适用于：

- SwitchYard 自身；
- NSTD 等完全离线、独立部署的程序；
- 具有自己认证体系、数据库和项目/实例模型的其他 ASP.NET Core + Vue 3 程序。

## 1. 模块边界

目录结构：

```text
SwitchYard.StationLayout/
├── backend/   # .NET 共享包
├── frontend/  # Vue 共享包
├── INTEGRATION_GUIDE.zh-CN.md
└── README.md
```

共享模块提供：

- 站场图完整 Vue 页面、编辑器、工具栏、样式和图元资源；
- 前后端 DTO 与 `StationLayoutGateway` 契约；
- 布局规范化、校验、拓扑和路径搜索；
- DWG 解析；
- 方案管理、载入、保存、路径搜索等应用服务；
- 8 个兼容 SwitchYard 旧接口的 ASP.NET Core Endpoint。

共享模块不负责：

- 不连接或直接读写数据库；
- 不规定宿主表名或数据库驱动；
- 不读取宿主连接字符串；
- 不包含 JWT、Cookie、用户表或项目权限实现；
- 不依赖正在运行的 SwitchYard 服务；
- 不在不同程序之间同步数据库。

因此，每个程序运行时可以完全独立。宿主只复用版本化的 npm/NuGet 构建产物，并实现自己的 Gateway、Repository 和 Authorization。

## 2. 推荐接入结构

```text
宿主 Vue 页面
  └── @switchyard/station-layout
        └── 宿主 StationLayoutGateway
              └── 宿主 /StationLayout HTTP 接口
                    └── SwitchYard.StationLayout 后端应用服务
                          ├── 宿主 IStationLayoutRepository
                          ├── 宿主 IStationLayoutAuthorization
                          ├── 宿主 IStationLayoutIdGenerator
                          └── 可选 IStationLayoutArtifactSink
```

前端和后端应使用相同版本号，例如 `0.1.0-alpha.2`。不要覆盖已经发布的同版本制品；升级时发布新版本，并同时升级 npm 与 NuGet 引用。

## 3. 后端接入

### 3.1 运行环境

后端项目位于：

```text
backend/SwitchYard.StationLayout.csproj
```

当前同时提供：

- `net8.0`
- `net10.0`

模块使用 `Microsoft.AspNetCore.App`，DWG 解析依赖 `ACadSharp`。模块不引用 Dapper、EF Core、SQLite 或 MySQL 驱动。

### 3.2 引用方式

开发阶段可使用 `ProjectReference`；以下相对路径仅为示例，需按宿主 `.csproj` 的位置调整：

```xml
<ItemGroup>
  <ProjectReference Include="..\SwitchYard.StationLayout\backend\SwitchYard.StationLayout.csproj" />
</ItemGroup>
```

其他仓库或离线交付建议使用固定版本 NuGet：

```xml
<ItemGroup>
  <PackageReference Include="SwitchYard.StationLayout" Version="0.1.0-alpha.2" />
</ItemGroup>
```

离线源可以放在宿主仓库的 `vendor/nuget`：

```xml
<PropertyGroup>
  <RestoreAdditionalProjectSources>
    $(MSBuildProjectDirectory)\..\vendor\nuget;$(RestoreAdditionalProjectSources)
  </RestoreAdditionalProjectSources>
</PropertyGroup>
```

生成 NuGet 包：

```powershell
dotnet pack backend\SwitchYard.StationLayout.csproj -c Release -o artifacts\nuget
```

### 3.3 宿主必须实现的接口

#### IStationLayoutRepository

宿主负责把完整布局聚合保存到自己的数据库：

```csharp
public interface IStationLayoutRepository
{
    Task<IReadOnlyList<StationSchemeRecord>> ListSchemesAsync(
        string scopeId, CancellationToken cancellationToken);

    Task<StationLayoutRecord?> LoadAsync(
        string scopeId, string? requestedSchemeId, CancellationToken cancellationToken);

    Task<bool> SchemeExistsAsync(
        string scopeId, string schemeId, CancellationToken cancellationToken);

    Task<bool> TryCreateSchemeAsync(
        StationSchemeRecord scheme, CancellationToken cancellationToken);

    Task<bool> TryCopySchemeAsync(
        string sourceSchemeId, StationSchemeRecord targetScheme, CancellationToken cancellationToken);

    Task<bool> RenameSchemeAsync(
        string scopeId, string schemeId, string name, CancellationToken cancellationToken);

    Task<bool> DeleteSchemeAsync(
        string scopeId, string schemeId, CancellationToken cancellationToken);

    Task<StationLayoutWriteResult> ReplaceLayoutAsync(
        StationLayoutWriteRequest request, CancellationToken cancellationToken);
}
```

实现要求：

1. `LoadAsync` 和 `ListSchemesAsync` 必须返回脱离数据库跟踪的快照。
2. `requestedSchemeId == null` 时，由宿主按自己的规则解析默认方案。
3. `ReplaceLayoutAsync` 必须在一个数据库事务中原子替换完整布局。
4. 请求包含 `ExpectedRevision` 时，必须在同一事务内比较修订号；不一致时抛出 `StationLayoutConflictException`。
5. 保存成功后修订号只增加一次。
6. `DeleteSchemeAsync` 应原子删除方案及其全部布局数据。
7. 数据库错误应继续抛出，或包装为 `StationLayoutStoreException`；不要吞掉异常后返回成功。
8. `TryCopySchemeAsync` 应在一个事务内复制方案及其关联数据，新方案从修订号 0 开始。目标 ID 已存在时返回 `false`，源方案不存在时抛出 `StationLayoutNotFoundException`。内嵌 JSON 的方案归属也应更新为副本。

最简单的宿主存储可以只保存一个 JSON 聚合：

```sql
CREATE TABLE app_station_layout (
    scope_id       VARCHAR(128) NOT NULL,
    scheme_id      VARCHAR(128) NOT NULL,
    name           VARCHAR(100) NOT NULL,
    revision       BIGINT NOT NULL,
    is_default     INTEGER NOT NULL,
    document_json  TEXT NOT NULL,
    PRIMARY KEY (scope_id, scheme_id)
);
```

如果宿主还需要查询单个图元，可以在同一事务内维护自己的投影表。投影表结构、迁移脚本和表前缀都属于宿主，不应放进共享模块。

#### IStationLayoutAuthorization

模块定义四种权限：

| 权限 | 用途 |
|---|---|
| `View` | 查看方案、载入布局、路径搜索 |
| `ManageSchemes` | 新建、改名、删除方案 |
| `SaveLayout` | 保存完整布局 |
| `ImportDwg` | 上传并解析 DWG |

宿主应把自己的用户、角色、项目或实例权限映射为：

- `Allowed`
- `Unauthenticated`
- `Forbidden`
- `NotFound`

示例：

> 下面的 `ScopeExistsAsync` 和 `IsScopeOwnerAsync` 代表宿主自己的项目/实例查询服务，不是共享模块提供的 API。

```csharp
public sealed class AppStationLayoutAuthorization : IStationLayoutAuthorization
{
    public async ValueTask<StationLayoutAccessDecision> AuthorizeScopeAsync(
        ClaimsPrincipal user,
        string scopeId,
        StationLayoutPermission permission,
        CancellationToken cancellationToken)
    {
        if (user.Identity?.IsAuthenticated != true)
            return StationLayoutAccessDecision.Unauthenticated();

        var scopeExists = await ScopeExistsAsync(scopeId, cancellationToken);
        if (!scopeExists)
            return StationLayoutAccessDecision.Missing();

        var canManage = user.IsInRole("Admin") || await IsScopeOwnerAsync(
            user, scopeId, cancellationToken);

        var requiresWrite = permission is StationLayoutPermission.ManageSchemes
            or StationLayoutPermission.SaveLayout
            or StationLayoutPermission.ImportDwg;

        return !requiresWrite || canManage
            ? StationLayoutAccessDecision.Allow()
            : StationLayoutAccessDecision.Forbid();
    }

    public ValueTask<StationLayoutAccessDecision> AuthorizeGlobalAsync(
        ClaimsPrincipal user,
        StationLayoutPermission permission,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(user.IsInRole("Admin")
            ? StationLayoutAccessDecision.Allow()
            : StationLayoutAccessDecision.Forbid());
}
```

#### IStationLayoutIdGenerator

方案 ID 由宿主生成：

```csharp
public sealed class AppStationLayoutIdGenerator : IStationLayoutIdGenerator
{
    public string NextId() => Guid.NewGuid().ToString("N");
}
```

ID 必须非空，并在同一 scope 内保持唯一。宿主也可以包装已有 Snowflake 生成器。

#### IStationLayoutArtifactSink（可选）

DWG 解析完成后，模块会把布局交给该接口。需要生成审计文件或其他宿主副作用时实现它；不注册时使用无操作实现。

### 3.4 注册服务与映射接口

```csharp
using SwitchYard.StationLayout;

var builder = WebApplication.CreateBuilder(args);

// 宿主自行配置 JWT、Cookie 或其他认证方案。
builder.Services.AddAuthentication(/* host scheme */);
builder.Services.AddAuthorization();

builder.Services.AddScoped<IStationLayoutRepository, AppStationLayoutRepository>();
builder.Services.AddScoped<IStationLayoutAuthorization, AppStationLayoutAuthorization>();
builder.Services.AddSingleton<IStationLayoutIdGenerator, AppStationLayoutIdGenerator>();
// 可选：
// builder.Services.AddSingleton<IStationLayoutArtifactSink, AppStationLayoutArtifactSink>();

builder.Services.AddSwitchYardStationLayout(options =>
{
    options.LegacyV1Compatibility = true;
    options.DefaultSchemeId = "station_layout_scheme";
    options.DefaultSchemeName = "车站布置图";
    options.MaximumSchemeNameLength = 100;
    options.TopologyRepairTolerance = 1; // 布置图坐标单位，修复绑定时的位置容差
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapSwitchYardStationLayout("/StationLayout");

app.Run();
```

`MapSwitchYardStationLayout` 会对整个路由组调用 `RequireAuthorization()`，但不会替宿主选择认证方案。

如果旧程序已经有相同路径的 Controller，不要同时映射共享 Endpoint，否则会出现路由冲突。过渡期可以保留旧 Controller，把 Action 改成调用 `IStationLayoutService`；完成差异测试后再切换到共享 Endpoint。

### 3.5 scopeId 的定义

`scopeId` 是宿主定义的不透明字符串，前端属性名为 `selectedInstanceId`，HTTP 兼容参数名为 `instanceID`。

示例：

- SwitchYard：`CapacityInstanceID`
- NSTD：车站 ID；项目 ID 从可信的服务端项目上下文取得
- 其他程序：场站 ID、设计项目 ID 或租户内资源 ID

不要默认不同程序的 ID 相等，也不要信任浏览器传入的项目/租户 ID。需要项目隔离时，Repository 应将可信项目上下文与 `scopeId` 共同作为数据库键。

## 4. HTTP 接口

默认路由前缀为 `/StationLayout`：

| 方法 | 路径 | 权限 | 主要输入 |
|---|---|---|---|
| GET | `/GetStationSchemes` | View | query `instanceID` |
| POST | `/CreateStationScheme` | ManageSchemes | JSON `instanceID`, `name` |
| POST | `/CopyStationScheme` | ManageSchemes | JSON `instanceID`, `sourceStationSchemeID`, optional `name` |
| PUT | `/EditStationScheme` | ManageSchemes | JSON `instanceID`, `originalID`, `name` |
| DELETE | `/DeleteStationScheme` | ManageSchemes | query `instanceID`, `stationSchemeID` |
| POST | `/GetJson` | View | query `instanceID`, optional `stationSchemeID` |
| POST | `/RepairJson` | View | body `instanceID`, `json`；校验并预览修复，不写数据库 |
| POST | `/SaveJson` | SaveLayout | query/body scope，body `json`, optional revision |
| POST | `/SearchRoutes` | View | query/body scope，起止节点 ID |
| POST | `/ExtractDwgFile` | ImportDwg | multipart `file`, `layerName` |

`GetJson` 返回 `StationLayoutDocument`，并设置：

- `ETag: "<revision>"`
- `X-Station-Layout-Revision: <revision>`
- `X-Station-Layout-Exists: true|false`

`SaveJson` 会先检查绑定关系，发现不一致时按节点、线段端点和设备的位置尝试修复，然后重新校验。
位置匹配必须唯一；已有有效引用及设备的显示偏移保持不变。节点邻接表从线段端点重建，道岔分支按方向匹配，
曲线按顶点及切点匹配。Cell 引用的旧线段仅在原邻接表仍能确定两个端点、且存在唯一共线线段链时替换。
无法定位或存在多个候选时返回 400，整份布局不会保存；乐观并发检查仍然有效。

`RepairJson` 返回 `{ json, repairCount, repairs }`，其中 `repairs` 为已修改的字段路径。
保存响应新增 `repairCount`、`repairs` 和 `repairedJson`；只有发生修复时才返回修复后的 JSON。
响应还包含 `savedJson`（最终保存的数据）和 `idMappings`（按 `nodes`、`tracks`、`signals` 等集合划分的旧 ID 到新 ID 字典）。
SwitchYard 宿主在同一保存事务内，为目标方案中尚未保存的对象分配雪花字符串 ID，并同步替换端点、设备、曲线、道岔分支和 Cell 引用；已有对象的 ID 保持不变。
前端必须应用这些映射，包括当前选中对象和撤销/重做历史。保存期间发生的新编辑保留，只更新其 ID 引用。
`SearchRoutes` 的 `startNodeId`、`endNodeId`、返回的 `nodeIds` 和 `linkIds` 均为字符串，禁止使用 `Number` 或整数解析，以免雪花 ID 丢失精度。

SwitchYard 数据库以对象关系表为唯一来源，读取时在同一事务中临时组装布置图 JSON。
启动迁移将旧整数 ID 列改为字符串；有历史存档时恢复原始 ID 并更新已有进路、业务文档的节点和线段引用，成功后删除 `stationscheme.LayoutDocument`。
历史拓扑存在无法确定的冲突时停止迁移，保留原数据和历史存档。
对象的额外绘图属性保存在各自数据表的 `ExtraProperties` 中，已映射到列的 ID、绑定和几何字段不会重复存入此字段。
`stationscheme.LayoutMetadata` 和 `LayoutExtensions` 只保存元数据及根级扩展，不包含节点、线段或设备集合。

前端宿主实现可选的 `StationLayoutGateway.repairJson` 后，导入和载入也会调用此接口，并显示修复提示。
保存时由后端在写入前修复，成功后前端应用 `repairedJson`；修复期间产生的新编辑会保留。
导入及载入的修复仅影响画布，需要保存才会持久化。未实现该可选方法的旧宿主继续使用原有前端严格校验。

保存时前端应把读取到的修订号作为以下任一种形式发送：

- JSON body 的 `expectedRevision`
- 强 ETag：`If-Match: "<revision>"`

修订冲突返回 HTTP 409。其他状态映射：

- 400：请求或布局校验失败
- 401：未认证
- 403：无权限
- 404：scope 或方案不存在
- 409：方案/修订冲突
- 500：宿主存储或未处理错误

DWG 默认最大 20 MB，也不能通过参数提高到 20 MB 以上。

如果前端与后端跨域部署，CORS 必须暴露修订响应头：

```csharp
policy.WithExposedHeaders(
    "ETag",
    "X-Station-Layout-Revision",
    "X-Station-Layout-Exists");
```

## 5. 前端接入

### 5.1 运行环境

前端包名：

```text
@switchyard/station-layout
```

Peer dependencies：

- Vue `>=3.5.0 <4`
- Element Plus `>=2.10.0 <3`
- `@element-plus/icons-vue >=2.3.0 <3`

宿主只应存在一份 Vue。Vite 项目建议配置：

```ts
resolve: {
  dedupe: ["vue", "element-plus", "@element-plus/icons-vue"],
}
```

### 5.2 安装方式

同仓库开发可以使用：

```json
{
  "dependencies": {
    "@switchyard/station-layout": "file:../SwitchYard.StationLayout/frontend"
  }
}
```

跨仓库和离线交付建议使用 `.tgz`：

```powershell
cd frontend
npm run test:contract
npm run type-check
npm run build
npm pack
```

宿主将审核后的 `.tgz` 放入自己的 `vendor/npm`，并固定文件名：

```json
{
  "dependencies": {
    "@switchyard/station-layout": "file:../vendor/npm/switchyard-station-layout-0.1.0-alpha.2.tgz"
  }
}
```

应提交 lockfile，并记录 `.tgz` 的 SHA-256。

### 5.3 实现 StationLayoutGateway

共享组件不导入宿主 Axios、认证 Store 或固定 URL。宿主必须实现以下 8 个方法：

```ts
export interface StationLayoutGateway {
  getStationSchemes(request): Promise<StationScheme[]>;
  createStationScheme(request): Promise<StationScheme>;
  editStationScheme(request): Promise<StationScheme>;
  deleteStationScheme(request): Promise<void>;
  getJson(request): Promise<StationLayoutDocument>;
  saveJson(request): Promise<SaveJsonResult>;
  searchRoutes(request): Promise<SearchRoutesResult>;
  extractDwgFile(request): Promise<ExtractDwgFileResult>;
}
```

Gateway 应复用宿主已有 HTTP 客户端，以继承登录令牌、刷新令牌、项目上下文和统一错误处理。

示例骨架：

```ts
import type {
  StationLayoutDocument,
  StationLayoutGateway,
} from "@switchyard/station-layout";
import axios from "@/utils/axios";

const base = "/StationLayout";

function readRevision(headers: Record<string, unknown>): number | undefined {
  const raw = headers["x-station-layout-revision"] ?? headers.etag;
  const value = Number(String(raw ?? "").replace(/^W\//i, "").replaceAll('"', ""));
  return Number.isSafeInteger(value) && value >= 0 ? value : undefined;
}

export const stationLayoutGateway: StationLayoutGateway = {
  async getStationSchemes({ instanceId }) {
    const { data } = await axios.get(`${base}/GetStationSchemes`, {
      params: { instanceID: instanceId },
    });
    return data;
  },

  async createStationScheme({ instanceId, name }) {
    const { data } = await axios.post(`${base}/CreateStationScheme`, {
      instanceID: instanceId,
      name,
    });
    return data;
  },

  async copyStationScheme({ instanceId, sourceStationSchemeId, name }) {
    const { data } = await axios.post(`${base}/CopyStationScheme`, {
      instanceID: instanceId,
      sourceStationSchemeID: sourceStationSchemeId,
      name,
    });
    return data;
  },

  async editStationScheme({ instanceId, originalId, name }) {
    const { data } = await axios.put(`${base}/EditStationScheme`, {
      instanceID: instanceId,
      originalID: originalId,
      name,
    });
    return data;
  },

  async deleteStationScheme({ instanceId, stationSchemeId }) {
    await axios.delete(`${base}/DeleteStationScheme`, {
      params: { instanceID: instanceId, stationSchemeID: stationSchemeId },
    });
  },

  async getJson({ instanceId, stationSchemeId }) {
    const response = await axios.post<StationLayoutDocument>(
      `${base}/GetJson`,
      null,
      {
        params: {
          instanceID: instanceId,
          ...(stationSchemeId ? { stationSchemeID: stationSchemeId } : {}),
        },
      },
    );
    const revision = readRevision(response.headers);
    return revision === undefined
      ? response.data
      : {
          ...response.data,
          metadata: { ...response.data.metadata, revision },
        };
  },

  async saveJson(request) {
    const response = await axios.post(
      `${base}/SaveJson`,
      {
        json: request.json,
        instanceID: request.instanceId,
        stationSchemeID: request.stationSchemeId,
        expectedRevision: request.expectedRevision,
      },
      {
        params: {
          instanceID: request.instanceId,
          ...(request.stationSchemeId
            ? { stationSchemeID: request.stationSchemeId }
            : {}),
        },
        headers: request.expectedRevision === undefined
          ? undefined
          : { "If-Match": `"${request.expectedRevision}"` },
      },
    );
    return {
      ...response.data,
      revision: response.data.revision ?? readRevision(response.headers),
    };
  },

  async searchRoutes(request) {
    const { data } = await axios.post(`${base}/SearchRoutes`, {
      instanceID: request.instanceId,
      stationSchemeID: request.stationSchemeId,
      startNodeId: request.startNodeId,
      endNodeId: request.endNodeId,
    });
    return data;
  },

  async extractDwgFile({ file, layerName }) {
    const form = new FormData();
    form.append("file", file);
    form.append("layerName", layerName || "0");
    const { data } = await axios.post(`${base}/ExtractDwgFile`, form);
    return data;
  },
};
```

生产代码还应统一处理后端 PascalCase/camelCase 差异和 `problem+json`/纯文本错误。

### 5.4 挂载完整页面

```vue
<script setup lang="ts">
import {
  StationLayout,
  createStationLayoutTranslator,
} from "@switchyard/station-layout";
import "@switchyard/station-layout/style.css";
import { computed, ref } from "vue";
import { stationLayoutGateway } from "./stationLayoutGateway";

const selectedScopeId = ref("");
const readonly = computed(() => false); // 根据宿主权限计算
const translate = createStationLayoutTranslator("zh");

function formatError(error: unknown, fallback: string): string {
  // 使用宿主统一错误格式化器。
  return error instanceof Error && error.message ? error.message : fallback;
}
</script>

<template>
  <div class="station-layout-host">
    <StationLayout
      :key="selectedScopeId"
      :selected-instance-id="selectedScopeId"
      :gateway="stationLayoutGateway"
      :translate="translate"
      :format-error="formatError"
      :readonly="readonly"
    />
  </div>
</template>

<style scoped>
.station-layout-host {
  width: 100%;
  height: calc(100vh - 100px);
  min-height: 640px;
  overflow: hidden;
}
</style>
```

属性说明：

| 属性 | 必填 | 说明 |
|---|---|---|
| `selectedInstanceId` | 否 | 当前宿主 scope ID；为空时显示未选择状态 |
| `gateway` | 是 | 宿主的 9 方法 Gateway |
| `translate` | 否 | 翻译函数；不传时使用内置中文 |
| `formatError` | 否 | 宿主错误格式化函数 |
| `readonly` | 否 | 只读模式；仍允许查看、载入和导出 |

样式文件必须显式导入。宿主容器也必须有明确高度，否则编辑区可能计算为零高度。

项目和 scope 同时参与页面隔离时，推荐使用：

```vue
:key="`${projectId}:${selectedScopeId}`"
```

## 6. 数据库与迁移原则

1. 数据库表、DDL 和迁移程序全部放在宿主项目。
2. 表名应使用独立前缀，避免与宿主已有 `stationroute`、`train`、`node` 等表冲突。
3. 不允许两个程序运行时共享同一组表或互相直接写库。
4. 不要在 HTTP 请求处理中执行 `CREATE TABLE` 或 `ALTER TABLE`。
5. 新版本迁移必须有版本号和 checksum；不要修改已经部署过的旧迁移内容。
6. 项目或 scope 删除时，宿主应显式清理对应站场图数据。
7. 两个独立程序之间需要传递布局时，使用带 `schemaVersion` 的 JSON 导出/导入，不做跨库 SQL 同步。

## 7. 发布与升级

推荐一次发布同时生成：

```text
SwitchYard.StationLayout.<version>.nupkg
switchyard-station-layout-<version>.tgz
SHA256SUMS.txt
```

发布流程：

1. 更新前后端相同版本号。
2. 运行后端 net8/net10 构建和契约测试。
3. 运行前端 `test:contract`、`type-check`、生产构建。
4. 生成 NuGet 和 tgz。
5. 计算 SHA-256 并提交 checksum。
6. 在宿主中显式升级引用和 lockfile。
7. 用真实制品完成一次宿主构建与冒烟测试。
8. 不覆盖同版本文件；需要修复时发布新版本。

升级前端本地包后应重启 Vite。只刷新数据不会替换浏览器内已经加载的旧组件代码；必要时清理 Vite 的依赖预打包缓存后重新启动。

## 8. 验收清单

后端：

- [ ] 空数据库迁移成功，失败时宿主启动失败而不是继续运行
- [ ] 方案新增、查询、改名、删除正常
- [ ] 完整布局保存和载入逐字段一致
- [ ] 同一 scope 的并发保存返回 409，不发生静默覆盖
- [ ] 不同项目、scope、方案之间严格隔离
- [ ] 项目/scope 删除能够清理宿主数据
- [ ] View/ManageSchemes/SaveLayout/ImportDwg 权限分别验证
- [ ] DWG 大小限制与文件错误验证

前端：

- [ ] 显式加载共享 CSS
- [ ] 8 个 Gateway 方法均实现
- [ ] `GetJson` 修订响应头写入 `metadata.revision`
- [ ] `SaveJson` 发送 `expectedRevision` 或 `If-Match`
- [ ] 当前 scope 切换后不会显示上一 scope 的布局
- [ ] 只读用户可以载入和查看，但不能修改
- [ ] 保存后重新载入，轨道、节点、道岔和支线仍正常显示
- [ ] 前端生产构建没有重复 Vue 实例

## 9. 常见问题

### 页面没有样式

确认已经执行：

```ts
import "@switchyard/station-layout/style.css";
```

### 页面空白但接口返回了数据

检查宿主容器高度、当前 scope、方案 ID、编辑器缩放/滚动位置，以及运行中的 Vite 是否仍缓存旧包。切换项目或 scope 时建议使用稳定的组合 `key`。

### GetJson 成功但保存总是 409

检查 Gateway 是否读取 `ETag` 或 `X-Station-Layout-Revision`，并在保存时发送相同修订号。保存成功后应使用响应中的新 revision。

### 请求返回 401

共享 Endpoint 已要求认证。确认宿主先配置并调用 `UseAuthentication()`，Gateway 复用了能附加登录凭据的宿主 HTTP 客户端。

### 请求返回 403 或 404

检查 `IStationLayoutAuthorization` 的权限映射，以及 Repository 中 `scopeId` 与可信项目/租户上下文的组合方式。

### 前端出现 duplicated Vue、ref 或注入异常

将 Vue、Element Plus 和 icons 保持为宿主依赖，并在 Vite 中配置 `resolve.dedupe`；不要直接从另一个仓库的任意 `.vue` 文件路径导入。

## 10. 仓库内参考实现

- SwitchYard 后端宿主适配：`../SwitchYard.WebApi/SwitchYard.Service/StationLayout/`
- SwitchYard 前端 Gateway：`../switchyard-vue/src/capacity/switchyardStationLayoutGateway.ts`
- 共享后端测试：`../SwitchYard.WebApi/SwitchYard.StationLayout.SmokeTests/`
- SwitchYard 宿主集成测试：`../SwitchYard.WebApi/SwitchYard.Service.StationLayout.IntegrationTests/`

新宿主应参考这些实现的契约和测试方式，但必须保留自己的认证、数据库和迁移边界。
