# 0001. Use MudBlazor Theme For Transportados Web Brand Colors

## Status

Accepted

## Date

2026-05-26

## Context

Transportados.Web uses MudBlazor as its primary UI framework, but brand colors can drift when pages, layout CSS, static metadata, and Bootstrap fallback styles each define their own hex values. The login screen exposed this risk: MudBlazor `Color.Primary` used an old blue while the Transportados logo and shell used green accents.

Without a single source of truth, future UI changes can accidentally reintroduce mismatched blues or parallel green palettes.

## Decision

Use the Transportados MudBlazor theme as the source of truth for Transportados.Web brand and semantic colors.

- Define Transportados.Web brand and semantic palette values in `Transportados.Web.Styling.TransportadosTheme`.
- `App.razor` must render the app-scoped `TransportadosThemeProvider`, which passes the shared `TransportadosTheme.Application` instance to `MudThemeProvider` so routes, layouts, and global app chrome share the same CSS variables.
- MudBlazor components should use semantic MudBlazor color parameters such as `Color.Primary`, `Color.Secondary`, `Color.Success`, and `Color.Error`.
- Custom CSS that is still needed for layout, responsive behavior, or non-Mud elements must reference MudBlazor CSS variables such as `--mud-palette-primary`, `--mud-palette-secondary`, `--mud-palette-error`, and `--mud-palette-divider` instead of duplicating brand hex values.
- Razor app metadata that can bind C# values should use `TransportadosTheme` constants. Static files that cannot bind runtime theme values should avoid carrying brand color declarations unless they are generated from the theme.

## Consequences

- Changing Transportados brand colors now happens in one place for MudBlazor components and CSS that consumes MudBlazor variables.
- Page-level and component-level color drift becomes easier to catch in review.
- Some CSS remains necessary for layout and framework chrome, but it must consume theme variables for brand and semantic colors.
- Static assets and manifest files need extra care because they cannot automatically read the runtime MudBlazor theme.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: N/A
- Related plans/issues: N/A
