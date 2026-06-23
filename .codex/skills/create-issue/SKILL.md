---
name: create-issue
description: Canonical repo-local issue creation command for Transportados using docs/issues Markdown files.
---

# Create Local Issue

Use this skill to draft and create a new Transportados issue in `docs/issues`.

## Source Of Truth

- Read `docs/issues/README.md` before drafting.
- Use the current `Issue system version` as `templateVersion`.
- Do not use GitHub Issues for tracking.
- Do not add remote issue URLs or remote issue metadata.
- Do not link tracked issue docs to temporary local artifacts.

## Workflow

1. Inspect existing files under `docs/issues`.
2. Find the highest four-digit issue number.
3. Draft the next issue with `number = max + 1`.
4. Normalize the filename as:
   - `NNNN - short-normalized-title.md`
   - lowercase ASCII kebab-case slug.
5. Use the README frontmatter and body template exactly.
6. Choose allowed values from the README:
   - `status`
   - `type`
   - `priority`
   - `source`
   - `labels`
7. Set `source: local`.
8. Add exactly one Obsidian Tasks marker directly below the H1 using the README mapping.
9. Show the draft to the user before creating it unless the user explicitly asked to create it immediately.
10. After creation, report the local issue number and file path.

## Required Checks

Before finishing:

- Confirm the number is unique in filename and frontmatter.
- Confirm required frontmatter fields are present.
- Confirm `templateVersion` is present.
- Confirm exactly one `#issue` Tasks marker exists.
- Confirm marker status/type/priority/source/parent tags match frontmatter.
- Confirm the issue has no remote issue links.
- Confirm the issue has no links to temporary local artifacts.
