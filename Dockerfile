# syntax=docker/dockerfile:1
# Read only the application version from the original configuration. The bind
# mount is read-only and does not store the source configuration in image layers.
FROM --platform=$BUILDPLATFORM node:24-bookworm-slim AS configuration
WORKDIR /config
COPY deploy/docker/api.appsettings.json ./appsettings.json
RUN --mount=type=bind,source=SwitchYard.WebApi/SwitchYard.Service/appsettings.json,target=/source-appsettings.json \
    node --input-type=module <<'JS'
import { readFileSync, writeFileSync } from 'node:fs';
const source = JSON.parse(readFileSync('/source-appsettings.json', 'utf8'));
const container = JSON.parse(readFileSync('appsettings.json', 'utf8'));
if (Object.hasOwn(source.App ?? {}, 'Version')) {
    container.App = { ...container.App, Version: source.App.Version };
}
writeFileSync('appsettings.json', JSON.stringify(container, null, 2) + '\n');
JS

# Build from the repository root so the station-layout project reference is available.
# SDK 10 is needed to restore the station-layout project's net8.0/net10.0 targets;
# the web service itself is published for .NET 8.
# Publish portable managed assemblies; the runtime stage selects the target CPU.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj SwitchYard.WebApi/SwitchYard.Service/
COPY SwitchYard.WebApi/SwitchYard.Capacity/SwitchYard.Capacity.csproj SwitchYard.WebApi/SwitchYard.Capacity/
COPY SwitchYard.WebApi/SwitchYard.Hump/SwitchYard.Hump.csproj SwitchYard.WebApi/SwitchYard.Hump/
COPY SwitchYard.WebApi/SwitchYard.SketchLayout/SwitchYard.SketchLayout.csproj SwitchYard.WebApi/SwitchYard.SketchLayout/
COPY SwitchYard.StationLayout/backend/SwitchYard.StationLayout.csproj SwitchYard.StationLayout/backend/
RUN dotnet restore SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj

COPY --exclude=appsettings.json SwitchYard.WebApi/SwitchYard.Service/ SwitchYard.WebApi/SwitchYard.Service/
COPY SwitchYard.WebApi/SwitchYard.Capacity/ SwitchYard.WebApi/SwitchYard.Capacity/
COPY SwitchYard.WebApi/SwitchYard.Hump/ SwitchYard.WebApi/SwitchYard.Hump/
COPY SwitchYard.WebApi/SwitchYard.SketchLayout/ SwitchYard.WebApi/SwitchYard.SketchLayout/
COPY SwitchYard.StationLayout/backend/ SwitchYard.StationLayout/backend/
COPY SwitchYard.StationLayout/README.md SwitchYard.StationLayout/README.md
COPY LICENSE ./LICENSE
COPY scripts/deploy/ scripts/deploy/
RUN test ! -e SwitchYard.WebApi/SwitchYard.Service/appsettings.json \
    && dotnet publish SwitchYard.WebApi/SwitchYard.Service/SwitchYard.Service.csproj \
    -c Release -f net8.0 --no-restore -p:UseAppHost=false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out/ ./
# Generated container settings retain the original App:Version, without copying
# development database credentials or replacing the version with an image tag.
COPY --from=configuration /config/appsettings.json ./appsettings.json
RUN mkdir -p /app/logs /app/LocalData /app/migration-backups /data /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /app/logs /app/LocalData /app/migration-backups /data /home/app/.aspnet
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    WebApi__Hosts__0=http://0.0.0.0:8080
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=120s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 4 http://127.0.0.1:8080/api/System/version > /dev/null || exit 1
ENTRYPOINT ["dotnet", "SwitchYard.Service.dll"]
