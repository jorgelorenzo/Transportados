---
number: 5
title: "Update development public hostnames"
status: in-progress
type: chore
priority: normal
labels: [deployment, docs, mobile]
created: 2026-06-23
updated: 2026-06-23
templateVersion: 1
source: local
parent:
---
# Issue 0005 - Update development public hostnames

- [/] #task #issue #issue/status/in-progress #issue/type/chore #issue/priority/normal #issue/source/local Issue 0005: Update development public hostnames ➕ 2026-06-23

## Summary

Replace the Transportados public development web and API hostnames from the `rj` endpoints to the `jl` endpoints.

## Current State

Root startup scripts and operator/mobile documentation still reference the previous `rj` development hosts.

## Acceptance Criteria

- [ ] Startup scripts use `https://transportados-app-jl.desarrollo.net.ar` and `https://transportados-api-jl.desarrollo.net.ar` where applicable.
- [ ] Repository documentation shows the new `jl` public development hostnames.
- [ ] No source or documentation reference to the previous development hostnames remains outside generated or dependency folders.

## Notes And Decisions

- This is a hostname-only operational update; no API route or authentication contract changes.
- Existing unrelated working-tree changes in affected files must be preserved.

## Implementation Evidence

- Pending implementation and validation.
