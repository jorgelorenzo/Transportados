# 0004. Page Long Lists Through Server-Side Queries

## Status

Accepted

## Date

2026-06-23

## Context

Transportados list screens can grow beyond demo-sized data. Loading large collections into the frontend and then paging locally wastes backend, network, and browser resources. It also makes filtered totals, sorting, and page reset behavior inconsistent between screens.

The repository already documents a server-paged list model for customer tables and platform tenant lists. This ADR makes that model durable for future list work.

## Decision

Transportados pageable list APIs and list screens must use server-side pagination by default.

- List endpoints must accept pagination inputs such as `skip`/`take` or `page`/`pageSize`.
- List responses must return total counts when the UI needs paging controls or filtered counts.
- Search, typed filters, and sorting must be applied before paging.
- Frontend list screens must request the current page from the backend instead of fetching a large fixed batch for local pagination.
- Summary/dashboard endpoints should not embed unbounded operational lists. If a screen needs summaries plus a long list, use separate summary and pageable list calls.
- Intentional deviations must be documented in the related issue or ADR, including the expected upper bound that makes server paging unnecessary.

## Consequences

- Large lists remain cheaper and more predictable as tenant data grows.
- API contracts stay explicit about paging, sorting, filtering, and totals.
- Frontend table behavior is more consistent across customer, tenant, user, and future operational screens.
- Some new screens require dedicated pageable query contracts instead of reusing broad overview payloads.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: N/A
- Related plans/issues: N/A
