using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public class CompetitionService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CompetitionService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<Competition>> GetUpcomingAsync(DateTime? now = null)
    {
        var current = now ?? DateTime.UtcNow;
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Competitions.AsNoTracking()
            .Include(c => c.Sport)
            .Include(c => c.OrganisingClub)
            .Include(c => c.Venue)
            .Where(c => c.IsPublished && c.EndsAt >= current)
            .OrderBy(c => c.StartsAt)
            .ToListAsync();
    }

    public async Task<List<Competition>> ListAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Competitions.AsNoTracking()
            .Include(c => c.Sport).Include(c => c.OrganisingClub).Include(c => c.Venue)
            .OrderByDescending(c => c.IsPublished).ThenBy(c => c.StartsAt)
            .ToListAsync();
    }

    public async Task<OperationResult> SaveAsync(Competition competition)
    {
        competition.Name = InputNormaliser.Text(competition.Name);
        competition.Description = InputNormaliser.Text(competition.Description);
        competition.Category = InputNormaliser.Text(competition.Category);
        competition.RegistrationUrl = InputNormaliser.NullIfBlank(competition.RegistrationUrl);
        competition.ContactEmail = InputNormaliser.NullIfBlank(competition.ContactEmail);
        competition.ImageUrl = InputNormaliser.NullIfBlank(competition.ImageUrl);

        if (competition.StartsAt < competition.RegistrationClosesAt ||
            competition.RegistrationClosesAt < competition.RegistrationOpensAt ||
            competition.EndsAt < competition.StartsAt)
        {
            return OperationResult.Fail("Competition dates must follow registration opens, registration closes, start, then end.");
        }

        if (competition.RegistrationUrl is not null &&
            (!Uri.TryCreate(competition.RegistrationUrl, UriKind.Absolute, out var url) ||
             (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)))
        {
            return OperationResult.Fail("Registration URL must use http:// or https://.");
        }

        if (competition.ImageUrl is not null &&
            (!Uri.TryCreate(competition.ImageUrl, UriKind.Absolute, out var image) ||
             (image.Scheme != Uri.UriSchemeHttp && image.Scheme != Uri.UriSchemeHttps)))
        {
            return OperationResult.Fail("Image URL must use http:// or https://.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        if (!await db.Sports.AnyAsync(s => s.SportId == competition.SportId && s.IsActive))
        {
            return OperationResult.Fail("Choose an active sport.");
        }

        if (competition.OrganisingClubId is int clubId &&
            !await db.Clubs.AnyAsync(c => c.ClubId == clubId && c.IsActive && c.VerificationStatus == VerificationStatus.Verified))
        {
            return OperationResult.Fail("Choose a verified active organising club.");
        }

        if (competition.VenueId is int venueId &&
            !await db.Venues.AnyAsync(v => v.VenueId == venueId && v.IsActive))
        {
            return OperationResult.Fail("Choose an active venue.");
        }

        if (await db.Competitions.AnyAsync(c => c.CompetitionId != competition.CompetitionId &&
            c.Name.ToLower() == competition.Name.ToLower() && c.StartsAt == competition.StartsAt))
        {
            return OperationResult.Fail("A competition with that name and start date already exists.");
        }

        if (competition.CompetitionId == 0)
        {
            competition.CreatedAt = DateTime.UtcNow;
            db.Competitions.Add(competition);
        }
        else
        {
            var existing = await db.Competitions.FirstOrDefaultAsync(c => c.CompetitionId == competition.CompetitionId);
            if (existing is null) return OperationResult.Fail("Competition not found.");
            existing.Name = competition.Name; existing.Description = competition.Description;
            existing.Category = competition.Category; existing.SportId = competition.SportId;
            existing.OrganisingClubId = competition.OrganisingClubId; existing.VenueId = competition.VenueId;
            existing.RegistrationOpensAt = competition.RegistrationOpensAt; existing.RegistrationClosesAt = competition.RegistrationClosesAt;
            existing.StartsAt = competition.StartsAt; existing.EndsAt = competition.EndsAt;
            existing.RegistrationUrl = competition.RegistrationUrl; existing.ContactEmail = competition.ContactEmail;
            existing.ImageUrl = competition.ImageUrl; existing.IsPublished = competition.IsPublished;
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetPublishedAsync(int id, bool published)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var competition = await db.Competitions.FindAsync(id);
        if (competition is null) return OperationResult.Fail("Competition not found.");
        competition.IsPublished = published;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }
}
