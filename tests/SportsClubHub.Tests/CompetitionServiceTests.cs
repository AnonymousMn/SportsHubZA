using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class CompetitionServiceTests
{
    [Fact]
    public async Task Save_RejectsInvalidDateOrder()
    {
        using var database = new TestDatabase();
        var sport = await database.AddSportAsync("Athletics");
        var service = new CompetitionService(database);

        var result = await service.SaveAsync(new Competition
        {
            Name = "Track Meet",
            Description = "A community event.",
            SportId = sport.SportId,
            RegistrationOpensAt = DateTime.UtcNow.AddDays(2),
            RegistrationClosesAt = DateTime.UtcNow.AddDays(1),
            StartsAt = DateTime.UtcNow.AddDays(3),
            EndsAt = DateTime.UtcNow.AddDays(4)
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Save_RejectsDuplicateNameAndStartDate()
    {
        using var database = new TestDatabase();
        var sport = await database.AddSportAsync("Rugby");
        var service = new CompetitionService(database);
        var start = DateTime.UtcNow.AddDays(10);
        var competition = NewCompetition(sport.SportId, start);

        Assert.True((await service.SaveAsync(competition)).Succeeded);
        var duplicate = NewCompetition(sport.SportId, start);

        var result = await service.SaveAsync(duplicate);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Save_RejectsNonHttpRegistrationUrl()
    {
        using var database = new TestDatabase();
        var sport = await database.AddSportAsync("Netball");
        var service = new CompetitionService(database);
        var competition = NewCompetition(sport.SportId, DateTime.UtcNow.AddDays(5));
        competition.RegistrationUrl = "javascript:alert(1)";

        var result = await service.SaveAsync(competition);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Upcoming_ExcludesUnpublishedAndPastCompetitions()
    {
        using var database = new TestDatabase();
        var sport = await database.AddSportAsync("Volleyball");
        var service = new CompetitionService(database);
        var now = DateTime.UtcNow;

        await service.SaveAsync(NewCompetition(sport.SportId, now.AddDays(2), isPublished: true));
        await service.SaveAsync(NewCompetition(sport.SportId, now.AddDays(-3), isPublished: true));
        await service.SaveAsync(NewCompetition(sport.SportId, now.AddDays(4), isPublished: false));

        var results = await service.GetUpcomingAsync(now);

        var item = Assert.Single(results);
        Assert.True(item.IsPublished);
    }

    private static Competition NewCompetition(int sportId, DateTime start, bool isPublished = false) => new()
    {
        Name = $"Competition {start:O}",
        Description = "A community competition.",
        SportId = sportId,
        RegistrationOpensAt = start.AddDays(-10),
        RegistrationClosesAt = start.AddDays(-1),
        StartsAt = start,
        EndsAt = start.AddDays(1),
        IsPublished = isPublished
    };
}
