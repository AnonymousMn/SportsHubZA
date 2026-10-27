using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;

namespace SportsClubHub.Web.Services;

/// <summary>
/// In-memory filtering for the sports browse page. The page loads the (small) list of active sports once and
/// filters it here, so category chips, the search box and the "has clubs" toggle respond instantly and the
/// category counts always match what the person would see after choosing that chip.
/// </summary>
public static class SportBrowseFilter
{
    /// <summary>Applies the search text, optional category and "only sports with verified clubs" switch.</summary>
    public static List<SportListItem> Apply(
        IEnumerable<SportListItem> items, string? search, SportCategory? category, bool onlyWithClubs) =>
        Base(items, search, onlyWithClubs)
            .Where(i => category is null || i.Sport.Category == category)
            .ToList();

    /// <summary>
    /// Counts per category for the chips. The category itself is deliberately ignored so every chip shows
    /// how many sports it would reveal; the search text and clubs switch are respected.
    /// </summary>
    public static Dictionary<SportCategory, int> CategoryCounts(
        IEnumerable<SportListItem> items, string? search, bool onlyWithClubs) =>
        Base(items, search, onlyWithClubs)
            .GroupBy(i => i.Sport.Category)
            .ToDictionary(g => g.Key, g => g.Count());

    private static IEnumerable<SportListItem> Base(IEnumerable<SportListItem> items, string? search, bool onlyWithClubs)
    {
        var term = InputNormaliser.Text(search);
        return items.Where(i =>
            (!onlyWithClubs || i.ClubCount > 0) &&
            (term.Length == 0 || i.Sport.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
}
