using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class VenueImportTests
{
    [Fact]
    public async Task Preview_ParsesVenueAndMatchesSportsCaseInsensitively()
    {
        using var database = new TestDatabase();
        await database.AddSportAsync("Football");
        await database.AddSportAsync("Cricket");
        var service = new VenueService(database);

        var rows = await service.PreviewImportAsync(new[]
        {
            ("venues.csv", "Name,Location,Address,VenueType,SurfaceType,Capacity,Sports\n" +
             "Central Ground, Gqeberha,1 Main Road,Field,Natural grass,2500,football;Cricket")
        });

        var row = Assert.Single(rows);
        Assert.Null(row.Error);
        Assert.Equal("Central Ground", row.Venue!.Name);
        Assert.Equal(2500, row.Venue.Capacity);
        Assert.Equal(new[] { "football", "Cricket" }, row.SportNames);
    }

    [Fact]
    public async Task Preview_AcceptsUtf8BomAndOptionalColumnsInDifferentOrder()
    {
        using var database = new TestDatabase();
        await database.AddSportAsync("Football");
        var service = new VenueService(database);

        var rows = await service.PreviewImportAsync(new[]
        {
            ("venues.csv", "\uFEFFSports,Name,VenueType,Location\nFootball,Central Ground,Field,Gqeberha")
        });

        var row = Assert.Single(rows);
        Assert.Null(row.Error);
        Assert.Equal("Central Ground", row.Venue!.Name);
    }

    [Fact]
    public async Task Preview_ReportsMissingHeadersAndInvalidValues()
    {
        using var database = new TestDatabase();
        await database.AddSportAsync("Football");
        var service = new VenueService(database);

        var missingHeader = await service.PreviewImportAsync(new[]
        {
            ("venues.csv", "Name,Location\nCentral Ground,Gqeberha")
        });
        var invalidRow = await service.PreviewImportAsync(new[]
        {
            ("venues.csv", "Name,Location,Address,VenueType,SurfaceType,Capacity,Sports\n" +
             "Central Ground,Gqeberha,,,Indoor,0,Football")
        });

        Assert.Contains("Missing required header", Assert.Single(missingHeader).Error);
        Assert.Contains("VenueType", Assert.Single(invalidRow).Error);
    }

    [Fact]
    public async Task Preview_ReportsDuplicateRowsBeforeImport()
    {
        using var database = new TestDatabase();
        await database.AddSportAsync("Football");
        var service = new VenueService(database);
        var content = "Name,Location,Address,VenueType,SurfaceType,Capacity,Sports\n" +
                      "Central Ground,Gqeberha,,Field,Natural grass,,Football\n" +
                      "central ground,GQEBERHA,,Field,Natural grass,,Football";

        var rows = await service.PreviewImportAsync(new[] { ("venues.csv", content) });

        Assert.Null(rows[0].Error);
        Assert.Contains("Duplicate venue", rows[1].Error);
    }

    [Fact]
    public async Task Import_SavesVenueAndSportRelationships()
    {
        using var database = new TestDatabase();
        var football = await database.AddSportAsync("Football");
        await database.AddSportAsync("Cricket");
        var service = new VenueService(database);
        var rows = await service.PreviewImportAsync(new[]
        {
            ("venues.txt", "Name,Location,Address,VenueType,SurfaceType,Capacity,Sports\n" +
             "Central Ground,Gqeberha,,Field,Natural grass,,Football;Cricket")
        });

        var result = await service.ImportAsync(rows);
        var saved = Assert.Single(await service.SearchAsync(null, null));

        Assert.True(result.Succeeded);
        Assert.Equal("Central Ground", saved.Name);
        Assert.Contains(saved.VenueSports, venueSport => venueSport.SportId == football.SportId);
        Assert.Equal(2, saved.VenueSports.Count);
    }

    [Fact]
    public async Task Import_SavesValidRowsWhenAnotherRowIsInvalid()
    {
        using var database = new TestDatabase();
        await database.AddSportAsync("Football");
        var service = new VenueService(database);
        var rows = await service.PreviewImportAsync(new[]
        {
            ("venues.csv", "Name,Location,VenueType,Sports\n" +
             "Good Ground,Gqeberha,Field,Football\n" +
             "Bad Ground,Gqeberha,Unknown,Football")
        });

        var result = await service.ImportAsync(rows);

        Assert.True(result.Succeeded);
        Assert.Single(await service.SearchAsync(null, null));
        Assert.NotNull(rows[1].Error);
    }

    [Fact]
    public async Task Preview_DeduplicatesRepeatedSportsInOneRow()
    {
        using var database = new TestDatabase();
        await database.AddSportAsync("Football");
        var service = new VenueService(database);

        var rows = await service.PreviewImportAsync(new[]
        {
            ("venues.csv", "Name,Location,VenueType,Sports\n" +
             "Central Ground,Gqeberha,Field,Football;football;Football")
        });

        Assert.Equal(new[] { "Football" }, Assert.Single(rows).SportNames);
    }
}
