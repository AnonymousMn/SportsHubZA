using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

public enum AdminTaskStatus
{
    Open,
    InProgress,
    Done
}

public enum AdminTaskPriority
{
    Low,
    Normal,
    High,
    Urgent
}

/// <summary>A piece of administration work that can be handed to a staff member. Contains no participant personal data.</summary>
public class AdminTask
{
    public int AdminTaskId { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public AdminTaskStatus Status { get; set; } = AdminTaskStatus.Open;

    public AdminTaskPriority Priority { get; set; } = AdminTaskPriority.Normal;

    /// <summary>A calendar date (South African time); the time of day is not used.</summary>
    public DateTime? DueDate { get; set; }

    public int? AssignedToAdminUserId { get; set; }
    public AdminUser? AssignedTo { get; set; }

    public int CreatedByAdminUserId { get; set; }
    public AdminUser CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
