using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public class TeamService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public TeamService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<Team>> ListForClubAsync(int clubId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Teams.AsNoTracking()
            .Include(t => t.Sport)
            .Where(t => t.ClubId == clubId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>Adds a team (TeamId = 0) or updates one. A team may only play a sport its club offers.</summary>
    public async Task<OperationResult> SaveAsync(Team team)
    {
        team.Name = InputNormaliser.Text(team.Name);
        team.AgeGroup = InputNormaliser.Text(team.AgeGroup);

        await using var db = await _factory.CreateDbContextAsync();

        var clubOffersSport = await db.ClubSports.AnyAsync(cs => cs.ClubId == team.ClubId && cs.SportId == team.SportId);
        if (!clubOffersSport)
        {
            return OperationResult.Fail("This club does not offer that sport. Add the sport to the club first.");
        }

        var nameTaken = await db.Teams.AnyAsync(t =>
            t.TeamId != team.TeamId &&
            t.ClubId == team.ClubId &&
            t.Name.ToLower() == team.Name.ToLower());
        if (nameTaken)
        {
            return OperationResult.Fail($"This club already has a team called \"{team.Name}\".");
        }

        if (team.TeamId == 0)
        {
            db.Teams.Add(team);
        }
        else
        {
            db.Teams.Update(team);
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetActiveAsync(int teamId, bool isActive)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var team = await db.Teams.FirstOrDefaultAsync(t => t.TeamId == teamId);
        if (team is null)
        {
            return OperationResult.Fail("Team not found.");
        }

        team.IsActive = isActive;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }
}
