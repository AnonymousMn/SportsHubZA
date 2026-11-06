using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class HeroSettingsRulesTests
{
    private static HeroSettings Settings(HeroMediaKind kind = HeroMediaKind.Image, string? image = "https://images.example.test/hero.jpg", string? video = null, int overlay = 65, HeroFocus focus = HeroFocus.Center) =>
        new(kind, image, video, overlay, focus);

    [Fact]
    public void ValidImageSettings_AreAccepted()
    {
        var (clean, error) = HeroSettingsRules.Normalise(Settings());
        Assert.Null(error);
        Assert.Equal(HeroMediaKind.Image, clean!.Kind);
        Assert.True(clean.HasMedia);
    }

    [Theory]
    [InlineData("http://images.example.test/hero.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("ftp://example.test/hero.jpg")]
    [InlineData("//example.test/hero.jpg")]
    [InlineData("https://user:pass@example.test/hero.jpg")]
    [InlineData("not a url")]
    [InlineData("/media/abc")]
    [InlineData("/media/0")]
    [InlineData("/media/12/../../secret")]
    public void UnsafeOrMalformedImageAddresses_AreRejected(string url) =>
        Assert.NotNull(HeroSettingsRules.Normalise(Settings(image: url)).Error);

    [Theory]
    [InlineData("/media/12")]
    [InlineData("https://images.unsplash.com/photo-123?w=1920&q=80")]
    public void UploadedPathsAndHttpsAddresses_AreAccepted(string url) =>
        Assert.Null(HeroSettingsRules.Normalise(Settings(image: url)).Error);

    [Theory]
    [InlineData("https://cdn.example.test/hero.mp4")]
    [InlineData("https://cdn.example.test/hero.WEBM?token=abc")]
    public void DirectHttpsVideoFiles_AreAccepted(string url) =>
        Assert.Null(HeroSettingsRules.Normalise(Settings(HeroMediaKind.Video, video: url)).Error);

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://vimeo.com/12345")]
    [InlineData("http://cdn.example.test/hero.mp4")]
    [InlineData("https://cdn.example.test/hero.exe")]
    [InlineData("javascript:alert(1)//.mp4")]
    public void VideoPageLinksAndUnsafeAddresses_AreRejected(string url) =>
        Assert.NotNull(HeroSettingsRules.Normalise(Settings(HeroMediaKind.Video, video: url)).Error);

    [Fact]
    public void ChosenKindMustHaveItsMedia()
    {
        Assert.NotNull(HeroSettingsRules.Normalise(Settings(HeroMediaKind.Image, image: " ")).Error);
        Assert.NotNull(HeroSettingsRules.Normalise(Settings(HeroMediaKind.Video, video: null)).Error);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(96)]
    [InlineData(-5)]
    public void OverlayOutsideTheReadableRange_IsRejected(int overlay) =>
        Assert.NotNull(HeroSettingsRules.Normalise(Settings(overlay: overlay)).Error);

    [Theory]
    [InlineData(20)]
    [InlineData(65)]
    [InlineData(95)]
    public void OverlayInsideTheRange_IsAccepted(int overlay) =>
        Assert.Null(HeroSettingsRules.Normalise(Settings(overlay: overlay)).Error);

    [Fact]
    public void OnlyWhatTheChosenKindUsesIsKept()
    {
        var none = HeroSettingsRules.Normalise(Settings(HeroMediaKind.None, video: "https://cdn.example.test/a.mp4")).Clean!;
        Assert.Null(none.ImageUrl);
        Assert.Null(none.VideoUrl);
        Assert.False(none.HasMedia);

        var image = HeroSettingsRules.Normalise(Settings(HeroMediaKind.Image, video: "https://cdn.example.test/a.mp4")).Clean!;
        Assert.Null(image.VideoUrl);

        var video = HeroSettingsRules.Normalise(Settings(HeroMediaKind.Video, video: "https://cdn.example.test/a.mp4")).Clean!;
        Assert.Equal("https://images.example.test/hero.jpg", video.ImageUrl); // kept as the poster
    }

    [Fact]
    public void UploadedMediaPaths_RoundTrip()
    {
        Assert.Equal("/media/7", HeroSettingsRules.MediaUrl(7));
        Assert.True(HeroSettingsRules.TryGetMediaId("/media/7", out var id));
        Assert.Equal(7, id);
        Assert.False(HeroSettingsRules.TryGetMediaId("/media/07", out _));
        Assert.False(HeroSettingsRules.TryGetMediaId(null, out _));
    }

    [Theory]
    [InlineData(HeroFocus.Top, "center top")]
    [InlineData(HeroFocus.Center, "center center")]
    [InlineData(HeroFocus.Bottom, "center bottom")]
    public void FocusMapsToObjectPosition(HeroFocus focus, string css) => Assert.Equal(css, HeroSettingsRules.FocusCss(focus));

    [Fact]
    public void ImageSniffer_RecognisesRealImagesByTheirBytes()
    {
        Assert.Equal("image/jpeg", ImageSniffer.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }));
        Assert.Equal("image/png", ImageSniffer.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }));
        Assert.Equal("image/webp", ImageSniffer.Detect("RIFF\0\0\0\0WEBPVP8 "u8.ToArray()));
    }

    [Fact]
    public void ImageSniffer_RejectsEverythingElse()
    {
        Assert.Null(ImageSniffer.Detect("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"u8.ToArray()));
        Assert.Null(ImageSniffer.Detect("<html><script>alert(1)</script></html>"u8.ToArray()));
        Assert.Null(ImageSniffer.Detect("GIF89a"u8.ToArray()));
        Assert.Null(ImageSniffer.Detect(Array.Empty<byte>()));
        Assert.Null(ImageSniffer.Detect(new byte[] { 0xFF, 0xD8 }));
    }
}
