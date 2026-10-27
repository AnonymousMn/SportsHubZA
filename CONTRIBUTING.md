# Contributing to SportsHubZA

Thank you for your interest in contributing.

## Before you start

- Follow any repository guidance in `AGENTS.md` (if present) and documented project conventions.
- Do not include secrets, credentials, private keys, personal data, or other sensitive information in code, tests, logs, screenshots, or issues.

## Contribution workflow

1. **Fork** this public repository to your own GitHub account.
2. **Create a feature branch** from `main` in your fork.
3. Make focused changes and run the local checks:

   ```bash
   dotnet restore SportsClubHub.sln
   dotnet build SportsClubHub.sln --configuration Release --no-restore
   dotnet test SportsClubHub.sln --configuration Release --no-restore
   ```

4. Push your branch and **open a pull request targeting `main`** in this repository.
5. Complete the pull request template with test evidence and impact notes.

## Reporting bugs and proposing features

- Open a GitHub issue using the provided templates for bug reports and feature requests.
- Include reproducible steps and enough context for maintainers to validate the request.

## Security vulnerability reporting

- Do **not** publish exploit details in public issues.
- Follow `SECURITY.md` to report vulnerabilities privately and coordinate disclosure.

## Review and acceptance

- All contributions are reviewed by maintainers.
- Submitting a pull request does **not** guarantee it will be accepted or merged.
