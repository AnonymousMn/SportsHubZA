using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

/// <summary>
/// A type of sport (football, chess, ...). Sports are database rows, not C# classes,
/// so a new sport can be added from the admin area without changing any code.
/// </summary>
public class Sport
{
    public int SportId { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    public SportCategory Category { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>A hint such as "Grass field" or "Indoor hall". Many sports can use several facility types.</summary>
    [StringLength(100)]
    public string? TypicalFacilityType { get; set; }

    /// <summary>Sports are deactivated instead of deleted so existing interest records stay meaningful.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<ClubSport> ClubSports { get; set; } = new List<ClubSport>();
    public ICollection<VenueSport> VenueSports { get; set; } = new List<VenueSport>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<SportInterest> SportInterests { get; set; } = new List<SportInterest>();
}
