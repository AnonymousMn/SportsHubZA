# Sports Club Hub RSA product specification

## Product purpose

Sports Club Hub RSA is a South African sports directory for residents to discover sports,
clubs, teams and venues and to register interest. Staff curate the directory and review
club submissions.

## Roles

| Role | Capabilities |
|---|---|
| Public resident | Browse active sports, verified clubs and venues; register interest; submit a club; view published announcements, competitions and nearby clubs |
| Staff | Sign in; manage directory records, teams, venues, announcements and competitions; review club submissions |
| Administrator | All staff capabilities plus account creation and account activation management |

Residents do not need an account. Participant interest is not club membership or an
application.

## Preserved capabilities

- Sports, clubs, teams and venues directory search and filtering
- Verified/active visibility rules
- Club registration as `Pending`
- Participant interest registration with consent, validation and duplicate prevention
- Venue bulk import and duplicate reporting
- Staff and administrator accounts with hashed passwords and role authorization
- Announcements and published competitions
- Optional email notifications
- Aggregate insights and nearby discovery

## Acceptance criteria

### Directory

- Public results contain only active records and verified clubs.
- Search and filters return deterministic, alphabetized results.
- Administrative edits validate required fields, relationships and duplicates.
- Deactivation hides records without deleting their history.

### Registration and privacy

- Participant registration requires adult confirmation, privacy acceptance and at least
  one active sport.
- E-mail addresses are normalized and duplicate interest rows are not created.
- Participant records and personal details are never listed on public pages.
- User-facing duplicate responses do not disclose whether an e-mail already exists.

### Authorization

- Unauthenticated users cannot access administrative pages.
- Staff cannot perform administrator-only account management.
- Sign-in uses a generic failure message and a local-only return URL.

### Persistence and release

- Database writes can be reloaded from the configured database.
- Startup never drops or deletes existing data.
- The current SQLite file must be placed on explicitly persistent Render storage before
  production release; this remains an unresolved operational blocker.
- Every release has restore, Release build, tests, configuration/secret checks and a
  deployment smoke check where safely available.

## Out of scope

MongoDB migration, club membership, booking, championship management, automatic deployment,
production credential changes and destructive migrations require separate approval and a
data migration plan.
