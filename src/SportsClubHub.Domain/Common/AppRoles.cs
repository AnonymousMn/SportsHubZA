namespace SportsClubHub.Domain.Common;

/// <summary>
/// Role names used for authorisation. Only Administrator exists today. A read-only
/// municipal role (aggregate statistics only) is a planned future addition.
/// </summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";
}
