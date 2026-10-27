using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class SportBrowseFilterTests
{
    private static SportListItem Item(string name, SportCategory category, int clubs) =>
        new(new Sport { Name = name, Category = category }, clubs);

    private static readonly List<SportListItem> Items = new()
    {
        Item("Football", SportCategory.FieldSport, 2),
        Item("Hockey", SportCategory.FieldSport, 0),
        Item("Netball", SportCategory.CourtSport, 1),
        Item("Swimming", SportCategory.Aquatic, 0),
    };

    [Fact]
    public void Apply_WithNoFilters_ReturnsEverything() =>
        Assert.Equal(4, SportBrowseFilter.Apply(Items, null, null, false).Count);

    [Fact]
    public void Apply_FiltersByCategory()
    {
        var result = SportBrowseFilter.Apply(Items, null, SportCategory.FieldSport, false);
        Assert.Equal(new[] { "Football", "Hockey" }, result.Select(i => i.Sport.Name));
    }

    [Theory]
    [InlineData("net", 1)]
    [InlineData("  NET  ", 1)]
    [InlineData("ball", 2)]
    [InlineData("zzz", 0)]
    public void Apply_SearchIsCaseInsensitiveAndTrimmed(string search, int expected) =>
        Assert.Equal(expected, SportBrowseFilter.Apply(Items, search, null, false).Count);

    [Fact]
    public void Apply_OnlyWithClubs_HidesSportsWithoutVerifiedClubs()
    {
        var result = SportBrowseFilter.Apply(Items, null, null, true);
        Assert.Equal(new[] { "Football", "Netball" }, result.Select(i => i.Sport.Name));
    }

    [Fact]
    public void CategoryCounts_IgnoreTheSelectedCategoryButRespectOtherFilters()
    {
        var counts = SportBrowseFilter.CategoryCounts(Items, null, true);
        Assert.Equal(1, counts[SportCategory.FieldSport]);
        Assert.Equal(1, counts[SportCategory.CourtSport]);
        Assert.False(counts.ContainsKey(SportCategory.Aquatic));
    }
}
