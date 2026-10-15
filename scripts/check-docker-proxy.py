#!/usr/bin/env python3
"""Check a running frontend/API deployment using anonymous, read-only requests."""

import argparse
import json
import sys
from urllib.error import HTTPError, URLError
from urllib.request import ProxyHandler, build_opener


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://localhost:8080")
    args = parser.parse_args()
    # Check the specified server directly, independent of workstation proxy settings.
    client = build_opener(ProxyHandler({}))
    base = args.base_url.rstrip("/")
    failures = []
    version = None
    cases = [
        ("/api/System/version", "version"),
        ("/API/system/VERSION", "version"),
        ("/hump/getflatlayout?instanceID=0&slopeLineID=0", "protected"),
        ("/Hump/GetFlatLayout?instanceID=0&slopeLineID=0", "protected"),
        ("/capacity/getinstances", "protected"),
        ("/stationlayout/getstationschemes", "protected"),
        ("/operationplan/getoperationplans", "protected"),
        ("/operationprocess/gettemplates", "protected"),
        ("/sketchlayout/schemes", "protected"),
        ("/hump/no-such-api", "backend-missing"),
        ("/HUMP/no-such-api", "backend-missing"),
        ("/hump/instancemanagement/extra", "backend-missing"),
        ("/api/no-such-api", "backend-missing"),
        ("/hump", "page"),
        ("/HUMP/", "page"),
        ("/hump/instancemanagement", "page"),
        ("/Hump/InstanceManagement/?from=proxy-check", "page"),
        ("/capacity/", "page"),
        ("/Capacity", "page"),
        ("/sketchlayout", "page"),
        ("/SketchLayout/", "page"),
        ("/capacity-preview", "page"),
        ("/courses", "page"),
        ("/assets/no-such-file.js", "asset-missing"),
        ("/runtime-config.js", "config"),
    ]
    for path, kind in cases:
        try:
            try:
                response = client.open(base + path, timeout=10)
            except HTTPError as error:
                response = error
            with response:
                status = response.status
                headers = response.headers
                body = response.read(16384)
            if kind == "version":
                assert status == 200 and "application/json" in headers.get("Content-Type", "")
                payload = json.loads(body)
                assert isinstance(payload.get("version"), str)
                assert version is None or payload == version, "API casing changed the response"
                version = payload
            elif kind == "protected":
                assert status == 401 and "Bearer" in headers.get("WWW-Authenticate", "")
            elif kind == "backend-missing":
                assert status == 404 and headers.get("X-Content-Type-Options") == "nosniff"
                assert b'<div id="app">' not in body, "Unknown API returned the SPA"
            elif kind == "page":
                assert status == 200 and "text/html" in headers.get("Content-Type", "")
                assert b'<div id="app">' in body and "no-store" in headers.get("Cache-Control", "")
            elif kind == "config":
                assert status == 200 and b"__SWITCHYARD_RUNTIME_CONFIG__" in body
                assert "no-store" in headers.get("Cache-Control", "")
            else:
                assert status == 404
            print(f"PASS {path}")
        except (AssertionError, ValueError, URLError, OSError) as error:
            failures.append(path)
            print(f"FAIL {path}: {error or 'unexpected HTTP status, headers or body'}", file=sys.stderr)
    print(f"{len(cases) - len(failures)}/{len(cases)} proxy checks passed.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
