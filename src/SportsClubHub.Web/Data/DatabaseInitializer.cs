using Microsoft.EntityFrameworkCore;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Web.Data;

/// <summary>
/// Prepares the database when the application starts. It never deletes anything:
/// EnsureCreated only builds the tables when the database file does not exist yet, and the
/// seed steps only run when the relevant tables are empty.
/// </summary>
public class DatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AdminAccountService _adminAccounts;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<AppDbContext> factory,
        AdminAccountService adminAccounts,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _factory = factory;
        _adminAccounts = adminAccounts;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitialiseAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        await db.Database.EnsureCreatedAsync();
        await EnsureSportImageColumnAsync(db);
        await EnsureAdminAccountColumnsAsync(db);
        await EnsureAnnouncementsTableAsync(db);
        await EnsureCompetitionsTableAsync(db);
        await EnsureCoordinateColumnsAsync(db);
        await EnsureDuplicateProtectionIndexesAsync(db);

        if (!await db.Sports.AnyAsync())
        {
            db.Sports.AddRange(SeedData.CreateSports());
            await db.SaveChangesAsync();
            _logger.LogInformation("Added the starter list of sports.");
        }

        var loadDemoData = _configuration.GetValue<bool>("Database:LoadDemoData");
        if (loadDemoData && !await db.Clubs.AnyAsync())
        {
            await SeedData.AddDemoDataAsync(db);
            _logger.LogInformation("Added fictional demonstration data.");
        }

        await _adminAccounts.EnsureInitialAdminAsync();
    }

    private static async Task EnsureCompetitionsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Competitions (
                CompetitionId INTEGER NOT NULL CONSTRAINT PK_Competitions PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL,
                Category TEXT NOT NULL,
                SportId INTEGER NOT NULL,
                OrganisingClubId INTEGER NULL,
                VenueId INTEGER NULL,
                RegistrationOpensAt TEXT NOT NULL,
                RegistrationClosesAt TEXT NOT NULL,
                StartsAt TEXT NOT NULL,
                EndsAt TEXT NOT NULL,
                RegistrationUrl TEXT NULL,
                ContactEmail TEXT NULL,
                ImageUrl TEXT NULL,
                IsPublished INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
            """);
    }

    private static async Task EnsureAnnouncementsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Announcements (
                AnnouncementId INTEGER NOT NULL CONSTRAINT PK_Announcements PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Message TEXT NOT NULL,
                Category TEXT NOT NULL,
                PublishAt TEXT NULL,
                ExpiresAt TEXT NULL,
                IsPublished INTEGER NOT NULL DEFAULT 0,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
            """);
    }

    private static async Task EnsureCoordinateColumnsAsync(AppDbContext db)
    {
        await EnsureColumnAsync(db, "Clubs", "Latitude", "REAL NULL");
        await EnsureColumnAsync(db, "Clubs", "Longitude", "REAL NULL");
        await EnsureColumnAsync(db, "Venues", "Latitude", "REAL NULL");
        await EnsureColumnAsync(db, "Venues", "Longitude", "REAL NULL");
    }

    private static async Task EnsureColumnAsync(AppDbContext db, string table, string column, string definition)
    {
        if (!IsSafeSqliteIdentifier(table))
        {
            throw new ArgumentException("The table name is not a valid SQLite identifier.", nameof(table));
        }

        if (!IsSafeSqliteIdentifier(column))
        {
            throw new ArgumentException("The column name is not a valid SQLite identifier.", nameof(column));
        }

        if (!string.Equals(definition, "REAL NULL", StringComparison.Ordinal))
        {
            throw new ArgumentException("The column definition is not supported.", nameof(definition));
        }

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;

        if (openedHere)
        {
            await db.Database.OpenConnectionAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";
            var exists = false;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                await using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};";
                await alter.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (openedHere)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    private static bool IsSafeSqliteIdentifier(string value) =>
        !string.IsNullOrEmpty(value) &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static async Task EnsureAdminAccountColumnsAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('AdminUsers');";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            if (!columns.Contains("Role"))
            {
                await using var alter = db.Database.GetDbConnection().CreateCommand();
                alter.CommandText = "ALTER TABLE AdminUsers ADD COLUMN Role TEXT NOT NULL DEFAULT 'Administrator';";
                await alter.ExecuteNonQueryAsync();
            }

            if (!columns.Contains("IsActive"))
            {
                await using var alter = db.Database.GetDbConnection().CreateCommand();
                alter.CommandText = "ALTER TABLE AdminUsers ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;";
                await alter.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureSportImageColumnAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('Sports');";

            var hasImageUrl = false;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), "ImageUrl", StringComparison.OrdinalIgnoreCase))
                    {
                        hasImageUrl = true;
                        break;
                    }
                }

            }

            if (!hasImageUrl)
            {
                await using var alter = db.Database.GetDbConnection().CreateCommand();
                alter.CommandText = "ALTER TABLE Sports ADD COLUMN ImageUrl TEXT NULL;";
                await alter.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task EnsureDuplicateProtectionIndexesAsync(AppDbContext db)
    {
        var indexes = new[]
        {
            ("IX_Sports_Name_NoCase", "Sports", "Name COLLATE NOCASE"),
            ("IX_Clubs_Name_Location_NoCase", "Clubs", "Name COLLATE NOCASE, Location COLLATE NOCASE"),
            ("IX_Venues_Name_Location_NoCase", "Venues", "Name COLLATE NOCASE, Location COLLATE NOCASE"),
            ("IX_Teams_ClubId_Name_NoCase", "Teams", "ClubId, Name COLLATE NOCASE"),
            ("IX_Participants_Email_NoCase", "Participants", "Email COLLATE NOCASE"),
            ("IX_AdminUsers_Email_NoCase", "AdminUsers", "Email COLLATE NOCASE")
        };

        await db.Database.OpenConnectionAsync();
        try
        {
            foreach (var (indexName, tableName, columns) in indexes)
            {
                await using var duplicateCheck = db.Database.GetDbConnection().CreateCommand();
                duplicateCheck.CommandText = $"SELECT EXISTS (SELECT 1 FROM {tableName} GROUP BY {columns} HAVING COUNT(*) > 1);";
                var hasDuplicates = Convert.ToInt32(await duplicateCheck.ExecuteScalarAsync()) == 1;
                if (hasDuplicates)
                {
                    _logger.LogWarning(
                        "Skipped duplicate-protection index {IndexName} because existing duplicate rows were found in {TableName}.",
                        indexName,
                        tableName);
                    continue;
                }

                await using var createIndex = db.Database.GetDbConnection().CreateCommand();
                createIndex.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS {indexName} ON {tableName} ({columns});";
                await createIndex.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
