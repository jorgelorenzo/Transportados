---
number: 4
title: "Fix mobile emulator startup"
status: done
type: bug
priority: high
labels: [mobile, deployment]
created: 2026-06-23
updated: 2026-06-23
templateVersion: 1
source: local
parent:
---
# Issue 0004 - Fix mobile emulator startup

- [x] #task #issue #issue/status/done #issue/type/bug #issue/priority/high #issue/source/local Issue 0004: Fix mobile emulator startup ➕ 2026-06-23 ✅ 2026-06-23

## Summary

Make `start-mobile.ps1` reliably build, deploy, and launch the Android app on the configured local emulator without attempting stale physical-device Wi-Fi connections or failing on local SDK and APK permission issues.

## Current State

Emulator startup can fail when a stale Wi-Fi endpoint remains configured, when the first discovered Android SDK does not contain the AVD system image, or when a previously signed Debug APK has inherited permissions that prevent MSBuild from replacing it. Direct APK installation also omits Fast Deployment assemblies unless MSBuild completes the deployment target.

## Acceptance Criteria

- [x] Emulator target mode does not attempt to connect a configured physical-device Wi-Fi endpoint.
- [x] A local Android SDK root can be selected from `start.local.env` and is used consistently by emulator, ADB, and .NET Android tooling.
- [x] Missing Android target configuration falls back to `Physical` without a `ValidateSet` assignment failure.
- [x] Existing generated signed APK permissions are repaired before build and deployment when required.
- [x] `start-mobile.ps1` builds, deploys, and launches `com.transportados` on the configured emulator.

## Notes And Decisions

- No accepted ADR constrains this local mobile tooling correction.
- Keep signing behavior unchanged for Debug emulator deployments; the release keystore is not required for this flow.

## Implementation Evidence

- PowerShell parser validation passed for `start-mobile.ps1`; `git diff --check` reported no whitespace errors.
- Full direct startup validation passed with `powershell -NoProfile -ExecutionPolicy Bypass -File .\start-mobile.ps1 -BackendStartupTimeoutSec 120 -AndroidStartupTimeoutSec 180`: backend Debug build completed with 0 errors, mobile Android Debug build completed with 0 errors, backend health became reachable, and `com.transportados` remained running on `emulator-5554`.
- Cold-emulator validation passed after stopping the AVD and running `start-mobile.ps1 -SkipBuild -AndroidTarget Emulator -AndroidWifiDevice 192.168.1.50:5555`: the stale Wi-Fi endpoint was explicitly ignored, `pixel_7_-_api_35_0` started from the configured SDK, and the app remained in the foreground with PID 8677.
- Runtime verification confirmed `topResumedActivity` is `com.transportados/crc64b1753140848be743.MainActivity` and the local backend health endpoint returned service `Transportados.Api` with status `ok`.
- `dotnet test tests\transportados\Transportados.Web.Test\Transportados.Web.Test.csproj --configuration Release` built successfully and executed 48 tests: 34 passed, 11 were skipped, and 3 unrelated deployment-configuration tests failed because their expected `.github/workflows` and `.github/scripts` files are absent from this checkout.
- Existing restore warnings remain: AndroidX `NU1608` version-constraint warnings for the mobile project and `NU1903` advisories for `System.Security.Cryptography.Xml` 9.0.0.
