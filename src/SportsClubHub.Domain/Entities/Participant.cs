using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

/// <summary>
/// A resident who has registered interest in sport. This is personal information (POPIA),
/// so the entity holds only what is needed to count interest by area and to follow up if the
/// person asks. Participants are never listed publicly.
/// </summary>
public class Participant
{
    public int ParticipantId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stored in lower case. Unique, so the same person is not recorded twice.</summary>
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(120)]
    public string Location { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the person accepted the privacy notice and confirmed they are an adult.</summary>
    public DateTime ConsentGivenAt { get; set; }

    public ICollection<SportInterest> SportInterests { get; set; } = new List<SportInterest>();
}
