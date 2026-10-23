namespace SportsClubHub.Domain.Entities;

/// <summary>
/// Join entity: "this club offers this sport". It is an explicit class (rather than a hidden
/// join table) so it is easy to query and can gain extra columns later.
/// The key is the pair (ClubId, SportId), so a club can offer each sport only once.
/// </summary>
public class ClubSport
{
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;
}
