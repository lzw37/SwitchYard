# syntax=docker/dockerfile:1
# Run from the repository root; the local station-layout package is outside switchyard-vue.
FROM --platform=$BUILDPLATFORM node:24-bookworm-slim AS build
WORKDIR /src/switchyard-vue

COPY switchyard-vue/package.json switchyard-vue/package-lock.json ./
COPY SwitchYard.StationLayout/frontend/package.json /src/SwitchYard.StationLayout/frontend/package.json
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund

# Vite and TypeScript resolve station-layout directly to this sibling source directory.
COPY SwitchYard.StationLayout/frontend/ /src/SwitchYard.StationLayout/frontend/
COPY switchyard-vue/ ./
# The runtime config selects the browser's origin unless an API origin is explicitly supplied.
# Keep the image's build-time fallback independent of the developer's production server.
RUN node --input-type=module -e "import { writeFileSync } from 'node:fs'; writeFileSync('src/config.production.json', JSON.stringify({ serverurl: '' }) + '\n');"
# Vite build defaults to production; this script also runs vue-tsc.
RUN npm run build

FROM nginx:stable-alpine AS runtime
RUN apk add --no-cache jq
ENV SWITCHYARD_BACKEND_URL=http://api:8080 \
    SWITCHYARD_API_BASE_URL= \
    NGINX_ENVSUBST_FILTER=^SWITCHYARD_BACKEND_URL$

COPY switchyard-vue/docker/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/switchyard-vue/dist/ /usr/share/nginx/html/
COPY --chmod=755 switchyard-vue/docker/40-switchyard-runtime-config.sh /docker-entrypoint.d/40-switchyard-runtime-config.sh
# Windows checkouts may use CRLF; make the Linux entrypoint safe in either checkout mode.
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-switchyard-runtime-config.sh

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD wget -q -T 3 -O /dev/null http://127.0.0.1/healthz \
        && wget -q -T 5 -O - http://127.0.0.1/api/System/version | jq -e -s 'length == 1 and (.[0] | has("version"))' > /dev/null
CMD ["nginx", "-g", "daemon off;"]
