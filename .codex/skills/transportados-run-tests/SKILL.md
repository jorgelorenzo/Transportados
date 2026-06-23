---
name: transportados-run-tests
description: Run Transportados test suites and report deterministic validation evidence for local issue closure.
---

# Transportados Run Tests

Use when asked to validate Transportados behavior before merge/closure.

## Commands
- `dotnet test tests/transportados/Transportados.Web.Test/Transportados.Web.Test.csproj --configuration Release`

## Output
- pass/fail summary
- failing tests with root cause
- durable summary suitable for the local issue `## Implementation Evidence`
- no links to temporary local artifacts in tracked issue docs
