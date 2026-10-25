using System.ComponentModel.DataAnnotations;
using SportsClubHub.Domain.Common;

namespace SportsClubHub.Domain.Entities;

/// <summary>A person allowed to sign in to the administration area. Only a password hash is stored.</summary>
public class AdminUser
{
    public int AdminUserId { get; set; }

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Role { get; set; } = AppRoles.Staff;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
