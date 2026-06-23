---
name: transportados-run-ui-tests
description: Run Transportados browser/UI end-to-end tests through the repository PowerShell runner. Use when asked to run, verify, debug, or show Playwright UI/E2E tests for Transportados; run all UI tests by default, support only an optional specific test-name filter, and handle visible browser or headless mode.
---

# Transportados Run UI Tests

Run Playwright browser/UI end-to-end tests using the repo script `ops/scripts/run-ui-tests.ps1`.

## Workflow

1. Determine test selection from user input.
- Default: run all Playwright UI E2E tests.
- Legacy words like `smoke`, `full`, or `all` are not suite options. Do not pass them to the script; run all UI tests.
- Specific test name: when the user asks for one named test, add `-TestName <name-or-name-fragment>`.

2. Determine UI visibility.
- Headless (default): no browser window shown.
- Visible: add `-ShowUi` when the user asks to show/ver/abrir the browser UI, run headed, debug visually, or "mostrar la UI".
- Explicit hidden/no UI/headless: do not add `-ShowUi`.

3. Execute the matching command from repo root `<repo-root>`.
- All UI tests, headless default:
```powershell
powershell -ExecutionPolicy Bypass -File "<repo-root>\ops\scripts\run-ui-tests.ps1"
```
- Specific UI test by name:
```powershell
powershell -ExecutionPolicy Bypass -File "<repo-root>\ops\scripts\run-ui-tests.ps1" -TestName DesktopDashboard_ShouldKeepSidebarVisible
```
- Visible browser (append `-ShowUi`):
```powershell
powershell -ExecutionPolicy Bypass -File "<repo-root>\ops\scripts\run-ui-tests.ps1" -ShowUi
```

4. Prerequisites.
- The script builds the UI test project and installs Playwright Chromium automatically.
- Docker must be running because Transportados E2E tests use SQL Server Testcontainers.
- The script enables E2E tests with `TRANSPORTADOS_E2E_ENABLED=true`.

5. Report results clearly.
- Include passed/failed counts and failing test names when relevant.
- If `-TestName` was used, mention the test-name filter.
- If `-ShowUi` was used, note that the `TRANSPORTADOS_E2E_SHOW_UI` env var was set for the run.
- Mention temporary artifact locations to the user when useful for debugging, but do not persist those paths in tracked issue docs.
- For local issue evidence, record only durable summaries: command, pass/fail counts, failing test names, and manual verification notes.

## Notes

- Use `ops/scripts/run-ui-tests.ps1`; do not call `dotnet test` directly for UI E2E runs because the script handles Playwright setup, UI-test filtering, and `TRANSPORTADOS_E2E_ENABLED`.
- The test project is at `tests/transportados/Transportados.Web.Test/Transportados.Web.Test.csproj`.
- Run settings are at `tests/transportados/Transportados.Web.Test/playwright.runsettings` and enforce `MaxCpuCount=1` to avoid browser-test concurrency issues.
- UI-test filtering uses xUnit trait `Category=UI`; optional test-name filtering uses a `FullyQualifiedName` fragment.
- If Playwright Chromium install fails, run the generated `playwright.ps1 install chromium` from the test project's Debug output and retry the repo script.
