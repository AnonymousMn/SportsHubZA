using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

public class Announcement
{
    public int AnnouncementId { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Category { get; set; } = "General";

    public DateTime? PublishAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsPublished { get; set; }
    public bool IsPinned { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
