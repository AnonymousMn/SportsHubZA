# Sports Hub RSA agent collaboration rules

## Mission

Preserve the existing Sports Club Hub RSA application while making small, reviewable,
well-tested improvements. The application is an ASP.NET Core Blazor Server application
targeting .NET 8, using EF Core 8 and SQLite. MongoDB is not implemented.

## Roles

- **Lead engineer**: turns a request into acceptance criteria, identifies dependencies,
  assigns isolated work, resolves conflicts, and confirms release readiness.
- **Implementation specialist**: makes only the assigned change, follows the acceptance
  criteria, and reports files changed, assumptions, commands run, results, and remaining
  risks.
- **Independent reviewer**: checks requirements, authorization, input validation,
  persistence, regressions, test adequacy, and release evidence without relying on the
  implementer's claims.

One person or agent may perform more than one role, but the review must remain independent
in reasoning and evidence.

## Collaboration protocol

1. Start from `docs/PRODUCT_SPEC.md` and use its acceptance criteria as the shared contract.
2. Before editing, inspect the current branch and worktree. Never discard or overwrite
   uncommitted work belonging to another contributor.
3. Use a separate branch or worktree for concurrent code changes. Parallel work is allowed
   only when files and behavioral boundaries are isolated.
4. Do not silently resolve conflicting edits. Stop, describe the conflict, and ask the
   lead engineer to choose the integration order.
5. Keep changes surgical. Do not rebuild the application or introduce a new persistence
   provider without an approved migration design.
6. Never deploy, alter production settings, rotate credentials, delete data, or run a
   destructive migration automatically.
7. Treat `MONGODB_URI` as a proposed future integration only. The current provider is
   SQLite, and Render's ephemeral filesystem remains a production persistence blocker.
8. Every handoff must include:
   - requested scope and acceptance criteria;
   - files changed;
   - assumptions and compatibility decisions;
   - commands actually run and their results;
   - unresolved issues and recommended follow-up.
9. A check is passed only when the command was executed and its result was observed.
10. Prefer existing services, validation helpers, and tests. Do not bypass service-level
    validation by trusting browser validation.

## Engineering rules

- Keep nullable reference types enabled and preserve type safety.
- Keep authorization on every administrative page and verify role boundaries in tests.
- Normalize and validate input in services, not only in Razor components.
- Do not expose participant personal data in public pages or logs.
- Use `IDbContextFactory<AppDbContext>` for Blazor database work.
- Preserve public visibility rules: only active, verified clubs and active directory data
  are public.
- Use migrations for future schema evolution; do not extend startup SQL indefinitely.
- Add or update a focused test for every behavior change.
- Do not report MongoDB or Render durability as implemented until it is actually wired,
  migrated, and tested.

## Standard commands

```powershell
dotnet restore SportsClubHub.sln
dotnet build SportsClubHub.sln --configuration Release --no-restore
dotnet test SportsClubHub.sln --configuration Release --no-build --no-restore
docker build -t sportshub-rsa:ci .
```

Run commands from the repository root. Do not place secrets in source, test fixtures,
logs, screenshots, or pull requests.
