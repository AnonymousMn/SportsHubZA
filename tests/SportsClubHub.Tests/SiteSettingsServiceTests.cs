using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class SiteSettingsServiceTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };

    private static MemoryStream Stream(byte[] bytes) => new(bytes);

    [Fact]
    public async Task NothingSaved_ShowsThePlainHero()
    {
        using var database = new TestDatabase();
        var hero = await new SiteSettingsService(database).GetHeroAsync();
        Assert.Equal(HeroSettings.Default, hero);
        Assert.False(hero.HasMedia);
    }

    [Fact]
    public async Task SavedSettings_AreReadBack()
    {
        using var database = new TestDatabase();
        var service = new SiteSettingsService(database);

        var result = await service.SaveHeroAsync(new HeroSettings(HeroMediaKind.Video, "https://cdn.example.test/poster.jpg", "https://cdn.example.test/hero.mp4", 50, HeroFocus.Top));
        var hero = await service.GetHeroAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(HeroMediaKind.Video, hero.Kind);
        Assert.Equal("https://cdn.example.test/hero.mp4", hero.VideoUrl);
        Assert.Equal(50, hero.OverlayOpacity);
        Assert.Equal(HeroFocus.Top, hero.Focus);

        // Saving again updates the same single row.
        await service.SaveHeroAsync(HeroSettings.Default);
        Assert.False((await service.GetHeroAsync()).HasMedia);
    }

    [Fact]
    public async Task InvalidSettings_AreRefusedAndNothingChanges()
    {
        using var database = new TestDatabase();
        var service = new SiteSettingsService(database);

        var result = await service.SaveHeroAsync(new HeroSettings(HeroMediaKind.Image, "javascript:alert(1)", null, 65, HeroFocus.Center));

        Assert.False(result.Succeeded);
        Assert.False((await service.GetHeroAsync()).HasMedia);
    }

    [Fact]
    public async Task UploadedImage_IsStoredServedAndCanBeUsedAsTheHero()
    {
        using var database = new TestDatabase();
        var service = new SiteSettingsService(database);

        var upload = await service.UploadHeroImageAsync(Stream(Png));
        Assert.True(upload.Succeeded);
        Assert.StartsWith("/media/", upload.Url);

        Assert.True((await service.SaveHeroAsync(new HeroSettings(HeroMediaKind.Image, upload.Url, null, 65, HeroFocus.Center))).Succeeded);

        HeroSettingsRules.TryGetMediaId(upload.Url, out var id);
        var media = await service.GetMediaAsync(id);
        Assert.Equal("image/png", media!.Value.ContentType);
        Assert.Equal(Png, media.Value.Data);
    }

    [Fact]
    public async Task FilesThatAreNotImages_AreRejected_WhateverTheyAreCalled()
    {
        using var database = new TestDatabase();
        var service = new SiteSettingsService(database);

        var svg = await service.UploadHeroImageAsync(Stream("<svg onload='alert(1)'></svg>"u8.ToArray()));
        var script = await service.UploadHeroImageAsync(Stream("<script>alert(1)</script>"u8.ToArray()));

        Assert.False(svg.Succeeded);
        Assert.False(script.Succeeded);
        Assert.Null(await service.GetMediaAsync(1));
    }

    [Fact]
    public async Task OversizedUploads_AreRejected()
    {
        using var database = new TestDatabase();
        var big = new byte[HeroSettingsRules.MaxUploadBytes + 10];
        Png.CopyTo(big, 0);

        var result = await new SiteSettingsService(database).UploadHeroImageAsync(Stream(big));

        Assert.False(result.Succeeded);
        Assert.Contains("MB", result.Error);
    }

    [Fact]
    public async Task ReplacingTheImage_DeletesTheOldUpload()
    {
        using var database = new TestDatabase();
        var service = new SiteSettingsService(database);
        var first = await service.UploadHeroImageAsync(Stream(Png));
        await service.SaveHeroAsync(new HeroSettings(HeroMediaKind.Image, first.Url, null, 65, HeroFocus.Center));

        var second = await service.UploadHeroImageAsync(Stream(Png));
        await service.SaveHeroAsync(new HeroSettings(HeroMediaKind.Image, second.Url, null, 65, HeroFocus.Center));

        HeroSettingsRules.TryGetMediaId(first.Url, out var firstId);
        HeroSettingsRules.TryGetMediaId(second.Url, out var secondId);
        Assert.Null(await service.GetMediaAsync(firstId));
        Assert.NotNull(await service.GetMediaAsync(secondId));
    }

    [Fact]
    public async Task SavingAnUploadThatNoLongerExists_IsRefused()
    {
        using var database = new TestDatabase();
        var result = await new SiteSettingsService(database).SaveHeroAsync(new HeroSettings(HeroMediaKind.Image, "/media/999", null, 65, HeroFocus.Center));
        Assert.False(result.Succeeded);
    }
}
