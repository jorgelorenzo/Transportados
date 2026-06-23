# Transportados.Mobile

Transportados.Mobile is the .NET MAUI Blazor Hybrid host for the Transportados Blazor client.

## Scope

- Initial functional target: Android (`net10.0-android`).
- Future targets: iOS, Mac Catalyst, and Windows remain out of scope for this first delivery. The template platform folders are kept so those targets can be enabled later without reshaping the project.
- Shared UI, theme, navigation, auth, API client, and operational screens live in `../Transportados.Client`.

## Dev API URL

The mobile host defaults `ApiSettings:BaseUrl` to the Transportados dev API exposed through the local Cloudflare Tunnel:

```text
https://transportados-api-rj.desarrollo.net.ar/
```

The tunnel forwards to the local Transportados API, so `start-mobile.ps1` still starts the backend on `http://localhost:7306`. If the tunnel is not running, use the Android emulator host alias `http://10.0.2.2:7306/` or another reachable URL by overriding the `ApiSettings:BaseUrl` value in `MauiProgram`.
