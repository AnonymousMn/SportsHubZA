---
name: architecture-planning
description: "Use when planning or reviewing a Sports Club Hub architecture or implementation change."
---

# Architecture and implementation planning

## Required inputs

- User request and desired behavior
- Relevant acceptance criteria in `docs/PRODUCT_SPEC.md`
- Current branch/worktree status
- Relevant architecture and data-model documentation
- Existing code paths, tests and deployment constraints

## Steps

1. Inspect before editing; identify entry points, services, data boundaries and callers.
2. Record current behavior that must remain unchanged.
3. Separate required changes from optional refactors and future MongoDB work.
4. Define a small file-level implementation plan and explicit non-goals.
5. Identify authorization, validation, persistence and compatibility risks.
6. Define focused tests and release checks before implementation.
7. Assign isolated tasks to branches/worktrees when parallel work is safe.

## Expected output

A plan containing acceptance criteria, affected files, dependencies, risks, test commands,
handoff format and a clear statement of verified versus assumed facts.

## Verification

```powershell
git status --short --branch
dotnet build SportsClubHub.sln --configuration Release
dotnet test SportsClubHub.sln --configuration Release
```

## Failure conditions

Stop and escalate when requirements conflict, the worktree contains conflicting edits,
production data could be changed, the provider migration is undefined, or acceptance
criteria cannot be tested.
