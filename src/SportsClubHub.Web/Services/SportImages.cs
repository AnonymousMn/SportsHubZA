using SportsClubHub.Domain.Entities;

namespace SportsClubHub.Web.Services;

/// <summary>
/// Chooses the photo for a sport. An administrator-supplied <see cref="Sport.ImageUrl"/> always wins; otherwise the
/// app looks for a self-hosted file at <c>wwwroot/images/sports/{key}.jpg</c> (see docs/SPORT_IMAGES.md). Pages
/// hide a missing image, so a sport without a photo still shows its coloured category panel.
/// </summary>
public static class SportImages
{
    /// <summary>Stable file-name key for a sport ("Table Tennis" becomes "tabletennis").</summary>
    public static string Key(string name) =>
        name.ToLowerInvariant() switch
        {
            "football" => "football",
            "rugby union" => "rugby",
            "cricket" => "cricket",
            "hockey" => "hockey",
            "netball" => "netball",
            "basketball" => "basketball",
            "volleyball" => "volleyball",
            "tennis" => "tennis",
            "badminton" => "badminton",
            "swimming" => "swimming",
            "water polo" => "waterpolo",
            "surfing" => "surfing",
            "athletics (track and field)" => "athletics",
            "road running" => "running",
            "boxing" => "boxing",
            "judo" => "judo",
            "taekwondo" => "taekwondo",
            "chess" => "chess",
            "table tennis" => "tabletennis",
            "squash" => "squash",
            "cycling" => "cycling",
            "archery" => "archery",
            "hiking and trail running" => "hiking",
            _ => "default"
        };

    /// <summary>The image address to try for this sport, or null when there is nothing to try.</summary>
    public static string? Resolve(Sport sport)
    {
        if (!string.IsNullOrWhiteSpace(sport.ImageUrl))
        {
            return sport.ImageUrl;
        }

        var key = Key(sport.Name);
        return key == "default" ? null : $"images/sports/{key}.jpg";
    }
}
