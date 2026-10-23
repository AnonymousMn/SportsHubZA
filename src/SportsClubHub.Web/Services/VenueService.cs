using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public class VenueService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public VenueService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<Venue>> SearchAsync(string? location, int? sportId, bool includeInactive = false)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Venues.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(v => v.IsActive);
        }

        var term = InputNormaliser.Text(location);
        if (term.Length > 0)
        {
            query = query.Where(v => EF.Functions.Like(v.Location, $"%{term}%"));
        }

        if (sportId is not null)
        {
            query = query.Where(v => v.VenueSports.Any(vs => vs.SportId == sportId));
        }

        return await query
            .Include(v => v.VenueSports).ThenInclude(vs => vs.Sport)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<Venue?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Venues.AsNoTracking()
            .Include(v => v.VenueSports).ThenInclude(vs => vs.Sport)
            .FirstOrDefaultAsync(v => v.VenueId == id);
    }

    /// <summary>Locations that already appear on a venue or club, used to suggest consistent spelling.</summary>
    public async Task<List<string>> GetKnownLocationsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var venueLocations = await db.Venues.Select(v => v.Location).ToListAsync();
        var clubLocations = await db.Clubs.Select(c => c.Location).ToListAsync();
        return venueLocations.Concat(clubLocations)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();
    }

    /// <summary>Adds a venue (VenueId = 0) or updates one, together with the sports it supports.</summary>
    public async Task<OperationResult> SaveAsync(Venue edited, IReadOnlyCollection<int> sportIds)
    {
        if (sportIds.Count == 0)
        {
            return OperationResult.Fail("Choose at least one sport the venue supports.");
        }

        edited.Name = InputNormaliser.Text(edited.Name);
        edited.Location = InputNormaliser.Text(edited.Location);
        edited.Address = InputNormaliser.NullIfBlank(edited.Address);
        edited.SurfaceType = InputNormaliser.NullIfBlank(edited.SurfaceType);

        await using var db = await _factory.CreateDbContextAsync();

        var nameTaken = await db.Venues.AnyAsync(v =>
            v.VenueId != edited.VenueId &&
            v.Name.ToLower() == edited.Name.ToLower() &&
            v.Location.ToLower() == edited.Location.ToLower());
        if (nameTaken)
        {
            return OperationResult.Fail("A venue with that name already exists in that location.");
        }

        Venue venue;
        if (edited.VenueId == 0)
        {
            venue = edited;
            db.Venues.Add(venue);
        }
        else
        {
            var existing = await db.Venues.Include(v => v.VenueSports).FirstOrDefaultAsync(v => v.VenueId == edited.VenueId);
            if (existing is null)
            {
                return OperationResult.Fail("Venue not found.");
            }

            existing.Name = edited.Name;
            existing.Location = edited.Location;
            existing.Address = edited.Address;
            existing.VenueType = edited.VenueType;
            existing.SurfaceType = edited.SurfaceType;
            existing.Capacity = edited.Capacity;
            venue = existing;
        }

        var toRemove = venue.VenueSports.Where(vs => !sportIds.Contains(vs.SportId)).ToList();
        foreach (var row in toRemove)
        {
            venue.VenueSports.Remove(row);
        }

        var existingIds = venue.VenueSports.Select(vs => vs.SportId).ToHashSet();
        foreach (var id in sportIds.Where(id => !existingIds.Contains(id)))
        {
            venue.VenueSports.Add(new VenueSport { SportId = id });
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetActiveAsync(int venueId, bool isActive)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.VenueId == venueId);
        if (venue is null)
        {
            return OperationResult.Fail("Venue not found.");
        }

        venue.IsActive = isActive;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }
}
