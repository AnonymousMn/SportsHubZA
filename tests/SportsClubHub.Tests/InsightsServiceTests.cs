using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Models;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class InsightsServiceTests
{
    [Fact]
    public async Task EmptyDatabase_ReportsZerosAndNoRankings()
    {
        using var database = new TestDatabase();

        var insights = await new InsightsService(database).GetAsync();

        Assert.Equal(0, insights.InterestRegistrationCount);
        Assert.False(insights.HasInterestData);
        Assert.Empty(insights.MostPopularSports);
        Assert.Empty(insights.UnderservedSports);
        Assert.Empty(insights.ParticipantsByLocation);
    }

    [Fact]
    public async Task SportWithInterestButNoVerifiedClub_IsReportedAsUnderserved()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        var service = new ParticipantInterestService(database);
        await service.RegisterAsync(
            new ParticipantInterestForm { Name = "A", Email = "a@example.test", Location = "Town", ConfirmedAdult = true, AcceptedPrivacyNotice = true },
            new[] { chess.SportId });

        var insights = await new InsightsService(database).GetAsync();

        var underserved = Assert.Single(insights.UnderservedSports);
        Assert.Equal("Chess", underserved.SportName);
        Assert.Equal(1, underserved.InterestCount);
    }

    [Fact]
    public async Task SmallLocations_AreHiddenUntilTheyReachTheMinimumGroupSize()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        var service = new ParticipantInterestService(database);

        for (var i = 1; i <= CommunityInsights.MinimumGroupSize; i++)
        {
            await service.RegisterAsync(
                new ParticipantInterestForm { Name = $"P{i}", Email = $"p{i}@example.test", Location = "Big Town", ConfirmedAdult = true, AcceptedPrivacyNotice = true },
                new[] { chess.SportId });
        }

        await service.RegisterAsync(
            new ParticipantInterestForm { Name = "Solo", Email = "solo@example.test", Location = "Tiny Town", ConfirmedAdult = true, AcceptedPrivacyNotice = true },
            new[] { chess.SportId });

        var insights = await new InsightsService(database).GetAsync();

        var visible = Assert.Single(insights.ParticipantsByLocation);
        Assert.Equal("Big Town", visible.Location);
    }
}
