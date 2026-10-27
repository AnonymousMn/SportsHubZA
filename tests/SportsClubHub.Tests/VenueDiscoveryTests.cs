using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Components.Pages;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class VenueDiscoveryTests
{
    [Fact]
    public async Task Nearby_OnlyReturnsActiveMappedVenuesWithinRadiusInDistanceOrder()
    {
        using var database = new TestDatabase();
        var football = await database.AddSportAsync("Football");
        var tennis = await database.AddSportAsync("Tennis");
        await using (var db = database.CreateDbContext())
        {
            db.Venues.AddRange(
                Venue("Far", -26.2, 28.0, football),
                Venue("Close", -33.93, 18.43, football),
                Venue("Closest", -33.925, 18.424, football),
                Venue("Hidden", -33.925, 18.424, football, false),
                Venue("Unmapped", null, null, football),
                Venue("Invalid", 91, 18.424, football),
                Venue("Other sport", -33.925, 18.424, tennis));
            await db.SaveChangesAsync();
        }

        var results = await new VenueService(database).FindNearestAsync(-33.925, 18.424, football.SportId);
        Assert.Equal(new[] { "Closest", "Close" }, results.Select(v => v.Venue.Name));
        Assert.Equal(0, results[0].DistanceKm, 5);
        Assert.InRange(results[1].DistanceKm, 0.1, 2);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public async Task Nearby_RejectsInvalidCoordinates(double latitude, double longitude)
    {
        using var database = new TestDatabase();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new VenueService(database).FindNearestAsync(latitude, longitude));
    }

    [Fact]
    public async Task Page_WaitsForChoice_AndManualSearchDoesNotAskForLocation()
    {
        using var database = new TestDatabase();
        await using (var db = database.CreateDbContext())
        {
            db.Venues.AddRange(new Venue { Name = "Public court", Location = "Cape Town" },
                new Venue { Name = "Private court", Location = "Hidden area", IsActive = false });
            await db.SaveChangesAsync();
        }
        using var context = Context(database);
        var page = context.RenderComponent<Venues>();
        Assert.Contains("Use my location", page.Markup);
        Assert.Empty(page.FindAll(".item-card"));
        Assert.DoesNotContain(context.JSInterop.Invocations, i => i.Identifier == "sportsClubHub.getCurrentPosition");

        page.FindAll("button")[1].Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("datalist option")));
        page.Find("#venue-location").Input("Cape Town");
        page.WaitForAssertion(() => Assert.Contains("Public court", page.Find(".item-card").TextContent));
        page.Find("#venue-location").Input("");
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".item-card")));
        Assert.DoesNotContain(context.JSInterop.Invocations, i => i.Identifier == "sportsClubHub.getCurrentPosition");
    }

    [Fact]
    public void Page_LocationDenialOffersManualRecovery()
    {
        using var database = new TestDatabase();
        using var context = Context(database);
        context.JSInterop.Setup<Venues.BrowserLocation>("sportsClubHub.getCurrentPosition")
            .SetException(new JSException("Denied"));
        var page = context.RenderComponent<Venues>();
        page.Find("button").Click();
        page.WaitForAssertion(() => Assert.Contains("Choose an area manually", page.Find("[role=alert]").TextContent));
        Assert.Empty(page.FindAll(".item-card"));
        Assert.False(page.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Page_AfterConsentDisplaysDistance_AndSportFilterKeepsLocation()
    {
        using var database = new TestDatabase();
        var sport = await database.AddSportAsync("Football");
        var other = await database.AddSportAsync("Tennis");
        await using (var db = database.CreateDbContext())
        {
            db.Venues.Add(Venue("Close court", -33.925, 18.424, sport));
            await db.SaveChangesAsync();
        }
        using var context = Context(database);
        context.JSInterop.Setup<Venues.BrowserLocation>("sportsClubHub.getCurrentPosition")
            .SetResult(new Venues.BrowserLocation(-33.925, 18.424));
        var page = context.RenderComponent<Venues>();
        page.Find("button").Click();
        page.WaitForAssertion(() => Assert.Contains("km away", page.Find(".item-card").TextContent));
        page.Find("#venue-sport").Change(other.SportId.ToString());
        page.WaitForAssertion(() => Assert.Contains("No mapped venues within 50 km", page.Markup));
        Assert.Single(context.JSInterop.Invocations, i => i.Identifier == "sportsClubHub.getCurrentPosition");
    }

    private static TestContext Context(TestDatabase database)
    {
        var context = new TestContext();
        context.Services.AddSingleton(new VenueService(database));
        context.Services.AddSingleton(new SportService(database));
        context.JSInterop.SetupVoid("sportsClubHub.setTitle", "Venues").SetVoidResult();
        return context;
    }

    private static Venue Venue(string name, double? latitude, double? longitude, Sport sport, bool active = true) => new()
    {
        Name = name, Location = "Cape Town", Latitude = latitude, Longitude = longitude, IsActive = active,
        VenueSports = new List<VenueSport> { new() { SportId = sport.SportId } }
    };
}
