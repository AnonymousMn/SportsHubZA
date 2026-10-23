using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

/// <summary>A facility where sport can be played. One venue can host many sports.</summary>
public class Venue
{
    public int VenueId { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Location { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Address { get; set; }

    public VenueType VenueType { get; set; } = VenueType.Field;

    [StringLength(60)]
    public string? SurfaceType { get; set; }

    [Range(1, 200000, ErrorMessage = "Capacity must be between 1 and 200 000.")]
    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<VenueSport> VenueSports { get; set; } = new List<VenueSport>();
}
