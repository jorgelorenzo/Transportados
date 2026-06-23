# Repository Guidelines

## Project Structure & Module Organization
- `src/apps/transportados/backend/Transportados.Api`: backend host, route composition, and API boundary.
- `src/apps/transportados/backend/Transportados.Contracts`: API contracts and DTOs under `Api.Dto`.
- `src/apps/transportados/backend/Transportados.Domain`: domain entities and business rules under `Api.Domain`.
- `src/apps/transportados/backend/Transportados.Persistence`: persistence logic and mapping under `DataAccess`.
- `src/apps/transportados/frontend/Transportados.Web`: Transportados web frontend host (Blazor Web App).
- `src/apps/transportados/sites`: Transportados static site area (reserved for future Transportados-owned sites).
- `tests/transportados/Transportados.Backend.Test`: Transportados backend test project.
- `tests/transportados/Transportados.Web.Test`: Transportados web/UI test project.
- `src/apps/transportados/backend/README.md`: backend conventions and ownership boundaries.
- `.codex/skills`: Transportados repo-local Codex skills.

## UI Flow Preview Planning Gate
- Use `$ui-flow-preview` before writing implementation plans or editing product code for tasks that touch screens, forms, navigation, dashboards, modals, Blazor Web UI components, browser UI tests, parity UI flows, or customer-facing UI copy.
- The preview gate requires inspecting existing routes/components/tests, producing a UI Flow Brief, showing a low-fidelity HTML preview with screens/states/transitions, and recording user validation before the final implementation plan.
- Keep previews temporary and untracked. Prefer the browser visual companion when available; otherwise use an ignored temporary artifact path.
- Temporary preview paths may be shared during the conversation for validation, but tracked docs and local issues must store only the decision summary.
- If the user explicitly waives the preview, record that waiver and the reason in the related local issue or final plan before implementation.

## Architecture Decision Records
- Before making implementation changes, read `docs/adr/index.md` and any relevant accepted ADRs to verify the change does not contradict recorded architectural decisions.
- If a change intentionally deviates from an accepted ADR, document the reason in the related Transportados issue and update or supersede the ADR as part of the same work.

## Build, Test, and Development Commands
- Restore: `dotnet restore Transportados.App.sln --source https://api.nuget.org/v3/index.json`
- Build: `dotnet build Transportados.App.sln --configuration Release`
- Tests (solution): `dotnet test Transportados.App.sln --configuration Release`
- Tests (backend only): `dotnet test tests/transportados/Transportados.Backend.Test/Transportados.Backend.Test.csproj --configuration Release`
- Tests (web only): `dotnet test tests/transportados/Transportados.Web.Test/Transportados.Web.Test.csproj --configuration Release`

## EF Migrations
- Transportados uses EF Core migrations (no `EnsureCreated` schema flow for SQL Server).
- Add a migration with:
  - `dotnet ef migrations add <MigrationName> --project <repo-root>\src\apps\transportados\backend\Transportados.Persistence\Transportados.Persistence.csproj --startup-project <repo-root>\src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj --output-dir Migrations`
- Apply migrations locally with:
  - `dotnet ef database update --project <repo-root>\src\apps\transportados\backend\Transportados.Persistence\Transportados.Persistence.csproj --startup-project <repo-root>\src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj`
- Recreate initial migration from zero (only when explicitly requested and backward compatibility is not required):
  1. Delete files under `src/apps/transportados/backend/Transportados.Persistence/Migrations/`.
  2. Run `dotnet ef migrations add InitialCreate --project <repo-root>\src\apps\transportados\backend\Transportados.Persistence\Transportados.Persistence.csproj --startup-project <repo-root>\src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj --output-dir Migrations`.
  3. Run `dotnet ef database update --project <repo-root>\src\apps\transportados\backend\Transportados.Persistence\Transportados.Persistence.csproj --startup-project <repo-root>\src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj`.
- Migrations must be generated into `src/apps/transportados/backend/Transportados.Persistence/Migrations`.
- Always keep the generated migration `.cs`, its `.Designer.cs`, and `TransportadosDbContextModelSnapshot.cs` together in the same change.

## Auth Contract Policy
- Transportados owns its authentication behavior and API contract.
- Authentication endpoint: `POST /auth/gettoken`.
- Document Transportados-specific contract changes explicitly if implementation requirements change.

## List Pagination Policy
- Transportados list screens and list APIs must use server-side pagination by default.
- For list endpoints, support and use pagination inputs (`skip`/`take` or `page`/`pageSize`) and return total counts when applicable.
- Frontend list pages should request only the current page from the backend, not fetch large fixed batches (for example `take=100/200`) as a default strategy.
- Any intentional deviation from server-side pagination must be documented in the related Transportados issue and code comments.

## NuGet Package Restore
- Transportados restores public NuGet dependencies from `https://api.nuget.org/v3/index.json`.
- `Transportados.Platform.*` dependencies are local project references under `src/apps/transportados/shared`.
- `NUGET_AUTH_TOKEN` is not required for local restore/build unless a future change reintroduces private package references.

## GitHub CLI Access
- This repo uses private GitHub resources for packages, actions, and repository operations, so `gh` commands may require valid auth.
- Check auth first: `gh auth status`
- If auth is invalid/expired: `gh auth login -h github.com` (browser flow recommended)
- Do not use GitHub Issues for active Transportados issue tracking.

## Git Safety
- Never run `git` commands that can change repository state without explicit prior user approval.
- This includes commands that modify the working tree, index, local refs, remote refs, branches, tags, stashes, or remote-tracking metadata, such as `git add`, `git commit`, `git reset`, `git restore`, `git checkout`, `git switch`, `git branch`, `git merge`, `git rebase`, `git cherry-pick`, `git revert`, `git stash`, `git clean`, `git tag`, `git fetch`, `git pull`, and `git push`.
- Read-only inspection commands such as `git status`, `git diff`, `git log`, and `git show` are allowed.

## Shared Project Policy
- Transportados consumes local shared projects under `src/apps/transportados/shared` (for example `Transportados.Platform.UI`).
- Keep shared project changes intentional and scoped.
- Transportados routes/pages remain Transportados-owned even when shared UI components are reused.

## Frontend Theming Policy
- Transportados.Web brand and semantic colors must come from the shared MudBlazor theme in `Transportados.Web.Styling.TransportadosTheme`.
- `App.razor` should keep the app-scoped `TransportadosThemeProvider` mounted so layouts, pages, and global chrome such as reconnect UI share the same MudBlazor CSS variables.
- Pages and components should use MudBlazor color parameters (`Color.Primary`, `Color.Secondary`, `Color.Success`, `Color.Error`, etc.) instead of local hex values.
- Custom CSS is allowed for layout, responsive behavior, and non-Mud framework chrome, but brand/semantic colors in CSS must reference MudBlazor CSS variables such as `--mud-palette-primary`, `--mud-palette-secondary`, `--mud-palette-error`, and `--mud-palette-divider`.
- Do not introduce page-level or component-level brand color palettes. If a theme token is missing, add it to `TransportadosTheme` and consume it through MudBlazor/theme variables.

## Coding & Testing Conventions
- Use C# with nullable reference types enabled and keep nullability warnings clean.
- Keep route modules in `Transportados.Api/Router` with `*Router.cs` naming.
- Keep DTOs in `Transportados.Contracts/Api.Dto`, domain models in `Transportados.Domain/Api.Domain`, and persistence logic in `Transportados.Persistence/DataAccess`.
- Testing stack is xUnit + `Microsoft.NET.Test.Sdk` + `coverlet.collector`.
- Do not add tests whose only purpose is to assert seed data shape, counts, or fixture content unless explicitly requested; validate seeding changes with build/runtime evidence instead.

## Search Commands
- Do not use `rg` in this repository's shell context.
- Do not search generated or dependency folders (`bin`, `obj`, `node_modules`) because binary/build outputs can contaminate results and cause timeouts.
- Use PowerShell-native search commands scoped to source files:
  - File name discovery: `Get-ChildItem -Recurse -File | Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\node_modules\\' }`
  - Text search: `$files = Get-ChildItem -Recurse -File | Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\node_modules\\' }; $files | Select-String -Pattern '<pattern>'`
  - Code-only text search: `$files = Get-ChildItem -Recurse -File -Include *.cs,*.razor,*.razor.cs,*.ts,*.js,*.json | Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\node_modules\\' }; $files | Select-String -Pattern '<pattern>'`

## Issue Tracking
- Local issue source of truth: `docs/issues`.
- Read `docs/issues/README.md` before creating, editing, or closing Transportados issues.
- Active implementation must be tracked in local Transportados issues, not remote trackers.
- Do not add remote issue URLs or remote issue metadata to tracked docs.
- Evidence in tracked docs must be durable summaries, not links to temporary local artifacts.

## Agent Boundaries
- Prefer Transportados-local implementation and validation evidence under `docs/issues`.
- Any mention of `legacy transportados` refers to the project located at `D:\Dev\transportados-movil`.
- Use root `<workspace-root>\AGENTS.md` only for cross-repo coordination tasks.

## Legacy Transportados Parity Policy
- When an issue requests parity with `legacy transportados`, parity means functional parity of the referenced legacy screen/flow, not only format, naming, or DTO compatibility.
- If legacy has a dedicated screen with specific actions (for example list/create/edit/delete flows, split forms, or navigation behavior), Transportados implementation must provide equivalent screen-level behavior in Transportados scope.
- The implementation must explicitly map legacy references to Transportados targets in evidence:
  - Legacy source file(s) and route(s).
  - Transportados source file(s) and route(s) added or updated for parity.
- Any intentional deviation from legacy must be documented in the issue evidence with:
  - what differs,
  - why the deviation is necessary in Transportados,
  - and impact on users/QA.
- A parity request is not complete until manual verification steps are documented for the Transportados route(s) equivalent to the referenced legacy route/screen.

## Canonical Local Issue Commands
- Use `$create-issue` as the canonical issue-drafting/creation command in Transportados sessions.
- Use `$create-qa-issue` as the canonical QA child-issue command in Transportados sessions.
