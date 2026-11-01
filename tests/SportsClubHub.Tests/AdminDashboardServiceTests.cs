using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class AdminDashboardServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EmptyDatabase_HasNothingNeedingAttention()
    {
        using var database = new TestDatabase();

        var dashboard = await new AdminDashboardService(database).GetAsync(Now);

        Assert.Equal(0, dashboard.NeedsAttentionCount);
        Assert.Equal(AdminDashboardService.TrendWeeks, dashboard.WeeklyInterest.Count);
        Assert.All(dashboard.WeeklyInterest, w => Assert.Equal(0, w.Count));
    }

    [Fact]
    public async Task PendingClub_IsQueuedForReview_ButVerifiedClubIsNot()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        await database.AddClubAsync("Waiting Club", "Gqeberha", VerificationStatus.Pending, chess);
        await database.AddClubAsync("Live Club", "Gqeberha", VerificationStatus.Verified, chess);

        var dashboard = await new AdminDashboardService(database).GetAsync(Now);

        Assert.Equal(1, dashboard.PendingClubCount);
        Assert.Equal("Waiting Club", Assert.Single(dashboard.PendingClubs).Name);
    }

    [Fact]
    public async Task VerifiedClubWithoutCoordinatesOrTeams_IsReportedAsAQualityIssue()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        await database.AddClubAsync("Live Club", "Gqeberha", VerificationStatus.Verified, chess);

        var dashboard = await new AdminDashboardService(database).GetAsync(Now);

        Assert.Equal(1, dashboard.QualityChecks.Single(q => q.Label.StartsWith("Verified clubs without map")).Count);
        Assert.Equal(1, dashboard.QualityChecks.Single(q => q.Label.StartsWith("Verified clubs with no teams")).Count);
    }

    [Fact]
    public async Task OnlyPublishedCompetitionsClosingWithinTwoWeeks_AreListed()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        await using (var db = database.CreateDbContext())
        {
            db.Competitions.AddRange(
                MakeCompetition("Closing soon", chess, Now.AddDays(5), published: true),
                MakeCompetition("Closing later", chess, Now.AddDays(40), published: true),
                MakeCompetition("Draft closing soon", chess, Now.AddDays(5), published: false));
            await db.SaveChangesAsync();
        }

        var dashboard = await new AdminDashboardService(database).GetAsync(Now);

        Assert.Equal("Closing soon", Assert.Single(dashboard.ClosingCompetitions).Name);
        Assert.Equal(1, dashboard.DraftCompetitionCount);
    }

    [Fact]
    public async Task InterestRegistrations_AreCountedPerWeekAndPerThirtyDayPeriod()
    {
        using var database = new TestDatabase();
        var chess = await database.AddSportAsync("Chess", SportCategory.Indoor);
        await using (var db = database.CreateDbContext())
        {
            db.SportInterests.Add(MakeInterest("a@example.test", chess, Now.AddDays(-1)));
            db.SportInterests.Add(MakeInterest("b@example.test", chess, Now.AddDays(-10)));
            db.SportInterests.Add(MakeInterest("c@example.test", chess, Now.AddDays(-40)));
            await db.SaveChangesAsync();
        }

        var dashboard = await new AdminDashboardService(database).GetAsync(Now);

        Assert.Equal(2, dashboard.InterestLast30Days);
        Assert.Equal(1, dashboard.InterestPrevious30Days);
        Assert.Equal(3, dashboard.WeeklyInterest.Sum(w => w.Count));
    }

    private static Competition MakeCompetition(string name, Sport sport, DateTime closes, bool published) => new()
    {
        Name = name,
        Description = "Test competition",
        SportId = sport.SportId,
        RegistrationOpensAt = closes.AddDays(-30),
        RegistrationClosesAt = closes,
        StartsAt = closes.AddDays(7),
        EndsAt = closes.AddDays(8),
        IsPublished = published
    };

    private static SportInterest MakeInterest(string email, Sport sport, DateTime createdAt) => new()
    {
        SportId = sport.SportId,
        CreatedAt = createdAt,
        Participant = new Participant { Name = "Test", Email = email, Location = "Town", ConsentGivenAt = createdAt }
    };
}
