using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public sealed record UploadResult(bool Succeeded, string? Url, string? Error);

/// <summary>Reads and changes website appearance settings (currently the home page hero).</summary>
public class SiteSettingsService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<SiteSettingsService> _logger;

    public SiteSettingsService(IDbContextFactory<AppDbContext> factory, ILogger<SiteSettingsService>? logger = null)
    {
        _factory = factory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SiteSettingsService>.Instance;
    }

    /// <summary>The hero settings. The public home page must never fail because of this, so problems fall back to the plain hero.</summary>
    public async Task<HeroSettings> GetHeroAsync()
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            var row = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(s => s.SiteSettingsId == SiteSettings.SingleRowId);
            if (row is null) { return HeroSettings.Default; }

            var settings = new HeroSettings(row.HeroMediaKind, row.HeroImageUrl, row.HeroVideoUrl, row.HeroOverlayOpacity, row.HeroFocus);
            return HeroSettingsRules.Normalise(settings).Clean ?? HeroSettings.Default;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.LogError("Could not load hero settings ({Reason}); showing the plain hero.", exception.GetType().Name);
            return HeroSettings.Default;
        }
    }

    public async Task<OperationResult> SaveHeroAsync(HeroSettings input)
    {
        var (clean, error) = HeroSettingsRules.Normalise(input);
        if (clean is null) { return OperationResult.Fail(error!); }

        await using var db = await _factory.CreateDbContextAsync();

        if (HeroSettingsRules.TryGetMediaId(clean.ImageUrl, out var mediaId) &&
            !await db.SiteMedia.AnyAsync(m => m.SiteMediaId == mediaId))
        {
            return OperationResult.Fail("That uploaded image no longer exists. Upload it again.");
        }

        var row = await db.SiteSettings.FirstOrDefaultAsync(s => s.SiteSettingsId == SiteSettings.SingleRowId);
        if (row is null)
        {
            row = new SiteSettings();
            db.SiteSettings.Add(row);
        }

        row.HeroMediaKind = clean.Kind;
        row.HeroImageUrl = clean.ImageUrl;
        row.HeroVideoUrl = clean.VideoUrl;
        row.HeroOverlayOpacity = clean.OverlayOpacity;
        row.HeroFocus = clean.Focus;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await DeleteUnusedMediaAsync(db, clean.ImageUrl);
        return OperationResult.Success();
    }

    /// <summary>Stores an uploaded image. Type is decided from the file's bytes; size is capped; nothing is trusted from the client.</summary>
    public async Task<UploadResult> UploadHeroImageAsync(Stream content)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > HeroSettingsRules.MaxUploadBytes)
            {
                return new UploadResult(false, null, $"The image is larger than {HeroSettingsRules.MaxUploadBytes / (1024 * 1024)} MB. Resize or compress it (about 1920 pixels wide is plenty) and try again.");
            }

            buffer.Write(chunk, 0, read);
        }

        var data = buffer.ToArray();
        var contentType = ImageSniffer.Detect(data);
        if (contentType is null)
        {
            return new UploadResult(false, null, "That file is not a JPEG, PNG or WebP image.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        var media = new SiteMedia { ContentType = contentType, Data = data, CreatedAt = DateTime.UtcNow };
        db.SiteMedia.Add(media);
        await db.SaveChangesAsync();

        // Earlier uploads that were never saved into the settings are dropped here, so the table cannot grow unnoticed.
        var saved = await db.SiteSettings.AsNoTracking().Select(s => s.HeroImageUrl).FirstOrDefaultAsync();
        var keepIds = new List<int> { media.SiteMediaId };
        if (HeroSettingsRules.TryGetMediaId(saved, out var savedId)) { keepIds.Add(savedId); }
        await db.SiteMedia.Where(m => !keepIds.Contains(m.SiteMediaId)).ExecuteDeleteAsync();

        return new UploadResult(true, HeroSettingsRules.MediaUrl(media.SiteMediaId), null);
    }

    public async Task<(string ContentType, byte[] Data)?> GetMediaAsync(int mediaId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var media = await db.SiteMedia.AsNoTracking().FirstOrDefaultAsync(m => m.SiteMediaId == mediaId);
        return media is null ? null : (media.ContentType, media.Data);
    }

    private static async Task DeleteUnusedMediaAsync(AppDbContext db, string? imageUrl)
    {
        var keep = HeroSettingsRules.TryGetMediaId(imageUrl, out var id) ? id : 0;
        await db.SiteMedia.Where(m => m.SiteMediaId != keep).ExecuteDeleteAsync();
    }
}
