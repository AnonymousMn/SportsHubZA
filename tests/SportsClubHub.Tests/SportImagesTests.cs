using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class SportImagesTests
{
    [Theory]
    [InlineData("Netball", "netball")]
    [InlineData("TABLE TENNIS", "tabletennis")]
    [InlineData("Rugby Union", "rugby")]
    [InlineData("Quidditch", "default")]
    public void Key_MapsSportNamesToFileKeys(string name, string expected) =>
        Assert.Equal(expected, SportImages.Key(name));

    [Fact]
    public void Resolve_PrefersAdministratorImageUrl()
    {
        var sport = new Sport { Name = "Netball", ImageUrl = "https://images.example.test/netball.jpg" };
        Assert.Equal("https://images.example.test/netball.jpg", SportImages.Resolve(sport));
    }

    [Fact]
    public void Resolve_FallsBackToSelfHostedFile() =>
        Assert.Equal("images/sports/volleyball.jpg", SportImages.Resolve(new Sport { Name = "Volleyball" }));

    [Fact]
    public void Resolve_ReturnsNullForUnknownSportWithoutImage() =>
        Assert.Null(SportImages.Resolve(new Sport { Name = "Quidditch", ImageUrl = "  " }));
}
