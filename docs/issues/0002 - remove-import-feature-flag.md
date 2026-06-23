---
number: 2
title: "Remove import feature flag"
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
# Issue 0002 - Remove import feature flag

- [x] #task #issue #issue/status/done #issue/type/refactor #issue/priority/normal #issue/source/local Issue 0002: Remove import feature flag ➕ 2026-06-23 ✅ 2026-06-23

## Summary

Remove the tenant-level `Importacion` feature flag from the superadmin UI and backend model/contracts so import is no longer configurable as a per-tenant functionality.

## Current State

The platform tenants screen shows feature switches for `Apariencia`, `Email`, and `Importacion`. The backend stores `FeatureImportEnabled`, exposes it through `TenantFeatureFlagsDto.Import`, persists it during tenant creation/update, and emits frontend tenant feature claims/policies for import.

## Acceptance Criteria

- [x] The superadmin tenant feature panel no longer shows `Importacion`.
- [x] `TenantFeatureFlagsDto` no longer exposes an `Import` flag.
- [x] Backend tenant creation/update/mapping no longer reads or writes `FeatureImportEnabled`.
- [x] The EF model snapshot and migrations remove the `FeatureImportEnabled` column.
- [x] Frontend tenant feature claims/policies no longer include import.
- [x] Tests and markup assertions reflect the removed flag.

## Notes And Decisions

- UI preview decision summary: based on the user screenshot, the target superadmin feature panel keeps only `Apariencia` and `Email`; `Importacion` disappears instead of being hidden conditionally.
- No accepted ADR conflicts were found. ADR 0001 is UI theme related and does not constrain this feature-flag removal.

## Implementation Evidence

- UI preview decision summary: the user screenshot identified the `Importacion` switch in the superadmin tenant feature panel as the target removal. The implemented panel keeps only `Apariencia` and `Email`.
- Product and migration search summary: no live frontend/backend or EF migration references remain for `FeatureImportEnabled`. The initial base migration creates only `FeatureAppearanceEnabled` and `FeatureEmailEnabled` tenant feature columns.
- EF baseline regenerated with a temporary local `dotnet-ef` tool: `20260623162401_InitialCreate` is now the only migration, together with `TransportadosDbContextModelSnapshot.cs`; the previous `RemoveTenantImportFeature` migration was folded into the base.
- `dotnet restore Transportados.App.sln --source https://api.nuget.org/v3/index.json`: passed. Existing warnings remained for AndroidX version constraints and `System.Security.Cryptography.Xml`.
- `dotnet build Transportados.App.sln --configuration Release --no-restore`: passed with 0 errors and 26 warnings.
- `dotnet test Transportados.App.sln --configuration Release --no-build`: passed with 42 executed tests and 11 skipped UI E2E tests.
- Follow-up cleanup: removed the stale `Import` enum value from `TenantFeatureFlag` and removed stale `"Import": true` values from mobile fake authenticated-context payloads. The generic `PostFileAsync` multipart helper was intentionally left in place because it is generic client infrastructure, not an import feature.
- Follow-up validation: `dotnet build Transportados.App.sln --configuration Release --no-restore` passed with 0 errors and 26 warnings; `dotnet test Transportados.App.sln --configuration Release --no-build` passed with 42 executed tests and 11 skipped UI E2E tests.
- Baseline migration validation: `dotnet restore Transportados.App.sln --source https://api.nuget.org/v3/index.json` passed with existing AndroidX and `System.Security.Cryptography.Xml` warnings; `dotnet build Transportados.App.sln --configuration Release --no-restore` passed with 0 errors and 26 warnings; `dotnet test Transportados.App.sln --configuration Release --no-build` passed with 42 executed tests and 11 skipped UI E2E tests; `dotnet-ef database update` applied `20260623162401_InitialCreate` successfully.
