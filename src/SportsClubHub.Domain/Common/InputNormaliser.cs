using System.Text.RegularExpressions;

namespace SportsClubHub.Domain.Common;

/// <summary>
/// Small helpers that clean up text typed by users so that "  Jo@Example.com "
/// and "jo@example.com" are treated as the same value. Keeping this in one place
/// means every service normalises input in exactly the same way.
/// </summary>
public static partial class InputNormaliser
{
    /// <summary>Trims the text and collapses runs of whitespace into one space.</summary>
    public static string Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(value.Trim(), " ");
    }

    /// <summary>Returns null instead of an empty string, which suits optional fields.</summary>
    public static string? NullIfBlank(string? value)
    {
        var cleaned = Text(value);
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>E-mail addresses are compared case-insensitively, so store them in lower case.</summary>
    public static string Email(string? value) => Text(value).ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
