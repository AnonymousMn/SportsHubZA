using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public record SportDemand(int SportId, string SportName, int InterestCount, int ClubCount);
public record LocationCount(string Location, int Count);
public record DirectoryCounts(int VerifiedClubs, int ActiveSports, int ActiveVenues);

/// <summary>
/// Aggregate figures only. Nothing here identifies a participant.
/// Remember what the numbers mean: an "interest registration" is someone saying they would like
/// to take part. It is not confirmed participation and not club membership.
/// </summary>
public class CommunityInsights
{
    /// <summary>Locations with fewer participants than this are hidden so individuals cannot be singled out.</summary>
    public const int MinimumGroupSize = 3;

    public int VerifiedClubCount { get; set; }
    public int PendingClubCount { get; set; }
    public int ActiveSportCount { get; set; }
    public int ActiveVenueCount { get; set; }
    public int InterestRegistrationCount { get; set; }
    public int ParticipantCount { get; set; }

    /// <summary>Every active sport with its interest and club counts (the full table for administrators).</summary>
    public List<SportDemand> AllSports { get; set; } = new();
    public List<SportDemand> MostPopularSports { get; set; } = new();
    public List<SportDemand> UnderservedSports { get; set; } = new();
    public List<LocationCount> ClubsByLocation { get; set; } = new();
    public List<LocationCount> ParticipantsByLocation { get; set; } = new();

    public bool HasInterestData => InterestRegistrationCount > 0;
}

public class InsightsService
{
    private const int TopSportsToShow = 5;
    private const int UnderservedInterestPerClub = 3;

    private readonly IDbContextFactory<AppDbContext> _factory;

    public InsightsService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    /// <summary>Three cheap counts for the home page (no participant data involved).</summary>
    public async Task<DirectoryCounts> GetDirectoryCountsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return new DirectoryCounts(
            await db.Clubs.CountAsync(c => c.IsActive && c.VerificationStatus == VerificationStatus.Verified),
            await db.Sports.CountAsync(s => s.IsActive),
            await db.Venues.CountAsync(v => v.IsActive));
    }

    public async Task<CommunityInsights> GetAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var verifiedClubs = db.Clubs.Where(c => c.IsActive && c.VerificationStatus == VerificationStatus.Verified);

        var insights = new CommunityInsights
        {
            VerifiedClubCount = await verifiedClubs.CountAsync(),
            PendingClubCount = await db.Clubs.CountAsync(c => c.IsActive && c.VerificationStatus == VerificationStatus.Pending),
            ActiveSportCount = await db.Sports.CountAsync(s => s.IsActive),
            ActiveVenueCount = await db.Venues.CountAsync(v => v.IsActive),
            InterestRegistrationCount = await db.SportInterests.CountAsync(),
            ParticipantCount = await db.Participants.CountAsync()
        };

        // One query for every sport, then the "popular" and "underserved" lists are worked out in memory.
        insights.AllSports = await db.Sports
            .Where(s => s.IsActive)
            .Select(s => new SportDemand(
                s.SportId,
                s.Name,
                s.SportInterests.Count,
                s.ClubSports.Count(cs => cs.Club.IsActive && cs.Club.VerificationStatus == VerificationStatus.Verified)))
            .ToListAsync();

        insights.AllSports = insights.AllSports.OrderByDescending(s => s.InterestCount).ThenBy(s => s.SportName).ToList();

        insights.MostPopularSports = insights.AllSports
            .Where(s => s.InterestCount > 0)
            .Take(TopSportsToShow)
            .ToList();

        // Underserved: people have shown interest, and there are no verified clubs or very many
        // interested people for each verified club.
        insights.UnderservedSports = insights.AllSports
            .Where(s => s.InterestCount > 0 &&
                        (s.ClubCount == 0 || s.InterestCount >= UnderservedInterestPerClub * s.ClubCount))
            .ToList();

        insights.ClubsByLocation = await verifiedClubs
            .GroupBy(c => c.Location)
            .Select(g => new LocationCount(g.Key, g.Count()))
            .ToListAsync();
        insights.ClubsByLocation = insights.ClubsByLocation.OrderByDescending(l => l.Count).ThenBy(l => l.Location).ToList();

        var participantLocations = await db.Participants
            .GroupBy(p => p.Location)
            .Select(g => new LocationCount(g.Key, g.Count()))
            .ToListAsync();
        insights.ParticipantsByLocation = participantLocations
            .Where(l => l.Count >= CommunityInsights.MinimumGroupSize)
            .OrderByDescending(l => l.Count).ThenBy(l => l.Location)
            .ToList();

        return insights;
    }
}
