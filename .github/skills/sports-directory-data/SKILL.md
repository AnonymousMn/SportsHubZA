---
name: sports-directory-data
description: "Use when changing sports, clubs, teams, venues, competitions, validation or public directory visibility."
---

# Sports directory data modelling and validation

## Required inputs

- Entity and relationship affected
- Public versus staff visibility requirement
- Validation and duplicate rules
- Existing service and `AppDbContext` behavior
- Acceptance criteria from `docs/PRODUCT_SPEC.md` and `docs/DATA_MODEL.md`

## Steps

1. Trace the entity, service, Razor pages and queries before changing the model.
2. Preserve active/verified public filtering and deactivation semantics.
3. Add validation at the entity/form boundary and repeat security-sensitive rules in
   the service.
4. Check unique indexes, composite keys, foreign keys and delete behavior.
5. Normalize text and e-mail values using existing helpers.
6. Add tests for valid data, invalid input, duplicates, visibility and relationships.
7. Review personal-data exposure in queries, logs and UI.

## Expected output

A documented model or service change with relationship rules, validation behavior,
affected UI, focused tests and migration implications.

## Verification

```powershell
dotnet test tests/SportsClubHub.Tests/SportsClubHub.Tests.csproj --configuration Release
```

## Failure conditions

Do not proceed if a change exposes participant data, bypasses service validation,
allows unverified public records, silently deletes data, or changes uniqueness without
an approved migration and regression tests.
