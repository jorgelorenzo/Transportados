# 0005. Use Product-First Transportados Monorepo Layout

## Status

Accepted

## Date

2026-06-23

## Context

Transportados uses a repository layout that groups product source, tests, shared local projects, and operational assets by ownership. The current structure already places Transportados application code under `src/apps/transportados`, tests under `tests/transportados`, and shared Transportados platform projects under the product tree.

Without a recorded layout decision, future services or shared libraries can be added in ad hoc top-level locations, making ownership and build entrypoints harder to understand.

## Decision

Use a product-first repository layout for Transportados.

- Product source lives under `src/apps/transportados`.
- Transportados backend projects live under `src/apps/transportados/backend`.
- Transportados frontend projects live under `src/apps/transportados/frontend`.
- Transportados-owned static site work is reserved under `src/apps/transportados/sites`.
- Transportados local shared projects live under `src/apps/transportados/shared`.
- Transportados tests live under `tests/transportados`.
- Operational assets should live under `ops` or documented product-owned operations paths instead of being mixed into source roots.
- Solution files may remain at the repository root as stable build and operator entrypoints.

## Consequences

- Contributors have one obvious place to add Transportados-owned backend, frontend, shared, site, test, and operations work.
- Product ownership stays clear even when shared platform primitives are reused.
- Future product or service roots can be added as siblings without reshaping Transportados.
- Repo moves, script updates, Docker updates, and CI updates should preserve this ownership model.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: N/A
- Related plans/issues: N/A
