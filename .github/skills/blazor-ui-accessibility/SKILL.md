---
name: blazor-ui-accessibility
description: "Use when changing Blazor pages, components, responsive layout, forms or accessibility in Sports Club Hub."
---

# Blazor UI, responsive design and accessibility

## Required inputs

- Component/page route and render mode
- User roles and authorization requirement
- Form validation and error states
- Mobile and desktop layout expectations
- Existing shared components and styles

## Steps

1. Confirm whether the page is static, interactive server-rendered, or requires an HTTP
   request for authentication/cookies.
2. Reuse shared layout, headers, navigation and form patterns.
3. Keep authorization attributes on administrative pages; never rely on hidden links.
4. Provide labels, validation messages, keyboard operation, semantic headings and useful
   status/error text.
5. Check responsive behavior at narrow and wide layouts without removing existing content.
6. Keep personal data out of public markup and browser-visible diagnostics.
7. Test both authorized and unauthorized paths where behavior changes.

## Expected output

An accessible component change with preserved routes, render-mode rationale, responsive
behavior, validation/error states and verification evidence.

## Verification

```powershell
dotnet build SportsClubHub.sln --configuration Release
dotnet test SportsClubHub.sln --configuration Release
```

## Failure conditions

Stop if a page loses authorization, requires client-side trust for a security decision,
breaks cookie-setting behavior, removes labels/status messages, or introduces a
desktop-only layout for an existing mobile flow.
