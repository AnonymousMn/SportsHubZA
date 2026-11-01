namespace SportsClubHub.Web.Services;

public record WeekCount(DateTime WeekStart, int Count);

/// <summary>Pure calculations behind the admin dashboard, kept separate so they are easy to test.</summary>
public static class AdminDashboardMath
{
    /// <summary>The Monday (00:00) of the week containing <paramref name="date"/>.</summary>
    public static DateTime WeekStart(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }

    /// <summary>Counts per week for the last <paramref name="weeks"/> weeks, oldest first, including empty weeks.</summary>
    public static List<WeekCount> WeeklyTrend(IEnumerable<DateTime> dates, DateTime now, int weeks)
    {
        var firstWeek = WeekStart(now).AddDays(-7 * (weeks - 1));
        var buckets = Enumerable.Range(0, weeks).Select(i => new WeekCount(firstWeek.AddDays(7 * i), 0)).ToList();

        foreach (var date in dates)
        {
            var index = (int)Math.Floor((WeekStart(date) - firstWeek).TotalDays / 7);
            if (index >= 0 && index < weeks)
            {
                buckets[index] = buckets[index] with { Count = buckets[index].Count + 1 };
            }
        }

        return buckets;
    }

    /// <summary>How many dates fall in the half-open range [from, to).</summary>
    public static int CountBetween(IEnumerable<DateTime> dates, DateTime from, DateTime to) =>
        dates.Count(d => d >= from && d < to);

    /// <summary>Whole-number percentage change, or null when there is no earlier figure to compare with.</summary>
    public static int? PercentChange(int previous, int current) =>
        previous <= 0 ? null : (int)Math.Round((current - previous) * 100.0 / previous);
}
