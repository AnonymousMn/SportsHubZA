namespace SportsClubHub.Web.Services;

/// <summary>
/// Returned by operations that can fail for a reason the user can fix
/// (for example "that name is already used"). Unexpected errors are exceptions instead.
/// </summary>
public record OperationResult(bool Succeeded, string? Error = null)
{
    public static OperationResult Success() => new(true);
    public static OperationResult Fail(string error) => new(false, error);
}
