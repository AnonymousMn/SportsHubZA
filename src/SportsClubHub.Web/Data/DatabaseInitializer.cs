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
