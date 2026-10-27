---
name: testing-security-release
description: "Use when adding tests, performing security review, checking configuration, or verifying a Sports Club Hub release."
---

# Automated testing, security review and release verification

## Required inputs

- Acceptance criteria and changed files
- Threats and authorization boundaries
- Test project and available runtime tools
- Release checklist and deployment constraints

## Steps

1. Run focused tests first, then the complete solution tests.
2. Cover authorization, input validation, duplicate handling, persistence reload and
   failure paths.
3. Scan tracked source and configuration for credentials, private keys, connection
   strings with secrets and accidental personal data.
4. Review logs, error responses, redirects, cookies, antiforgery and public data queries.
5. Run restore, Release build and tests; run Docker validation when available.
6. Record exact commands and observed results. Mark unavailable checks as `Not run`.
7. Independently review the diff against the product acceptance criteria.

## Expected output

A test/security/release evidence report listing passed checks, failed checks, unverified
checks, findings by severity, changed files and unresolved risks.

## Verification

```powershell
dotnet restore SportsClubHub.sln
dotnet build SportsClubHub.sln --configuration Release --no-restore
dotnet test SportsClubHub.sln --configuration Release --no-build --no-restore
docker build -t sportshub-rsa:ci .
```

## Failure conditions

Do not call a check passed if it was not run, do not suppress test failures, do not
commit secrets, and do not deploy when persistence, authorization or release evidence
is unresolved.
