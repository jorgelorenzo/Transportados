# 0008. Seed Initial Data Through Code-Owned Seeding

## Status

Accepted

## Date

2026-06-23

## Context

Transportados needs initial local/demo data for validation, development, and first-run behavior. The current backend owns this through `TransportadosSeedService` and `SeedingOptions`, not through runtime file imports.

File-driven startup imports make local runs and hosted startup more fragile because missing files, malformed payloads, or environment-specific paths can change startup behavior. Code-owned seeding keeps the seed contract reviewable with normal source changes.

## Decision

Transportados initial and demo data must be populated through code-owned seeding in the persistence layer.

- Seed behavior is owned by `Transportados.Persistence` seeding code.
- Seed enablement and product seed selection are controlled by configuration such as `Seeding:Enabled` and `Seeding:SeedTransportados`.
- Startup seeding must be idempotent and safe to run repeatedly.
- Runtime startup must not depend on external import files for required baseline data.
- Future seed data changes should be reviewed as code changes and validated through build or runtime evidence, not by asserting brittle fixture counts.

## Consequences

- Local and demo environments are easier to recreate because baseline data lives with source-controlled seeding logic.
- Seed changes require code review and redeployment rather than swapping local files.
- The seed path must stay careful about idempotency, deterministic credentials for local/demo users, and avoiding production secrets.
- Import features, if introduced later for user data, must remain separate from startup baseline seeding.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: [0005](./0005-use-product-first-transportados-monorepo-layout.md)
- Related plans/issues: N/A
