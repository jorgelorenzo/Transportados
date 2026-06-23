---
name: transportados-implement-task
description: Implement Transportados repository tasks end-to-end with Transportados-local validation evidence.
---

# Transportados Implement Task

Use when implementing a feature, fix, or refactor in `rodrigojuarez/transportados`.

## Workflow
1. Ground the request scope in Transportados code paths and any durable planning artifact the user explicitly provides.
2. Make focused changes under `src/apps/transportados/**` and `tests/transportados/**`.
3. Keep list UX/API behavior aligned with Transportados defaults (server-side pagination for list views and list endpoints unless an issue explicitly approves an exception).
4. For customer tables, keep the current Transportados search/filter/sort model: header search and filter icons open centered popups, apply/clear actions are textual buttons centered at the bottom of each popup, outside click/tap closes without applying draft criteria, lower-right red dot badges mark only applied search/filter criteria, sortable headers are used when supported, and backend `search`/typed filters/`sortBy`/`sortDescending` are applied before paging.
5. Validate restore/build/tests.
6. Report durable command results, manual verification, and any approved deviations in the final response or in the user-provided planning artifact.
7. Do not store links to temporary local artifacts in tracked documentation. Summarize screenshots, traces, videos, and preview validation instead.
8. Treat the task as complete only when implementation and validation are complete; use `blocked` when work cannot proceed.
