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
}
