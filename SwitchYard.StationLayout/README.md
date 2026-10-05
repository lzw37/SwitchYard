# SwitchYard.StationLayout

其他程序的完整接入步骤见 [中文接入指南](./INTEGRATION_GUIDE.zh-CN.md)。

`SwitchYard.StationLayout` is the single reusable station-layout module shared by
SwitchYard and offline NSTD deployments. It contains the compatible document and
HTTP contracts, layout validation, route search, DWG extraction, application
service, and the nine legacy-compatible endpoint mappings.

Layout JSON archives use `format: "switchyard.station-layout"` and
`formatVersion: 1`. The frontend exposes the same complete import/export codec to
file actions and host applications. Archives preserve all ten element collections,
geometry, topology, display/grid settings and extension fields. Importing binds the
document to the currently selected destination scheme; source revision numbers do
not replace the destination's optimistic concurrency token.

The SwitchYard host stores the complete archive in `stationscheme.LayoutDocument`
within the same transaction as its legacy relational projection and revision.
Existing databases acquire this nullable column during schema initialization;
unversioned layouts continue to use the legacy loading path. Other repository
implementations must retain the complete document to provide the same fidelity.
Use `StationLayoutDocument.FromJson()` / `ToJson()` when persisting or cloning
archives: they retain the original JSON shape, including omitted optional fields.
The relational route-analysis APIs still require integer node/track identifiers;
archive storage itself preserves arbitrary string identifiers.

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
