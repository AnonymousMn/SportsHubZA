# Release checklist

## Required before merge

- [ ] Acceptance criteria in `docs/PRODUCT_SPEC.md` reviewed.
- [ ] Worktree and branch changes are understood; no unrelated edits included.
- [ ] `dotnet restore SportsClubHub.sln` passed.
- [ ] `dotnet build SportsClubHub.sln --configuration Release --no-restore` passed.
- [ ] `dotnet test SportsClubHub.sln --configuration Release --no-build --no-restore` passed.
- [ ] Authorization and input-validation behavior is covered by tests or reviewed evidence.
- [ ] Persistence regression tests pass.
- [ ] No secrets, real credentials or personal data were added.
- [ ] Docker image build passes when Docker is available.
- [ ] Independent reviewer records findings and unresolved risks.

## Required before production deployment

- [ ] Render service configuration has been reviewed without changing it automatically.
- [ ] The SQLite database is on a verified persistent disk, or an approved external
  database migration is complete. A normal Render ephemeral filesystem is a release blocker.
- [ ] Database backup and restore procedure has been tested.
- [ ] `Admin__InitialEmail` and `Admin__InitialPassword` are supplied securely only when
  initial account creation is required.
- [ ] SMTP credentials, if enabled, are stored as secrets.
- [ ] `Database__LoadDemoData=false` is confirmed.
- [ ] HTTPS, cookie and host settings are checked.
- [ ] A non-destructive smoke test verifies home, sign-in boundary, public directory and
  registration behavior.
- [ ] Rollback owner and rollback steps are known.

## Explicit prohibitions

Do not automatically deploy, delete database files, alter production credentials, modify
Render storage, or run destructive migrations from CI.

## Evidence

Record the exact command, date, environment and result for every checked item. Mark items
as `Not run` rather than `Passed` when the environment cannot execute them.
