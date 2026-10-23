using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

/// <summary>A sport together with how many verified clubs offer it (used on the directory page).</summary>
public record SportListItem(Sport Sport, int ClubCount);

public class SportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SportService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<SportListItem>> SearchAsync(string? search, SportCategory? category, bool includeInactive = false)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var query = db.Sports.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var term = InputNormaliser.Text(search);
        if (term.Length > 0)
        {
            query = query.Where(s => EF.Functions.Like(s.Name, $"%{term}%"));
        }

        if (category is not null)
        {
            query = query.Where(s => s.Category == category);
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SportListItem(
                s,
                s.ClubSports.Count(cs => cs.Club.IsActive && cs.Club.VerificationStatus == VerificationStatus.Verified)))
            .ToListAsync();
    }

    /// <summary>Active sports only, for check-box lists and drop-downs.</summary>
    public async Task<List<Sport>> GetActiveAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Sports.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Sport?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Sports.AsNoTracking().FirstOrDefaultAsync(s => s.SportId == id);
    }

    /// <summary>Adds a new sport (SportId = 0) or updates an existing one.</summary>
    public async Task<OperationResult> SaveAsync(Sport sport)
    {
        sport.Name = InputNormaliser.Text(sport.Name);
        sport.Description = InputNormaliser.NullIfBlank(sport.Description);
        sport.TypicalFacilityType = InputNormaliser.NullIfBlank(sport.TypicalFacilityType);

        await using var db = await _factory.CreateDbContextAsync();

        var nameTaken = await db.Sports.AnyAsync(s =>
            s.SportId != sport.SportId && s.Name.ToLower() == sport.Name.ToLower());
        if (nameTaken)
        {
            return OperationResult.Fail($"A sport called \"{sport.Name}\" already exists.");
        }

        if (sport.SportId == 0)
        {
            db.Sports.Add(sport);
        }
        else
        {
            db.Sports.Update(sport);
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetActiveAsync(int sportId, bool isActive)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var sport = await db.Sports.FirstOrDefaultAsync(s => s.SportId == sportId);
        if (sport is null)
        {
            return OperationResult.Fail("Sport not found.");
        }

        sport.IsActive = isActive;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<int> CountInterestAsync(int sportId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.SportInterests.CountAsync(si => si.SportId == sportId);
    }
}
