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
}
