# 0006. Keep Shared UI Primitives Product Neutral

## Status

Accepted

## Date

2026-06-23

## Context

Transportados consumes local shared UI primitives from `Transportados.Platform.UI` while keeping Transportados routes, pages, navigation, copy, API integration, and product workflows local. This split is useful, but it can erode if shared components start carrying Transportados-specific labels, route assumptions, permissions, or workflow behavior.

The repository already has a Transportados-owned theme ADR. This ADR defines the broader shared UI boundary.

## Decision

Shared UI projects must provide product-neutral primitives, while Transportados owns product-specific page composition and behavior.

Shared UI primitives may include:

- layout and shell primitives;
- context selector primitives;
- generic table, filter, pager, loading, empty, and error states;
- low-level reusable controls that do not encode Transportados-specific domain behavior.

Transportados-owned code must keep:

- routes and page composition;
- navigation matrix behavior;
- customer-facing and operator-facing copy;
- feature and permission wiring;
- domain-specific labels, validations, and API calls;
- product-specific theme values, following ADR 0001.

## Consequences

- Shared UI can be reused without forcing Transportados product behavior into the shared layer.
- Transportados screens remain easy to reason about because page-level decisions live in Transportados code.
- Shared component APIs need careful design so they accept product content and callbacks instead of importing product assumptions.
- When a UI need is only useful for Transportados, it should stay in Transportados until a second product-neutral use case exists.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: [0001](./0001-use-mudblazor-theme-for-transportados-web-brand-colors.md), [0005](./0005-use-product-first-transportados-monorepo-layout.md)
- Related plans/issues: N/A
