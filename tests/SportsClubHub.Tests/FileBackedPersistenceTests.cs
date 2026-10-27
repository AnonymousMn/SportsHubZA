using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;
using SportsClubHub.Web.Models;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public sealed class FileBackedPersistenceTests
{
    [Fact]
    public async Task CoordinateColumnInitializationOpensClosedFileConnectionAndPreservesExistingData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SportsClubHubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "sportsclubhub.db");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE Clubs (ClubId INTEGER PRIMARY KEY, Name TEXT NOT NULL);");
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE Venues (VenueId INTEGER PRIMARY KEY);");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Clubs (ClubId, Name) VALUES (1, 'Existing Club');");

            var connection = db.Database.GetDbConnection();
            Assert.Equal(ConnectionState.Closed, connection.State);

            await InvokeCoordinateColumnInitializationAsync(db);

            Assert.Equal(ConnectionState.Closed, connection.State);
            await db.Database.OpenConnectionAsync();
            var clubColumns = await GetColumnNamesAsync(db, "Clubs");
            Assert.Contains("Latitude", clubColumns);
            Assert.Contains("Longitude", clubColumns);

            await using var clubCommand = connection.CreateCommand();
            clubCommand.CommandText = "SELECT Name FROM Clubs WHERE ClubId = 1;";
            var existingClubName = (string?)await clubCommand.ExecuteScalarAsync();
            Assert.Equal("Existing Club", existingClubName);

            await InvokeCoordinateColumnInitializationAsync(db);
            Assert.Equal(ConnectionState.Open, connection.State);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InterestRegistrationCanBeReloadedFromFileBackedSqlite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SportsClubHubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "sportsclubhub.db");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;

            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Sports.Add(new Sport { Name = "Netball" });
                await setup.SaveChangesAsync();
            }

            await using (var registrationContext = new AppDbContext(options))
            {
                var sport = await registrationContext.Sports.SingleAsync();
                var service = new ParticipantInterestService(new SingleContextFactory(options));
                var result = await service.RegisterAsync(new ParticipantInterestForm
                {
                    Name = "Sam Example",
                    Email = "SAM@example.test",
                    Location = "Gqeberha",
                    ConfirmedAdult = true,
                    AcceptedPrivacyNotice = true
                }, new[] { sport.SportId });

                Assert.True(result.Succeeded);
            }

            await using (var reloaded = new AppDbContext(options))
            {
                var participant = await reloaded.Participants
                    .Include(p => p.SportInterests)
                    .SingleAsync();

                Assert.Equal("sam@example.test", participant.Email);
                Assert.Single(participant.SportInterests);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class SingleContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public SingleContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext() => new(_options);
    }

    private static async Task InvokeCoordinateColumnInitializationAsync(AppDbContext db)
    {
        var method = typeof(DatabaseInitializer).GetMethod("EnsureCoordinateColumnsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var initialization = (Task)method.Invoke(null, new object[] { db })!;
        await initialization;
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(AppDbContext db, string table)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }
}
