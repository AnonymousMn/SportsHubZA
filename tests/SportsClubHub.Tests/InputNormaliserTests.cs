using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;

namespace SportsClubHub.Tests;

public class InputNormaliserTests
{
    [Theory]
    [InlineData("  Jo   Smith ", "Jo Smith")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Text_TrimsAndCollapsesWhitespace(string? input, string expected) =>
        Assert.Equal(expected, InputNormaliser.Text(input));

    [Fact]
    public void Email_IsTrimmedAndLowerCased() =>
        Assert.Equal("jo@example.test", InputNormaliser.Email("  Jo@Example.TEST "));

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(null)]
    public void NullIfBlank_ReturnsNullForBlankText(string? input) =>
        Assert.Null(InputNormaliser.NullIfBlank(input));

    [Fact]
    public void ToDisplayText_SplitsPascalCase() =>
        Assert.Equal("Court Sport", SportCategory.CourtSport.ToDisplayText());

    [Fact]
    public void Club_BlankOptionalFieldsBecomeNull()
    {
        var club = new Club { ContactPhone = "  ", Website = "" };
        Assert.Null(club.ContactPhone);
        Assert.Null(club.Website);
    }
}
