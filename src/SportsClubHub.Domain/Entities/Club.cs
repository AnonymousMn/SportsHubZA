using System.ComponentModel.DataAnnotations;
using SportsClubHub.Domain.Common;

namespace SportsClubHub.Domain.Entities;

/// <summary>An organisation that offers one or more sports and may run several teams.</summary>
public class Club
{
    private string? _contactPhone;
    private string? _website;

    public int ClubId { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1500)]
    public string? Description { get; set; }

    /// <summary>Town, suburb or municipality, for example "Nelson Mandela Bay".</summary>
    [Required, StringLength(120)]
    public string Location { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    /// <summary>Organisation contact details. These belong to the club, not to a private individual.</summary>
    [Required, EmailAddress, StringLength(254)]
    public string ContactEmail { get; set; } = string.Empty;

    // Optional text boxes in a form give "" rather than null, which would fail the
    // [Phone] and [Url] checks. Converting blanks to null here keeps the rules simple.
    [Phone, StringLength(30)]
    public string? ContactPhone
    {
        get => _contactPhone;
        set => _contactPhone = InputNormaliser.NullIfBlank(value);
    }

    [Url, StringLength(250)]
    public string? Website
    {
        get => _website;
        set => _website = InputNormaliser.NullIfBlank(value);
    }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClubSport> ClubSports { get; set; } = new List<ClubSport>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
