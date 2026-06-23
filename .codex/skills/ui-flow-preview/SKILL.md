---
name: ui-flow-preview
description: Generate and validate low-fidelity UI flow previews before Transportados implementation plans. Use when a task touches Transportados screens, forms, navigation, dashboards, modals, Blazor Web UI components, browser UI tests, legacy Transportados parity UI flows, customer-facing UI copy, or asks for a UI implementation plan.
---

# UI Flow Preview

## Overview

Use this as a mandatory planning gate for UI-facing Transportados work. The output is a low-fidelity preview and validation summary, not product code.

## Hard Rules

- Do not edit product UI code or write the final implementation plan until the UI flow preview has been shown and the user approves it, unless the user explicitly waives this gate.
- Keep preview artifacts temporary and untracked. Prefer the browser visual companion when available; otherwise write HTML under an ignored temporary artifact path.
- Tracked docs and local issues must record the preview decision summary, not links to temporary preview files.
- Use grayscale wireframes, realistic labels, numbered flow steps, and explicit loading, empty, error, and success states.
- Use Storybook, component spikes, or product-code prototypes only when the user explicitly asks for higher fidelity.

## Workflow

1. Ground the request in the repo.
- Inspect relevant routes, pages, shared components, layouts, and UI tests before sketching.
- For Transportados web UI, start under `src/apps/transportados/frontend/Transportados.Web`.
- For expected browser behavior, inspect `tests/transportados/Transportados.Web.Test`.
- For parity work, inspect the referenced legacy Transportados source before previewing and carry the source behavior into the brief.
- Respect the Transportados theme policy: show semantic intent and layout in low-fi, then map color/theming decisions to `Transportados.Web.Styling.TransportadosTheme` in the later plan.

2. Produce a UI Flow Brief.
- Use `references/ui-flow-brief-template.md`.
- Include target role, entry point, routes/screens, actions, data needs, states, and explicit out-of-scope notes.
- Resolve blocking questions before previewing. Non-blocking assumptions may be labeled in the brief.

3. Generate a low-fidelity HTML preview.
- Use `references/lowfi-preview-template.html` as the starting structure.
- Show the flow as a sequence the user can follow: entry point, main action path, validation/error path, empty/loading state, and success end state.
- Keep the preview visually simple. Prioritize layout, information hierarchy, actions, and state transitions over polish.

4. Ask the user to validate the preview.
- Share the temporary local URL or file path during the conversation and summarize what is on screen.
- Iterate until the user accepts the flow or explicitly waives preview validation.
- Record accepted screens, requested changes, covered states, and final decisions.

5. Only then write the implementation plan.
- Reference the validated preview decisions.
- Map the accepted flow to concrete Transportados routes/components/tests.
- Include any preview-driven decisions that constrain implementation.

## Acceptance Checklist

- The flow can be followed without hidden assumptions.
- Primary and secondary actions are visible for each step.
- Loading, empty, error, and success states are represented or explicitly marked out of scope.
- The preview is not product code and is not tracked for commit.
- The final plan names the preview evidence and maps it to implementation targets.
