# Sports Club Hub RSA architecture

## Runtime

The solution targets `net8.0` and contains a dependency-free domain project, a Blazor
Server web project, and xUnit tests.

```text
Blazor Razor components
        |
application services
        |
IDbContextFactory<AppDbContext>
        |
EF Core 8 SQLite provider
        |
App_Data/sportsclubhub.db
```

The entry point is `src/SportsClubHub.Web/Program.cs`. It configures the database factory,
services, cookie authentication, authorization, Razor components, middleware and startup
initialization.

## Boundaries

- `src/SportsClubHub.Domain`: entities, enums, roles and input normalization.
- `src/SportsClubHub.Web/Data`: EF model configuration, seed data and current startup
  schema compatibility helpers.
- `src/SportsClubHub.Web/Services`: business rules and database queries.
- `src/SportsClubHub.Web/Components/Pages`: public, account and administrative UI.
- `tests/SportsClubHub.Tests`: service/domain tests using SQLite, primarily in-memory.

## Authentication

Cookie authentication uses `SportsClubHub.Auth`. Passwords are hashed with
`PasswordHasher<AdminUser>`. Claims contain the account ID, e-mail and role. Administrative
pages enforce role authorization; account management is administrator-only.

## Persistence reality

The provider is SQLite, not MongoDB. The default database is a file under the application
content root. `DatabaseInitializer` currently uses `EnsureCreatedAsync` plus targeted SQLite
compatibility SQL. This avoids destructive startup behavior but is not a substitute for
versioned EF migrations.

Render's default service filesystem is not a durable database location. A persistent disk
or external database is required before relying on registrations across restarts. No MongoDB
provider or migration exists in this repository.

## Architectural constraints

- Keep database operations short-lived through `IDbContextFactory`.
- Keep business validation in services and domain models.
- Keep public/admin visibility checks explicit.
- Do not add a second data provider opportunistically.
- Future schema changes should move to reviewed EF migrations.
- A MongoDB migration must include mapping, indexes, data transfer, rollback, secrets,
  integration tests and an operational cutover plan.
