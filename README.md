# Transportados

Transportados application scaffold (Blazor Web App).

# Configure your dev environment

Use this setup once before the first local build/start.

## 1) Install .NET SDK

Install the .NET 10 SDK before restoring or running the solution.

## 2) Restore/build

```powershell
dotnet restore Transportados.App.sln --source https://api.nuget.org/v3/index.json
dotnet build Transportados.App.sln --configuration Release
dotnet test Transportados.App.sln --configuration Release
```

## Architecture Overview

Transportados uses a layered application shape with Transportados-owned boundaries:

```text
Browser
   |
   v
Transportados.Web
   |
   v
Transportados.Api
   |
   v
Transportados.Persistence
```

Transportados routes, pages, and product behavior remain Transportados-owned even when shared platform/auth primitives are reused.

## Boundary rules

- Browser clients talk to `Transportados.Api`; frontend code should not bypass this boundary.
- `Transportados.Api` is the public API boundary for Transportados product features.
- `Transportados.Persistence` owns Transportados data access and mapping concerns.
- Shared `Transportados.Platform.*` projects provide common auth/tenancy building blocks, while Transportados keeps local ownership of Transportados-specific routes and flows.

## Customer list pattern

Customer tables keep the Transportados server-paged list model.

- List APIs keep server-side pagination and return totals after search/filters and before paging.
- Primary table loading uses `skip`/`take` or an existing page/page-size equivalent, not large fixed fetches.
- Search opens from the header search icon as a centered popup with one free-text field.
- Advanced filters open from the header filter icon as a centered popup when the filter set is not always needed.
- Search and filter popups close on outside click/tap without applying or clearing draft criteria.
- Apply/clear actions are textual buttons centered at the bottom of each popup.
- Search and filter header buttons show a lower-right red dot only when criteria are applied, not while values are only drafted in an open popup.
- Filter controls use responsive popup columns that fit the available width.
- Supported customer columns expose sortable headers backed by API `sortBy`/`sortDescending` handling.
- Applying search, filters, or sort resets the current table view to the first page.

## Authentication contract

Transportados owns its auth contract shape:

- Endpoint: `POST /auth/gettoken`
- Status: contract documented now; Transportados auth implementation is still evolving.
- Purpose: keep Transportados tooling and operator workflows consistent.

## Current status

- Runnable frontend scaffold created in `src/apps/transportados/frontend/Transportados.Web`.
- Initial backend foundation created in `src/apps/transportados/backend` with:
  - `Transportados.Api`
  - `Transportados.Contracts`
  - `Transportados.Domain`
  - `Transportados.Persistence`
- Consumes shared UI project `Transportados.Platform.UI`.
- Uses shared components while keeping Transportados page ownership local.

## Shared component usage

- Layout top bar: `PlatformContextSelector`
- Transportados dashboard page (`/`):
  - `PlatformFiltersPanel`
  - `PlatformTableShell`
  - `PlatformTablePager`
  - `PlatformEmptyState`

## Repository navigation

- Backend API boundary: `src/apps/transportados/backend/Transportados.Api`
- Contracts and DTOs: `src/apps/transportados/backend/Transportados.Contracts`
- Domain models and rules: `src/apps/transportados/backend/Transportados.Domain`
- Persistence and mapping: `src/apps/transportados/backend/Transportados.Persistence`
- Web frontend host: `src/apps/transportados/frontend/Transportados.Web`
- Tests: `tests/transportados/Transportados.Backend.Test`, `tests/transportados/Transportados.Web.Test`
- Server configuration guide: `docs/operations/server-configuration-guide.md`

Canonical solution entry point:
- `Transportados.App.sln`

## Prerequisites

- .NET 10 SDK
- Public NuGet access to `https://api.nuget.org/v3/index.json`

`NUGET_AUTH_TOKEN` is not required for restore/build unless a future change reintroduces private package references.

## Build

```powershell
dotnet restore Transportados.App.sln --source https://api.nuget.org/v3/index.json
dotnet build Transportados.App.sln --configuration Release
dotnet test Transportados.App.sln --configuration Release
```

## Local startup scripts

- `.\start-sql.ps1`: starts local Transportados SQL Server container on `localhost:1436` and keeps data volume.
- `.\start-sql-clean.ps1`: resets SQL container + volume and starts a fresh SQL instance.
- `.\stop-sql.ps1`: stops SQL container while preserving data volume.
- `.\start.ps1`: starts Transportados backend/frontend, auto-starts SQL if needed, checks backend health at `http://localhost:7306/api/health`, and prints local + public dev endpoints.
- `.\start-mobile.ps1`: starts Transportados backend and runs the MAUI Android app on a physical Android device by default.

## Android device over WiFi

`start-mobile.ps1` targets a physical Android device by default. To run on a phone over WiFi, the computer and phone must be on the same network and the phone must have Developer options enabled.

On the phone:

1. Enable Developer options.
2. Enable Wireless debugging.
3. Use Pair device with pairing code and note the pairing endpoint, connection endpoint, and pairing code.

On the computer, make sure `adb.exe` is available. If `adb` is not in `PATH`, use the SDK path directly:

```powershell
& "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" pair 10.0.0.130:41987
```

When prompted, enter the pairing code shown by Android. After pairing, either run the script with the endpoint that Android exposes for wireless debugging:

```powershell
.\start-mobile.ps1 -AndroidDevice 10.0.0.130:41987
```

Or persist the local device configuration in `start.local.env`:

```text
TRANSPORTADOS_DEV_ANDROID_TARGET=Physical
TRANSPORTADOS_DEV_ANDROID_WIFI_DEVICE=10.0.0.130:41987
```

If `adb devices` already shows the phone as `device`, `.\start-mobile.ps1` can select it automatically. To force the emulator instead, run:

```powershell
.\start-mobile.ps1 -AndroidTarget Emulator
```

For a persistent emulator configuration, set the SDK root that contains the AVD system image and optionally pin the AVD name:

```text
TRANSPORTADOS_DEV_ANDROID_TARGET=Emulator
TRANSPORTADOS_DEV_ANDROID_SDK_ROOT=C:\Program Files (x86)\Android\android-sdk
TRANSPORTADOS_DEV_ANDROID_AVD_NAME=pixel_7_-_api_35_0
TRANSPORTADOS_DEV_ANDROID_DEVICE=
TRANSPORTADOS_DEV_ANDROID_WIFI_DEVICE=
```

When the target is `Emulator`, `start-mobile.ps1` ignores physical-device and Wi-Fi endpoints. It validates the selected AVD system image, repairs stale generated APK permissions when needed, deploys the Debug app through the .NET Android tooling, and confirms that the app remains running.

## Seeded test users

These credentials are created by Transportados seeding for local/demo validation. They are not production secrets.

### Platform

| User | Password | Role |
| --- | --- | --- |
| `superadmin@transportados.com` | `superadmin` | `superadmin` |

### Transportados

| User | Password | Role |
| --- | --- | --- |
| `admin_transportados@transportados.com` | `admin` | `admin` |
| `soporte_transportados@transportados.com` | `transportados-demo` | `supervisor` |
| `operador_transportados@transportados.com` | `transportados-demo` | `tech` |

## Cloudflare Tunnel dev endpoint checklist

Use this checklist to expose Transportados dev endpoints:

1. Create or reuse a Cloudflare Tunnel in Zero Trust (`cloudflared tunnel create <transportados-dev-tunnel>`).
2. Add tunnel routes (ingress) for Transportados hostnames:
- `transportados-api-jl.desarrollo.net.ar` -> `http://localhost:7306`
- `transportados-app-jl.desarrollo.net.ar` -> `http://localhost:5142`
3. Add a fallback ingress rule (`http_status:404`) at the end of the ingress list.
4. Create DNS records in Cloudflare for both hostnames as CNAMEs to the tunnel (`<tunnel-id>.cfargotunnel.com`) with proxy enabled.
5. If access control is required, configure Cloudflare Access policies for the two applications.
6. Run the tunnel from your dev machine (`cloudflared tunnel run <transportados-dev-tunnel>`) while Transportados is running locally.
7. Validate endpoints:
- API: `https://transportados-api-jl.desarrollo.net.ar/api/health`
- Web: `https://transportados-app-jl.desarrollo.net.ar`

`start.ps1` default public URLs match these hostnames and prints them at startup.

If you need different tunnel hostnames, set these env vars before running `.\start.ps1`:

```powershell
$env:TRANSPORTADOS_DEV_BACKEND_PUBLIC_URL = 'https://your-api-hostname/api/health'
$env:TRANSPORTADOS_DEV_FRONTEND_PUBLIC_URL = 'https://your-web-hostname'
```

## Current note

- `dotnet restore`/`build`/`test` currently surface a NU1903 warning in `Transportados.Web.Test` from transitive package `Microsoft.Extensions.Caching.Memory` version `9.0.0-preview.3.24172.9`.

## Intentional Transportados scope

This Transportados README intentionally includes only Transportados-owned operations content. Deployment notes live under `ops/infrastructure/vm`.

## Next

- Add Transportados-specific domain screens and API integration.
- Replace backend bootstrap placeholders with first Transportados domain vertical.
