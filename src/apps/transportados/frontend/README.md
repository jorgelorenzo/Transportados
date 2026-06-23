# Transportados Frontend Conventions

## Project Split

- `Transportados.Client` owns reusable Blazor components, shared CSS/static assets, theme, navigation, auth, API client, and client-side services.
- `Transportados.Web` owns the Interactive Server host, HTTP pipeline, cookies, reconnect UI, and web-only static metadata.
- `Transportados.Mobile` owns the .NET MAUI Blazor Hybrid host for Android. iOS, Mac Catalyst, and Windows are future targets.

## MudBlazor Baseline (Transportados.Web / Transportados.Mobile)

Transportados uses MudBlazor as the shared UI foundation for app-level UX parity and predictable component behavior.

Checklist:
- Register `AddTransportadosMudServices(...)` from `Transportados.Client` in host startup with explicit snackbar and resize options.
- Keep `TransportadosThemeProvider` mounted in the host root (`Transportados.Web/Components/App.razor` or `Transportados.Client/Components/TransportadosClientApp.razor`).
- Keep layout providers at the top of `Transportados.Client/Components/Layout/MainLayout.razor` and `LoginLayout.razor` in this order:
  - `MudPopoverProvider`
  - `MudDialogProvider`
  - `MudSnackbarProvider`
- Keep `@using MudBlazor` in `Transportados.Client/Components/_Imports.razor` so pages/components can use Mud controls without per-file imports.
- Keep Mud assets loaded in the host document (`Transportados.Web/Components/App.razor` or `Transportados.Mobile/wwwroot/index.html`):
  - `_content/MudBlazor/MudBlazor.min.css`
  - `_content/MudBlazor/MudBlazor.min.js`

## Customer Table

- Search opens from the header search icon as a centered popup with one free-text field.
- Advanced filters are hidden by default when appropriate and open from the header filter icon as a centered popup.
- Search and filter popups close on outside click/tap without applying or clearing draft criteria.
- Apply and clear actions are textual buttons centered at the bottom of each popup.
- Search and filter header buttons show a lower-right red dot only when criteria are applied, not while values are only drafted in an open popup.
- Filter controls use responsive popup columns that fit the available width.
- Table headers are sortable when the backend supports the column.
- Applying search, filters, or sort reloads from the first page through server-side pagination.
- Autocomplete filters should request a small lookup page instead of loading a large full list.

## Responsive Conventions

- Use the existing layout and nav CSS breakpoints (`641px`) as the baseline mobile/desktop split.
- Prefer Transportados layout primitives first (`MainLayout.razor`, `NavMenu.razor`) and add Mud responsive components only when page behavior requires them.
- Validate layout sanity for both desktop and mobile flows when changing providers, top bar, or nav structure.

## Verification Expectations

Before closing UI integration work:
- `dotnet build Transportados.App.sln --configuration Release`
- `dotnet test tests/transportados/Transportados.Web.Test/Transportados.Web.Test.csproj --configuration Release`
- Ensure bUnit smoke tests cover provider presence and main layout rendering.
