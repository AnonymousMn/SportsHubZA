using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public class ClubService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ClubService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    /// <summary>
    /// Searches clubs by name or location, optionally for one sport.
    /// The public directory passes publicOnly = true so unverified or inactive clubs stay hidden.
    /// </summary>
    public async Task<List<Club>> SearchAsync(string? search, int? sportId, bool publicOnly = true)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Clubs.AsNoTracking().AsQueryable();
        if (publicOnly)
        {
            query = query.Where(c => c.IsActive && c.VerificationStatus == VerificationStatus.Verified);
        }

        var term = InputNormaliser.Text(search);
        if (term.Length > 0)
        {
            query = query.Where(c => EF.Functions.Like(c.Name, $"%{term}%") || EF.Functions.Like(c.Location, $"%{term}%"));
        }

        if (sportId is not null)
        {
            query = query.Where(c => c.ClubSports.Any(cs => cs.SportId == sportId));
        }

        return await query
            .Include(c => c.ClubSports).ThenInclude(cs => cs.Sport)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>Public clubs offering at least one of the given sports (used after interest registration).</summary>
    public async Task<List<Club>> FindOfferingAnyAsync(IReadOnlyCollection<int> sportIds)
    {
        await using var db = await _factory.CreateDbContextAsync();

        return await db.Clubs.AsNoTracking()
            .Where(c => c.IsActive && c.VerificationStatus == VerificationStatus.Verified)
            .Where(c => c.ClubSports.Any(cs => sportIds.Contains(cs.SportId)))
            .Include(c => c.ClubSports).ThenInclude(cs => cs.Sport)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Club?> GetAsync(int id, bool publicOnly = true)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var club = await db.Clubs.AsNoTracking()
            .Include(c => c.ClubSports).ThenInclude(cs => cs.Sport)
            .Include(c => c.Teams).ThenInclude(t => t.Sport)
            .FirstOrDefaultAsync(c => c.ClubId == id);

        if (club is not null && publicOnly &&
            (!club.IsActive || club.VerificationStatus != VerificationStatus.Verified))
        {
            return null;
        }

        return club;
    }

    /// <summary>
    /// Public club registration. Whatever the caller sends, the club always starts as Pending and
    /// active, and an administrator must verify it before it is shown in the directory.
    /// </summary>
    public async Task<OperationResult> RegisterAsync(Club club, IReadOnlyCollection<int> sportIds)
    {
        if (sportIds.Count == 0)
        {
            return OperationResult.Fail("Choose at least one sport that your club offers.");
        }

        Normalise(club);
        club.ClubId = 0;
        club.VerificationStatus = VerificationStatus.Pending;
        club.IsActive = true;
        club.CreatedAt = DateTime.UtcNow;

        await using var db = await _factory.CreateDbContextAsync();

        if (await NameTakenAsync(db, club))
        {
            return OperationResult.Fail("A club with that name already exists in that location.");
        }

        var validIds = await db.Sports.Where(s => s.IsActive && sportIds.Contains(s.SportId))
            .Select(s => s.SportId).ToListAsync();
        if (validIds.Count == 0)
        {
            return OperationResult.Fail("Choose at least one sport from the list.");
        }

        foreach (var id in validIds)
        {
            club.ClubSports.Add(new ClubSport { SportId = id });
        }

        db.Clubs.Add(club);
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    /// <summary>Administrator edit of a club's details and the sports it offers.</summary>
    public async Task<OperationResult> SaveAsync(Club edited, IReadOnlyCollection<int> sportIds)
    {
        if (sportIds.Count == 0)
        {
            return OperationResult.Fail("A club must offer at least one sport.");
        }

        Normalise(edited);

        await using var db = await _factory.CreateDbContextAsync();
        var club = await db.Clubs
            .Include(c => c.ClubSports)
            .Include(c => c.Teams)
            .FirstOrDefaultAsync(c => c.ClubId == edited.ClubId);
        if (club is null)
        {
            return OperationResult.Fail("Club not found.");
        }

        if (await NameTakenAsync(db, edited))
        {
            return OperationResult.Fail("A club with that name already exists in that location.");
        }

        // Rule: a club cannot stop offering a sport while it still has an active team for it.
        var removedWithTeams = club.ClubSports
            .Where(cs => !sportIds.Contains(cs.SportId))
            .Select(cs => cs.SportId)
            .Where(id => club.Teams.Any(t => t.SportId == id && t.IsActive))
            .ToList();
        if (removedWithTeams.Count > 0)
        {
            return OperationResult.Fail("A sport cannot be removed while the club has an active team playing it. Deactivate the team first.");
        }

        club.Name = edited.Name;
        club.Description = edited.Description;
        club.Location = edited.Location;
        club.ContactEmail = edited.ContactEmail;
        club.ContactPhone = edited.ContactPhone;
        club.Website = edited.Website;

        // Remove rows for sports no longer offered, add rows for newly offered sports.
        var toRemove = club.ClubSports.Where(cs => !sportIds.Contains(cs.SportId)).ToList();
        foreach (var row in toRemove)
        {
            club.ClubSports.Remove(row);
        }

        var existingIds = club.ClubSports.Select(cs => cs.SportId).ToHashSet();
        foreach (var id in sportIds.Where(id => !existingIds.Contains(id)))
        {
            club.ClubSports.Add(new ClubSport { ClubId = club.ClubId, SportId = id });
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetVerificationAsync(int clubId, VerificationStatus status)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.ClubId == clubId);
        if (club is null)
        {
            return OperationResult.Fail("Club not found.");
        }

        club.VerificationStatus = status;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetActiveAsync(int clubId, bool isActive)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.ClubId == clubId);
        if (club is null)
        {
            return OperationResult.Fail("Club not found.");
        }

        club.IsActive = isActive;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    private static void Normalise(Club club)
    {
        club.Name = InputNormaliser.Text(club.Name);
        club.Description = InputNormaliser.NullIfBlank(club.Description);
        club.Location = InputNormaliser.Text(club.Location);
        club.ContactEmail = InputNormaliser.Email(club.ContactEmail);
    }

    private static Task<bool> NameTakenAsync(AppDbContext db, Club club) =>
        db.Clubs.AnyAsync(c =>
            c.ClubId != club.ClubId &&
            c.Name.ToLower() == club.Name.ToLower() &&
            c.Location.ToLower() == club.Location.ToLower());
}
