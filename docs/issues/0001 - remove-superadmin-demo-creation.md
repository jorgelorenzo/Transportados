---
number: 1
title: "Remove superadmin demo tenant creation"
status: done
type: refactor
priority: normal
labels: [api, backend, frontend, ui, data]
created: 2026-06-23
updated: 2026-06-23
templateVersion: 1
source: local
parent:
---
# Issue 0001 - Remove superadmin demo tenant creation

- [x] #task #issue #issue/status/done #issue/type/refactor #issue/priority/normal #issue/source/local Issue 0001: Remove superadmin demo tenant creation ➕ 2026-06-23 ✅ 2026-06-23

## Summary

Remove the superadmin demo tenant creation feature from the Transportados platform API and UI so demo/customer data is owned by the initial seed path only.

## Current State

The platform tenants screen exposes a `Crear demo` action for superadmins. The action calls `POST /api/platform/tenants/seed`, which creates a demo tenant, demo users, and parameterized customers through `EFDataServices.CreateParameterizedCompanySeed`.

## Acceptance Criteria

- [x] The platform tenants UI no longer exposes the `Crear demo` action.
- [x] The backend no longer maps `POST /api/platform/tenants/seed`.
- [x] Parameterized company seed request/response DTOs and data-service members are removed when no longer referenced.
- [x] The initial startup seed remains available as the only demo/customer seeding path.
- [x] Tests and markup assertions are updated to reflect the removed feature.

## Notes And Decisions

- UI preview decision: after seeing the temporary preview for removing parameterized client generation, the requested final flow changed to removing superadmin demo creation entirely. No new action flow remains; the platform tenants screen should focus on list, status, features, and delete operations.
- No accepted ADR conflicts were found. ADR 0001 is UI theme related and does not constrain this backend/API removal.

## Implementation Evidence

- UI preview decision summary: the accepted final state removes the superadmin demo creation action entirely. The platform tenants screen keeps metrics, tenant list, feature editing, status updates, and delete operations; no demo creation loading/success/error state remains.
- Product search summary: removed demo creation references are absent from product source. Remaining matches are negative assertions in `Transportados.Web.Test`.
- `dotnet restore Transportados.App.sln --source https://api.nuget.org/v3/index.json`: passed. Existing warnings remained for AndroidX version constraints and `System.Security.Cryptography.Xml`.
- `dotnet build Transportados.App.sln --configuration Release --no-restore`: passed with 0 errors and 26 warnings.
- `dotnet test Transportados.App.sln --configuration Release --no-build`: passed with 42 executed tests and 11 skipped UI E2E tests.
