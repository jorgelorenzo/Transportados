# Transportados Local Issues

Issue system version: `1`

`docs/issues` is the source of truth for Transportados work tracking. Remote issue trackers are not used for active Transportados issue tracking.

## File Names

Use this format:

```text
0001 - short-normalized-title.md
```

Rules:

- Use four digits for the issue number.
- Keep the issue number consecutive and human-readable.
- Use ` - ` between the number and title slug.
- Use lowercase ASCII kebab-case for the slug.
- Keep the slug short enough to scan in Obsidian and terminal output.
- If two branches create the same number, renumber one branch before merge.

## Frontmatter

Every issue must start with this frontmatter:

```yaml
---
number: 70
title: "Example issue title"
status: open
type: feature
priority: normal
labels: []
created: 2026-06-10
updated: 2026-06-10
templateVersion: 1
source: local
parent:
---
```

Imported issues use `source: imported`. Local issues use `source: local`.

Do not store remote issue URLs, remote issue IDs as separate metadata, or links to remote issue pages. The local issue number is the canonical identifier.

## Allowed Values

Status:

- `open`
- `planned`
- `in-progress`
- `blocked`
- `done`
- `cancelled`

Type:

- `feature`
- `bug`
- `qa`
- `docs`
- `chore`
- `refactor`

Priority:

- `low`
- `normal`
- `high`
- `urgent`

Source:

- `local`
- `imported`

Labels are lowercase ASCII tokens. Prefer existing product, layer, or workflow words such as:

- `api`
- `auth`
- `backend`
- `data`
- `deployment`
- `docs`
- `frontend`
- `legacy-parity`
- `mobile`
- `qa`
- `ui`

New labels may be added when useful, but keep them short and stable.

## Body Template

```markdown
# Issue 0070 - Example issue title

- [ ] #task #issue #issue/status/open #issue/type/feature #issue/priority/normal #issue/source/local Issue 0070: Example issue title ➕ 2026-06-10

## Summary

Short description of the goal and expected outcome.

## Current State

Known state, constraints, existing behavior, and relevant context.

## Acceptance Criteria

- [ ] Observable criterion.
- [ ] Observable criterion.

## Notes And Decisions

- Decision or note that should survive beyond local logs.

## Implementation Evidence

- Durable command results, validation summaries, and manual verification notes.
```

## Obsidian Tasks Marker

Each issue must include exactly one Tasks marker directly below the H1. The marker lets Obsidian Tasks query issue status without mixing issue tracking with acceptance-criteria checkboxes.

The marker is generated from frontmatter and must stay synchronized with it:

| Frontmatter `status` | Tasks checkbox | Required status tag | Date signifier |
| --- | --- | --- | --- |
| `open` | `[ ]` | `#issue/status/open` | `➕ <created>` |
| `planned` | `[ ]` | `#issue/status/planned` | `➕ <created>` |
| `in-progress` | `[/]` | `#issue/status/in-progress` | `➕ <created>` |
| `blocked` | `[ ]` | `#issue/status/blocked` | `➕ <created>` |
| `done` | `[x]` | `#issue/status/done` | `➕ <created> ✅ <updated>` |
| `cancelled` | `[-]` | `#issue/status/cancelled` | `➕ <created> ❌ <updated>` |

The marker must include:

- `#task`
- `#issue`
- `#issue/status/<status>`
- `#issue/type/<type>`
- `#issue/priority/<priority>`
- `#issue/source/<source>`
- `#issue/parent/<NNNN>` when `parent` has a value

Acceptance criteria remain normal Markdown checkboxes. Index queries under `docs/issues/index.md` filter by `#issue`, so acceptance criteria are not treated as issue records.

## Parent And Child Issues

Use `parent` for QA or child work:

```yaml
parent: 70
```

Reference related issues with Obsidian wiki links:

```markdown
Related: [[0070 - example-issue-title]]
```

In conversation, say "issue 70".

## Evidence Policy

Tracked issue files must contain durable evidence summaries directly in `## Implementation Evidence`.

Temporary artifacts under `logs/**` are scratchpad output only. Do not link to temporary logs, traces, videos, screenshots, or local file URLs from tracked documentation. If an artifact matters long term, summarize the result in the issue instead of linking the file.

Use this phrase when importing or summarizing content that previously pointed to an untracked local artifact:

```text
Untracked local artifact omitted.
```

## Creation Checklist

When creating a local issue:

- Read this README and use the current `Issue system version`.
- Find the highest existing issue number in `docs/issues`.
- Create the next consecutive number.
- Use the standard frontmatter and body template.
- Add the Obsidian Tasks marker directly below the H1.
- Set `templateVersion` to the current issue system version.
- Keep the issue free of remote issue tracker links.

## Validation Checklist

Before finishing changes to local issues, use the code harness to verify:

- Issue filenames have unique four-digit numbers.
- Frontmatter `number` values are unique and match filenames.
- Required frontmatter fields are present.
- `templateVersion` is present.
- Exactly one Tasks marker with `#issue` exists in each issue file.
- Tasks marker status/type/priority/source/parent values match frontmatter.
- `status`, `type`, `priority`, and `source` use allowed values.
- Tracked documentation has no links to remote issue pages.
- Tracked documentation has no links to temporary local artifacts.
- No issue has a prohibited imported-thread section.
