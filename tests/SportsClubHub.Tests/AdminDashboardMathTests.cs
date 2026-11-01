using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class AdminDashboardMathTests
{
    // Wednesday 14 October 2026.
    private static readonly DateTime Now = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WeekStart_IsTheMondayOfThatWeek()
    {
        Assert.Equal(new DateTime(2026, 10, 12), AdminDashboardMath.WeekStart(Now));
        Assert.Equal(new DateTime(2026, 10, 12), AdminDashboardMath.WeekStart(new DateTime(2026, 10, 18, 23, 59, 0))); // Sunday
        Assert.Equal(new DateTime(2026, 10, 19), AdminDashboardMath.WeekStart(new DateTime(2026, 10, 19, 0, 0, 0)));  // Monday
    }

    [Fact]
    public void WeeklyTrend_ReturnsEveryWeekOldestFirstIncludingEmptyOnes()
    {
        var dates = new[]
        {
            new DateTime(2026, 10, 13), new DateTime(2026, 10, 14), // this week
            new DateTime(2026, 10, 1),                              // two weeks earlier
        };

        var trend = AdminDashboardMath.WeeklyTrend(dates, Now, 4);

        Assert.Equal(4, trend.Count);
        Assert.Equal(new DateTime(2026, 9, 21), trend[0].WeekStart);
        Assert.Equal(new[] { 0, 1, 0, 2 }, trend.Select(w => w.Count));
    }

    [Fact]
    public void WeeklyTrend_IgnoresDatesOutsideTheWindow()
    {
        var trend = AdminDashboardMath.WeeklyTrend(new[] { new DateTime(2025, 1, 1), new DateTime(2026, 12, 1) }, Now, 4);
        Assert.All(trend, w => Assert.Equal(0, w.Count));
    }

    [Fact]
    public void CountBetween_IsInclusiveOfFromAndExclusiveOfTo()
    {
        var dates = new[] { new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), new DateTime(2026, 1, 3) };
        Assert.Equal(2, AdminDashboardMath.CountBetween(dates, new DateTime(2026, 1, 1), new DateTime(2026, 1, 3)));
    }

    [Theory]
    [InlineData(10, 15, 50)]
    [InlineData(10, 5, -50)]
    [InlineData(4, 4, 0)]
    public void PercentChange_ComparesWithTheEarlierFigure(int previous, int current, int expected) =>
        Assert.Equal(expected, AdminDashboardMath.PercentChange(previous, current));

    [Fact]
    public void PercentChange_IsNullWhenThereIsNothingToCompareWith() =>
        Assert.Null(AdminDashboardMath.PercentChange(0, 7));
}
