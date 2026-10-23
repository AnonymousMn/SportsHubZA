using System.ComponentModel.DataAnnotations;

namespace SportsClubHub.Domain.Entities;

/// <summary>A person allowed to sign in to the administration area. Only a password hash is stored.</summary>
public class AdminUser
{
    public int AdminUserId { get; set; }

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
