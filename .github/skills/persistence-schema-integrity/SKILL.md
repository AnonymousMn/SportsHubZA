---
name: persistence-schema-integrity
description: "Use when changing EF Core entities, SQLite persistence, startup schema work, migrations, data integrity or future MongoDB planning."
---

# Persistence, schema and data integrity

## Required inputs

- Entity/model change and current database provider
- Existing schema and initializer behavior
- Production storage assumptions
- Backup, rollback and migration requirements
- Data-loss and concurrency risks

## Steps

1. Confirm the provider from the project files and runtime configuration; do not infer
   MongoDB from an environment placeholder.
2. Inspect `AppDbContext`, indexes, relationships and `DatabaseInitializer`.
3. Prefer reviewed EF migrations for schema changes. Treat startup SQL as transitional.
4. Preserve data, use deactivation where appropriate, and never drop tables automatically.
5. Add file-backed persistence tests that reload data from a new context/process-like
   boundary.
6. Test duplicate indexes, foreign keys, concurrent-write failure handling and startup
   compatibility where relevant.
7. Document Render filesystem durability explicitly. A MongoDB migration requires a
   separate approved design, data transfer and rollback plan.

## Expected output

A provider-accurate change, migration/backup impact statement, integrity tests, and an
explicit production storage status.

## Verification

```powershell
dotnet restore SportsClubHub.sln
dotnet build SportsClubHub.sln --configuration Release --no-restore
dotnet test SportsClubHub.sln --configuration Release --no-build --no-restore
```

## Failure conditions

Never proceed with unreviewed destructive SQL, unverified production durability, silent
provider substitution, credential changes, or a schema change without a rollback story.
