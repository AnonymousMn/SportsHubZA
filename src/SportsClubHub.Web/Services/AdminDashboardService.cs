using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public record AttentionClub(int ClubId, string Name, string Location, DateTime CreatedAt);
public record ClosingCompetition(int CompetitionId, string Name, DateTime ClosesAt);
public record ExpiringAnnouncement(int AnnouncementId, string Title, DateTime ExpiresAt);
public record QualityCheck(string Label, string Hint, int Count, string Href);

/// <summary>
/// Work queues, trends and content-quality checks for administrators. Everything here is a count or a
/// directory record; no participant personal data is read, only registration timestamps.
/// </summary>
public class AdminDashboard
{
    public int PendingClubCount { get; set; }
    public List<AttentionClub> PendingClubs { get; set; } = new();
    public int ClosingCompetitionCount { get; set; }
    public List<ClosingCompetition> ClosingCompetitions { get; set; } = new();
    public int ExpiringAnnouncementCount { get; set; }
    public List<ExpiringAnnouncement> ExpiringAnnouncements { get; set; } = new();
    public int DraftCompetitionCount { get; set; }
    public List<QualityCheck> QualityChecks { get; set; } = new();
    public List<WeekCount> WeeklyInterest { get; set; } = new();
    public int InterestLast30Days { get; set; }
    public int InterestPrevious30Days { get; set; }

    public int NeedsAttentionCount => PendingClubCount + ClosingCompetitionCount + ExpiringAnnouncementCount;
    public int OpenQualityIssues => QualityChecks.Count(q => q.Count > 0);
}

public class AdminDashboardService
{
    public const int TrendWeeks = 8;
    private const int ListLimit = 5;

    private readonly IDbContextFactory<AppDbContext> _factory;

    public AdminDashboardService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<AdminDashboard> GetAsync(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        await using var db = await _factory.CreateDbContextAsync();

        var pending = db.Clubs.Where(c => c.IsActive && c.VerificationStatus == VerificationStatus.Pending);
        var verified = db.Clubs.Where(c => c.IsActive && c.VerificationStatus == VerificationStatus.Verified);

        var dashboard = new AdminDashboard
        {
            PendingClubCount = await pending.CountAsync(),
            PendingClubs = await pending
                .OrderBy(c => c.CreatedAt)
                .Take(ListLimit)
                .Select(c => new AttentionClub(c.ClubId, c.Name, c.Location, c.CreatedAt))
                .ToListAsync(),
            DraftCompetitionCount = await db.Competitions.CountAsync(c => !c.IsPublished)
        };

        var competitionWindowEnd = now.AddDays(14);
        var closingCompetitions = db.Competitions
            .Where(c => c.IsPublished && c.RegistrationClosesAt >= now && c.RegistrationClosesAt <= competitionWindowEnd);
        dashboard.ClosingCompetitionCount = await closingCompetitions.CountAsync();
        dashboard.ClosingCompetitions = await closingCompetitions
            .OrderBy(c => c.RegistrationClosesAt)
            .Take(ListLimit)
            .Select(c => new ClosingCompetition(c.CompetitionId, c.Name, c.RegistrationClosesAt))
            .ToListAsync();

        var announcementWindowEnd = now.AddDays(7);
        var expiringAnnouncements = db.Announcements
            .Where(a => a.IsPublished && a.ExpiresAt != null && a.ExpiresAt >= now && a.ExpiresAt <= announcementWindowEnd);
        dashboard.ExpiringAnnouncementCount = await expiringAnnouncements.CountAsync();
        dashboard.ExpiringAnnouncements = await expiringAnnouncements
            .OrderBy(a => a.ExpiresAt)
            .Take(ListLimit)
            .Select(a => new ExpiringAnnouncement(a.AnnouncementId, a.Title, a.ExpiresAt!.Value))
            .ToListAsync();

        dashboard.QualityChecks = new List<QualityCheck>
        {
            new("Sports without an image URL",
                "Cards fall back to a coloured panel unless a photo file exists.",
                await db.Sports.CountAsync(s => s.IsActive && (s.ImageUrl == null || s.ImageUrl == "")),
                "admin/sports"),
            new("Venues without map coordinates",
                "These venues cannot appear in Near me results.",
                await db.Venues.CountAsync(v => v.IsActive && (v.Latitude == null || v.Longitude == null)),
                "admin/venues"),
            new("Verified clubs without map coordinates",
                "These clubs cannot appear in Near me results.",
                await verified.CountAsync(c => c.Latitude == null || c.Longitude == null),
                "admin/clubs"),
            new("Verified clubs with no teams listed",
                "Visitors see an empty team section.",
                await verified.CountAsync(c => !c.Teams.Any()),
                "admin/clubs")
        };

        // Only timestamps are read here, never names or contact details.
        var trendStart = AdminDashboardMath.WeekStart(now).AddDays(-7 * (TrendWeeks - 1));
        var since = trendStart < now.AddDays(-60) ? trendStart : now.AddDays(-60);
        var registrations = await db.SportInterests
            .Where(i => i.CreatedAt >= since)
            .Select(i => i.CreatedAt)
            .ToListAsync();

        dashboard.WeeklyInterest = AdminDashboardMath.WeeklyTrend(registrations, now, TrendWeeks);
        dashboard.InterestLast30Days = AdminDashboardMath.CountBetween(registrations, now.AddDays(-30), now.AddTicks(1));
        dashboard.InterestPrevious30Days = AdminDashboardMath.CountBetween(registrations, now.AddDays(-60), now.AddDays(-30));

        return dashboard;
    }
}
