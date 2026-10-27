using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

public class Competition
{
    public int CompetitionId { get; set; }

    [Required, StringLength(140)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(60)]
    public string Category { get; set; } = "Competition";

    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    public int? OrganisingClubId { get; set; }
    public Club? OrganisingClub { get; set; }

    public int? VenueId { get; set; }
    public Venue? Venue { get; set; }

    public DateTime RegistrationOpensAt { get; set; }
    public DateTime RegistrationClosesAt { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    [StringLength(500)]
    public string? RegistrationUrl { get; set; }

    [EmailAddress, StringLength(254)]
    public string? ContactEmail { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
