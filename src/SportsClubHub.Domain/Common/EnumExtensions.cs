using System.Text.RegularExpressions;

namespace SportsClubHub.Domain.Common;

public static partial class EnumExtensions
{
    /// <summary>Turns "CourtSport" into "Court Sport" for display in the UI.</summary>
    public static string ToDisplayText(this Enum value) =>
        PascalCaseBoundary().Replace(value.ToString(), " $1");

    [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
