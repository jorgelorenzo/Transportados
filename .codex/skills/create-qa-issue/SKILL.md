---
name: create-qa-issue
description: Canonical repo-local QA child issue creation command for Transportados using docs/issues Markdown files.
---

# Create Local QA Issue

Use this skill to draft and create a QA child issue in `docs/issues`.

## Source Of Truth

- Read `docs/issues/README.md` before drafting.
- QA issues are local Markdown files, not remote tracker items.
- Do not use GitHub Issues for tracking.
- Do not add remote issue URLs or remote issue metadata.
- Do not link tracked issue docs to temporary local artifacts.

## Workflow

1. Resolve the parent local issue from the user's request.
2. Inspect existing files under `docs/issues`.
3. Find the highest four-digit issue number.
4. Draft the next issue with `number = max + 1`.
5. Set:
   - `type: qa`
   - `status: open`
   - `priority: normal` unless the parent issue implies a different allowed priority
   - `source: local`
   - `parent: <parent issue number>`
   - `templateVersion` from `docs/issues/README.md`
6. Normalize the filename as:
   - `NNNN - qa-short-normalized-title.md`
7. Add exactly one Obsidian Tasks marker directly below the H1 using the README mapping.
8. Include validation scenarios, required roles/data, acceptance criteria, and durable evidence expectations.
9. Show the draft to the user before creating it unless the user explicitly asked to create it immediately.
10. After creation, report the local issue number and file path.

## Required Checks

Before finishing:

- Confirm parent exists in `docs/issues`.
- Confirm the number is unique in filename and frontmatter.
- Confirm `type: qa` and `parent` are present.
- Confirm required frontmatter fields are present.
- Confirm exactly one `#issue` Tasks marker exists.
- Confirm marker status/type/priority/source/parent tags match frontmatter.
- Confirm the issue has no remote issue links.
- Confirm the issue has no links to temporary local artifacts.
