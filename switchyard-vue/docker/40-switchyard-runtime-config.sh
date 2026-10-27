#!/bin/sh
set -eu

# This configuration is public. Never add passwords or tokens here.
# jq encodes quotes, backslashes and line breaks as JSON rather than executable code.
runtime_config=$(jq -cn --arg apiBaseUrl "${SWITCHYARD_API_BASE_URL:-}" '{apiBaseUrl: $apiBaseUrl}')
printf 'window.__SWITCHYARD_RUNTIME_CONFIG__ = %s;\n' "$runtime_config" > /usr/share/nginx/html/runtime-config.js
