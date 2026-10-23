using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

/// <summary>A team that belongs to one club and plays one sport.</summary>
public class Team
{
    public int TeamId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>For example "Open", "Under 19" or "Masters 40+".</summary>
    [Required, StringLength(40)]
    public string AgeGroup { get; set; } = "Open";

    public CompetitionLevel CompetitionLevel { get; set; } = CompetitionLevel.Recreational;

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public int SportId { get; set; }
    public Sport Sport { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
