namespace SportsClubHub.Domain.Entities;

public enum HeroMediaKind
{
    None,
    Image,
    Video
}

/// <summary>Which part of a photo stays visible when the hero crops it.</summary>
public enum HeroFocus
{
    Center,
    Top,
    Bottom
}

/// <summary>Website appearance settings managed by administrators. There is only ever one row (id 1).</summary>
public class SiteSettings
{
    public const int SingleRowId = 1;

    public int SiteSettingsId { get; set; } = SingleRowId;
    public HeroMediaKind HeroMediaKind { get; set; } = HeroMediaKind.None;

    /// <summary>An https address, or a /media/{id} path for a photo uploaded through the admin area. Also the video's poster.</summary>
    public string? HeroImageUrl { get; set; }

    /// <summary>A direct https link to an .mp4 or .webm file.</summary>
    public string? HeroVideoUrl { get; set; }

    /// <summary>How strongly the brand-colour overlay covers the media, as a percentage.</summary>
    public int HeroOverlayOpacity { get; set; } = 65;

    public HeroFocus HeroFocus { get; set; } = HeroFocus.Center;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An image uploaded by an administrator and served from /media/{id}. Rows are never edited, only replaced.</summary>
public class SiteMedia
{
    public int SiteMediaId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
