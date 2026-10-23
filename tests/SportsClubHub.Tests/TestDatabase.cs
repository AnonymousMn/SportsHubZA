using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Tests;

/// <summary>
/// A throw-away in-memory SQLite database for one test. SQLite (not the EF "InMemory" provider)
/// is used so unique indexes and foreign keys behave like the real database.
/// </summary>
public sealed class TestDatabase : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public async Task<Sport> AddSportAsync(string name, SportCategory category = SportCategory.FieldSport)
    {
        await using var db = CreateDbContext();
        var sport = new Sport { Name = name, Category = category };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();
        return sport;
    }

    public async Task<Club> AddClubAsync(string name, string location, VerificationStatus status, params Sport[] sports)
    {
        await using var db = CreateDbContext();
        var club = new Club { Name = name, Location = location, ContactEmail = "club@example.test", VerificationStatus = status };
        foreach (var sport in sports)
        {
            club.ClubSports.Add(new ClubSport { SportId = sport.SportId });
        }

        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        return club;
    }

    public void Dispose() => _connection.Dispose();
}
