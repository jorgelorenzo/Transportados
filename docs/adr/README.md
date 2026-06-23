# Architecture Decision Records (ADR)

This directory is the architecture decision log for Transportados.

The ADR process in this repository follows the guidance and templates from:
- https://github.com/joelparkerhenderson/architecture-decision-record

## Purpose

Use ADRs to capture architectural decisions that have meaningful long-term impact, including the decision context and consequences.

## ADR Required

Create or update an ADR when a task changes or defines any of these:
- Domain model invariants or ownership rules.
- Authentication, authorization, tenant-boundary, or visibility rules.
- Persistence strategy, data model shape, or import/seeding strategy.
- Integration architecture, deployment topology, or cross-service contracts.
- Framework/library choices that materially affect long-term maintenance.

## ADR Not Required

Skip ADRs for low-risk local changes such as:
- Pure refactors with no architectural tradeoff.
- Minor UI/content tweaks without architectural impact.
- Trivial bug fixes that do not change system design constraints.

If uncertain, prefer writing an ADR.

## File Naming Convention

- Directory: `docs/adr/`
- File format: markdown (`.md`)
- Numbering: zero-padded sequence `NNNN`
- Name shape: `NNNN-short-imperative-title.md`
- Example: `0010-enforce-tenant-write-boundaries.md`

## ADR Template Contract

Each ADR should contain at least:
- Title
- Status
- Context
- Decision
- Consequences

Use `docs/adr/template.md`.

## Status Lifecycle

Allowed statuses in this repo:
- `Proposed`
- `Accepted`
- `Superseded`
- `Deprecated`
- `Rejected`

When a decision is replaced:
- Create a new ADR for the replacement.
- Mark the older ADR as `Superseded` and link the replacement.
- Link back from the new ADR to the old ADR.

## Workflow

1. Identify if the task includes an architectural decision.
2. Create a new ADR from template, or update status/links of existing ADRs.
3. Keep each ADR scoped to one decision.
4. Update `docs/adr/index.md`.
5. Cross-link related and superseded ADRs.

## Backfill Policy

Historical key decisions are backfilled in this directory. Continue with forward ADRs for new architectural decisions.
