# Transportados backend conventions

This backend baseline follows a Transportados-owned structure and naming model.

## Project boundaries

- `Transportados.Api`: host, router registration, and endpoint composition.
- `Transportados.Contracts`: API contracts and DTOs under `Api.Dto` namespace/folder.
- `Transportados.Domain`: domain entities and rules under `Api.Domain` namespace/folder.
- `Transportados.Persistence`: persistence and mapping logic under `DataAccess` namespace/folder.

## Data structure conventions

- Place external/API response and request models in `Transportados.Contracts/Api.Dto`.
- Place domain entities/value models in `Transportados.Domain/Api.Domain`.
- Place repository interfaces, entity mapping, and storage orchestration in `Transportados.Persistence/DataAccess`.
- Add route modules in `Transportados.Api/Router` using `*Router.cs` naming.

## Customer list API conventions

Customer list APIs should follow the Transportados server-paged list model:

- Use server-side pagination by default (`skip`/`take` or an established page/page-size equivalent).
- Return total counts after search and filters are applied and before paging.
- Accept `search` for one free-text query when the entity has natural searchable fields.
- Accept typed filters for stable customer facets such as city or province when exposed by the UI.
- Accept `sortBy` and `sortDescending` for supported sortable columns.
- Keep stable sort field names in contracts when multiple callers depend on them.
- Apply search, filters, ordering, and then paging in persistence.
- Avoid large fixed default fetches for primary table loading.

## Auth contract

- Transportados implements authentication entrypoint: `POST /auth/gettoken`.
- `POST /auth/gettoken` returns a JWT token as a JSON string payload.
- Transportados API host registers JWT authentication + authorization middleware and exposes auth security in Swagger.

## Transportados auth notes

- Current Transportados baseline uses a single compatibility credential pair configured through:
  - `Auth:CompatibilityUser:Username`
  - `Auth:CompatibilityUser:Password`
- If not configured, Transportados falls back to local development defaults for the compatibility user and JWT settings.
- Current Transportados scope includes `/auth/gettoken` and `/auth/me` for JWT validation/inspection; advanced auth flows such as context selection, refresh, and registration are not implemented yet.

## Database migrations

- Transportados backend applies EF Core migrations automatically on API startup when using a relational provider (`Database.MigrateAsync()`).
- Generate migrations from repo root with:
  - `dotnet ef migrations add <MigrationName> --project <repo-root>\src\apps\transportados\backend\Transportados.Persistence\Transportados.Persistence.csproj --startup-project <repo-root>\src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj --output-dir Migrations`
- Apply migrations manually (optional in local flows) with:
  - `dotnet ef database update --project <repo-root>\src\apps\transportados\backend\Transportados.Persistence\Transportados.Persistence.csproj --startup-project <repo-root>\src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj`
- Design-time `DbContext` tooling uses `TransportadosDbContextFactory` and resolves connection string from:
  - `TRANSPORTADOS_CONNECTION_STRING`
  - `ConnectionStrings__DefaultConnection`
  - fallback: LocalDB (`transportados-dev`)
