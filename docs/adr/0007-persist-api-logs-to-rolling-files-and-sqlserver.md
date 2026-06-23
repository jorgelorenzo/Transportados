# 0007. Persist API Logs To Rolling Files And SQL Server

## Status

Accepted

## Date

2026-06-23

## Context

Transportados API needs local and hosted diagnostics that operators can inspect without relying only on console output. The API host already uses Serilog request logging, rolling file logs, and an optional SQL Server sink configured through `Serilog:Database`.

The logging table is operational telemetry, not domain data. It should not become an EF Core migration-owned table.

## Decision

`Transportados.Api` uses Serilog console output, rolling file logs, and app-database SQL Server log persistence as the supported backend logging shape.

- Console logging remains enabled for local and process-level diagnostics.
- Rolling file logging is configured through `Serilog:RollingFile`, with daily rolling and retained file limits.
- SQL Server logging is configured through `Serilog:Database` and writes to `dbo.SerilogLogs` by default.
- The SQL Server sink uses `AutoCreateSqlTable=true`; the `SerilogLogs` table is owned by the Serilog sink, not EF Core migrations.
- When database logging is enabled for a relational run, the configured connection string must exist or API startup fails with a clear configuration error.
- Database logging is disabled for in-memory database runs.
- API log events should include stable service metadata such as service name, application name, environment, trace id, request path, user id, active role, and active tenant id where available.

## Consequences

- Backend diagnostics can be inspected from rolling files and from the application SQL Server database.
- Operators must account for log growth in the application database.
- The application database user needs permissions required by the Serilog sink to create or write `dbo.SerilogLogs`.
- EF migrations must not create, alter, or drop the Serilog sink table unless a future ADR changes ownership.
- Database connectivity or permission problems can affect startup when database logging is enabled.

## Related

- Supersedes: N/A
- Superseded by: N/A
- Related ADRs: [0005](./0005-use-product-first-transportados-monorepo-layout.md)
- Related plans/issues: N/A
