---
number: 3
title: "Implement load planner with 3D visualization"
status: open
type: feature
priority: normal
labels: [api, backend, data, frontend, ui]
created: 2026-06-23
updated: 2026-06-23
templateVersion: 1
source: local
parent:
---
# Issue 0003 - Implement load planner with 3D visualization

- [ ] #task #issue #issue/status/open #issue/type/feature #issue/priority/normal #issue/source/local Issue 0003: Implement load planner with 3D visualization ➕ 2026-06-23

## Summary

Implement a tenant-aware load-planning module that automatically arranges packages inside a vehicle and presents the calculated result in an inspectable 3D visualization.

## Current State

Transportados does not currently model vehicles, packages, load plans, or calculated placements. The approved MVP is an autonomous module whose plans define their own rectangular vehicle space and package lines instead of depending on future fleet, shipment, or order domains.

The validated UI flow contains a server-paged plan list, a vehicle-and-package editor, a synchronous optimization state, and a persisted 3D result with metrics, warnings, and recalculation. Required loading, empty, validation, calculation-error, partial-result, and complete-success states were included in the validation.

## Acceptance Criteria

- [ ] Tenant users can list and search persisted load plans through a server-paged API and UI.
- [ ] Administrators and supervisors can create, edit, save, optimize, and recalculate plans; operator/technician users can inspect plans without modifying them.
- [ ] A plan captures its name, rectangular internal vehicle dimensions in meters, maximum load in kilograms, and no more than 100 package instances.
- [ ] Package lines capture code, description, quantity, dimensions, unit weight, allowed rotation policy, and whether packages may support stacked cargo.
- [ ] Backend validation rejects invalid dimensions, weights, quantities, impossible individual orientations, and loads whose total weight exceeds the vehicle limit.
- [ ] The optimizer produces deterministic, non-overlapping placements within vehicle bounds while respecting allowed rotations, full support, non-stackable packages, and maximum total weight.
- [ ] Complete and partial optimization results persist placed coordinates, unplaced packages, utilization metrics, selected strategy, and calculation duration without exposing data across tenants.
- [ ] Recalculation replaces a successful result atomically, while a calculation failure preserves the saved draft or last valid result.
- [ ] The result screen provides a locally hosted 3D vehicle-and-package view with orbit, zoom, reset, selection, legend, filters, placement details, and an accessible non-WebGL fallback.
- [ ] The list, editor, calculation, result, and required error/empty/loading states remain usable without horizontal overflow on supported desktop and mobile viewports.
- [ ] Automated tests cover optimizer invariants, tenant isolation, permissions, persistence, API validation, responsive UI behavior, 3D renderer readiness/fallback, and a representative 100-package plan.
- [ ] The implementation includes the complete EF Core migration set and passes the Transportados Release build, backend tests, web tests, and UI test runner.

## Notes And Decisions

- The reference behavior performs hierarchical 3D packing across packages, pallets, and larger transport spaces. This MVP intentionally implements only packages placed directly inside one vehicle.
- Plans are autonomous and persist their own vehicle and package inputs; reusable catalogs and integration with fleet, orders, or shipments are out of scope.
- Input and display units are meters and kilograms. Geometry calculations should use canonical integer units internally to avoid floating-point placement errors.
- The vehicle cargo space is rectangular. Axle loads, center of gravity, wheel wells, fragility, incompatibilities, unloading sequence, pallet consolidation, export, and manual 3D placement are out of scope.
- Optimization is synchronous for at most 100 packages. Users inspect and recalculate results but do not drag or rotate individual packages manually.
- The server owns the authoritative placement result. The frontend renders that result and must not become a second optimization engine.
- The 3D renderer must be packaged locally without a runtime CDN dependency, and its colors must follow the shared Transportados theme policy.
- No new tenant feature flag is required for the MVP.

## Implementation Evidence

