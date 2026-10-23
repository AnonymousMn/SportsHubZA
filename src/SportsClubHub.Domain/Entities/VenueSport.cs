namespace SportsClubHub.Domain.Entities;

/// <summary>Join entity: "this venue supports this sport". Key is (VenueId, SportId).</summary>
public class VenueSport
{
    public int VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;
}
