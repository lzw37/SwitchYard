# SwitchYard.StationLayout

其他程序的完整接入步骤见 [中文接入指南](./INTEGRATION_GUIDE.zh-CN.md)。

`SwitchYard.StationLayout` is the single reusable station-layout module shared by
SwitchYard and offline NSTD deployments. It contains the compatible document and
HTTP contracts, layout validation, route search, DWG extraction, application
service, and the legacy-compatible endpoint mappings.

Layout JSON archives use `format: "switchyard.station-layout"` and
`formatVersion: 1`. The frontend exposes the same complete import/export codec to
file actions and host applications. Archives preserve all ten element collections,
geometry, topology, display/grid settings and extension fields. Importing binds the
document to the currently selected destination scheme; source revision numbers do
not replace the destination's optimistic concurrency token.

The SwitchYard host stores each object in its relational table and assembles JSON
on demand in a consistent read transaction. It does not store a complete layout
snapshot. Node, track and endpoint IDs are strings throughout persistence, route
search and rendering, including IDs with leading zeros or values beyond JavaScript's
safe integer range. Never convert an ID to a number.

On save, the host assigns Snowflake string IDs to objects not already persisted in
the destination scheme. Existing objects keep their identities. It rewrites all
bindings in the same transaction, checks the revision, and returns `savedJson` and
per-collection `idMappings`. The editor acknowledges those IDs in the canvas,
selection, Cells and undo/redo history, including edits made during the save.
The read-only ID field displays the server-assigned identity.

Schema initialization upgrades existing integer ID columns to text, restores
original IDs from historical archives, updates route/process references, and then
drops `stationscheme.LayoutDocument`. Conflicting historical topology stops the
migration without discarding the archive. Geometry and device positions have their
own columns; `ExtraProperties` contains only unmapped object attributes.
`LayoutMetadata` and `LayoutExtensions` contain scheme metadata and root extensions,
never object collections or a second copy of relational bindings.

Incoming saves attempt unambiguous spatial repairs of dangling topology references
before persistence. `RepairJson` offers the same validation and repair without writing
data, for imports and loads. Repair reports identify changed fields; ambiguous or
unresolvable bindings reject the whole save. The default positional tolerance is one
layout coordinate unit (`TopologyRepairTolerance`). Valid bindings and geometry are
preserved. See the integration guide for the optional frontend gateway method.

Run backend repair regression tests with
`dotnet run --project SwitchYard.StationLayout/tests/SwitchYard.StationLayout.Tests.csproj`
from the repository root, and frontend tests with `npm run test:runtime` in `frontend/`.
Run relational migration/save/search integration tests with
`dotnet run --project SwitchYard.WebApi/SwitchYard.Service.Tests/SwitchYard.Service.Tests.csproj`.
They create and remove isolated temporary databases and do not read appsettings.

The module lives at the repository root beside `switchyard-vue`; it is deliberately
kept outside the backend-only `SwitchYard.WebApi` directory. Its source is split by
runtime boundary:

- `frontend/`: the Vue package consumed by `switchyard-vue` and offline NSTD;
- `backend/`: the multi-target .NET package consumed by each backend host.

The module never opens a database connection, executes SQL, reads a connection
string, or assumes a host table name. Every host must provide:

- `IStationLayoutRepository`, including transactional compare-and-replace behavior;
- `IStationLayoutAuthorization` for host-specific scope permissions;
- `IStationLayoutIdGenerator` for host-owned identifiers;
- optionally `IStationLayoutArtifactSink` for post-DWG host side effects.

Register and map it with:

```csharp
builder.Services.AddSwitchYardStationLayout(options =>
{
    options.LegacyV1Compatibility = true;
});

app.MapSwitchYardStationLayout("/StationLayout");
```

The same module contains one frontend package under `frontend/`. That package
is versioned together with this assembly and injects a `StationLayoutGateway`
instead of importing a host Axios/auth singleton.
