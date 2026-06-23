# 0003. Keep Tenant Feature Flags Outside Tenant Settings

## Status

Accepted

## Date

2026-06-23

## Context

Transportados has tenant settings for tenant-owned operational and presentation configuration, and platform-managed feature flags for module availability such as appearance customization and email. These concepts have different owners and different enforcement paths.

If feature enablement is mixed into tenant-managed settings, product features can become editable by the wrong role, authorization can drift between UI and API checks, and future flags become harder to govern consistently from platform administration.

## Decision

Keep platform-managed tenant feature flags outside tenant-owned `Settings`.

- Tenant feature flags are part of tenant/platform governance, not tenant operational settings.
- Platform tenant management owns updates to feature flags.
- Tenant-facing UI, navigation, authorization policies, and API access must read feature enablement from the tenant feature flag contract.
- `Settings` remains focused on tenant-owned configuration such as labels, contact data, branding assets, and transport configuration.
- New feature flags should be added to the tenant feature flag contract and persisted with tenant/platform-owned data, not hidden inside settings payloads.

## Consequences

- Feature access can be audited and managed through platform tenant administration.
- Tenant settings stay narrower and avoid becoming a mixed authority for both configuration and platform entitlements.
- New modules need explicit flag wiring in backend contracts and frontend policies when access is feature-gated.
- Existing flags stored directly on `Tenant` remain valid because they are still outside `Settings`; a separate feature flag table can be introduced later only if the flag surface grows enough to justify it.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: [0002](./0002-model-tenant-identity-with-global-users-and-active-context.md)
- Related plans/issues: N/A
