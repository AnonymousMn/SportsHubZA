# Sports Club Hub RSA data model

## Entities

| Entity | Purpose | Important rules |
|---|---|---|
| `Sport` | Directory sport | Unique name; active flag; category |
| `Club` | Sports organization | Unique name/location; pending until verified; active flag |
| `ClubSport` | Club/sport join | Composite key prevents duplicate relationships |
| `Team` | Team belonging to club and sport | Unique club/name; sport must be offered by club |
| `Venue` | Sports facility | Unique name/location; active flag |
| `VenueSport` | Venue/sport join | Composite key prevents duplicate relationships |
| `Participant` | Resident interest registration | Unique normalized e-mail; minimal personal data |
| `SportInterest` | Participant/sport join | Composite key prevents duplicate interest |
| `AdminUser` | Staff identity | Unique e-mail; password hash only; role and active flag |
| `Announcement` | Public staff-managed update | Draft/publish/pin/priority/expiry state |
| `SiteSettings` | Website appearance settings (one row) | Home hero media kind (none/image/video), image and video addresses, overlay strength (20 to 95 percent) and focus. Created by the `AddHeroMedia` migration. |
| `SiteMedia` | Images uploaded in the admin area | Content type (decided from the file bytes: JPEG, PNG or WebP only), bytes, created time. Served from `/media/{id}`; never edited, only replaced. |
| `AdminTask` | Internal to-do item for administrators | Title, notes, status (Open/InProgress/Done), priority, due date, assignee and creator (`AdminUser`), timestamps. Holds no participant personal data. Included in the legacy schema baseline. |
| `Competition` | Optional scheduled event | Linked to active sport; optional club and venue |

## Relationships and deletion

- Club and venue join rows cascade when the parent is deleted.
- Sports referenced by joins or teams are restricted from deletion.
- Teams restrict club and sport deletion.
- Participant interest rows cascade with participant deletion.
- Competition club and venue links become null if those records are deleted.
- Operational behavior should prefer deactivation over deletion.

## Validation and normalization

- Required fields and lengths are expressed through data annotations.
- E-mail values are normalized to lower case.
- Blank optional phone/URL values become null.
- Service methods re-check consent, active sport selection, duplicate names and
  relationship rules.
- Coordinates must be valid latitude/longitude ranges.

## Persistence rules

- Current storage is EF Core SQLite.
- The default file is `App_Data/sportsclubhub.db`.
- Startup must not drop or wipe data.
- Legacy databases are baselined after additive compatibility updates; migrations then evolve the schema.
- Future schema changes require reviewed EF migrations and backups.
- MongoDB is not part of this model or provider implementation.
- Render durability is not guaranteed by this repository; persistent storage must be
  provisioned and verified separately.

## Sensitive data

Participant name, e-mail, phone, location and consent time are personal information.
Do not expose them in public queries, logs, fixtures or screenshots. Password hashes are
never credentials and must still be treated as sensitive.
