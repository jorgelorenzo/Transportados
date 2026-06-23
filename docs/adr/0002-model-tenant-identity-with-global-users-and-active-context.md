# 0002. Model Tenant Identity With Global Users And Active Context

## Status

Accepted

## Date

2026-06-23

## Context

Transportados supports platform users who may operate across tenants, and the backend already separates global identity from tenant-scoped permissions. A person is represented by a global `User`, while tenant access is represented through `TenantMember` records. Runtime behavior also depends on the selected `ActiveRole` and `ActiveTenantId` carried through the authenticated context and token claims.

Without a durable decision, new endpoints can drift into user-global authorization checks, infer permissions from all memberships at once, or bypass the active context selected by the user.

## Decision

Use global users, tenant memberships, and active runtime context as the Transportados identity model.

- `User` is global and does not own tenant-scoped data directly.
- `TenantMember` represents tenant-scoped access for one `User`, one `Tenant`, and one role.
- The membership uniqueness model is `UserId + TenantId + Role`, allowing a user to hold more than one role in the same tenant when the product requires it.
- Authenticated runtime context is represented by `ActiveRole`, `ActiveTenantId`, `TenantMemberships`, and `AllowedTenantIds`.
- Authorization and navigation must evaluate the active runtime context first, not every membership role globally.
- `superadmin` remains the platform-level exception for platform operations and cross-tenant administration.
- Tenant-scoped entities must continue to enforce tenant boundaries through tenant context, query filters, or explicit access checks.

## Consequences

- Multi-tenant and multi-role behavior stays predictable because the selected context is authoritative.
- New tenant-scoped features have a default authorization shape to follow.
- Endpoints that need broader platform access must document and implement an explicit `superadmin` path.
- Tests and reviews should look for accidental checks against all user memberships when active role or active tenant should be used.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: N/A
- Related plans/issues: N/A
