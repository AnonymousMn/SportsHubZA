using SportsClubHub.Domain.Entities;

namespace SportsClubHub.Web.Services;

/// <summary>What the home page hero should show.</summary>
public sealed record HeroSettings(
    HeroMediaKind Kind,
    string? ImageUrl,
    string? VideoUrl,
    int OverlayOpacity,
    HeroFocus Focus)
{
    public static HeroSettings Default { get; } = new(HeroMediaKind.None, null, null, HeroSettingsRules.DefaultOverlay, HeroFocus.Center);

    public bool HasMedia =>
        (Kind == HeroMediaKind.Image && !string.IsNullOrEmpty(ImageUrl)) ||
        (Kind == HeroMediaKind.Video && !string.IsNullOrEmpty(VideoUrl));
}

/// <summary>Checks and cleans hero settings. Pure, so the security-relevant rules (which addresses are allowed) are easy to test.</summary>
public static class HeroSettingsRules
{
    public const int MinOverlay = 20;
    public const int MaxOverlay = 95;
    public const int DefaultOverlay = 65;
    public const int MaxUploadBytes = 4 * 1024 * 1024;
    private const int MaxUrlLength = 500;
    private static readonly string[] VideoExtensions = { ".mp4", ".webm", ".m4v" };

    public static string MediaUrl(int mediaId) => $"/media/{mediaId}";

    /// <summary>True for the path of an uploaded image, such as "/media/12".</summary>
    public static bool TryGetMediaId(string? url, out int mediaId)
    {
        mediaId = 0;
        return url is not null && url.StartsWith("/media/", StringComparison.Ordinal) &&
               int.TryParse(url.AsSpan("/media/".Length), out mediaId) && mediaId > 0 && url == MediaUrl(mediaId);
    }

    /// <summary>The CSS object-position that keeps the chosen part of a photo in view.</summary>
    public static string FocusCss(HeroFocus focus) => focus switch
    {
        HeroFocus.Top => "center top",
        HeroFocus.Bottom => "center bottom",
        _ => "center center"
    };

    public static (HeroSettings? Clean, string? Error) Normalise(HeroSettings input)
    {
        if (!Enum.IsDefined(input.Kind)) { return (null, "Choose none, image or video."); }
        if (!Enum.IsDefined(input.Focus)) { return (null, "Choose where the picture should stay in view."); }
        if (input.OverlayOpacity < MinOverlay || input.OverlayOpacity > MaxOverlay)
        {
            return (null, $"Overlay strength must be between {MinOverlay}% and {MaxOverlay}% so the headline stays readable.");
        }

        var image = Blank(input.ImageUrl);
        if (image is not null && !TryGetMediaId(image, out _) && !IsHttpsUrl(image))
        {
            return (null, "The image address must start with https:// (or be an image uploaded here).");
        }

        var video = Blank(input.VideoUrl);
        if (video is not null)
        {
            if (!IsHttpsUrl(video)) { return (null, "The video address must start with https://."); }
            if (!HasVideoExtension(video))
            {
                return (null, "Use a direct link to an .mp4 or .webm file. YouTube and Vimeo page links cannot be used as a background.");
            }
        }

        if (input.Kind == HeroMediaKind.Image && image is null) { return (null, "Upload an image or paste an image address."); }
        if (input.Kind == HeroMediaKind.Video && video is null) { return (null, "Paste the address of an .mp4 or .webm video."); }

        // Only keep what the chosen kind uses (a video keeps its image as the poster).
        return (new HeroSettings(
            input.Kind,
            input.Kind == HeroMediaKind.None ? null : image,
            input.Kind == HeroMediaKind.Video ? video : null,
            input.OverlayOpacity,
            input.Focus), null);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsHttpsUrl(string value) =>
        value.Length <= MaxUrlLength &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Length > 0 &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static bool HasVideoExtension(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        VideoExtensions.Any(ext => uri.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Recognises uploaded images by their first bytes, never by the file name or the browser's claim.</summary>
public static class ImageSniffer
{
    /// <summary>"image/jpeg", "image/png" or "image/webp"; null for anything else (SVG is deliberately not allowed).</summary>
    public static string? Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) { return "image/jpeg"; }

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A) { return "image/png"; }

        if (data.Length >= 12 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F' &&
            data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P') { return "image/webp"; }

        return null;
    }
}
