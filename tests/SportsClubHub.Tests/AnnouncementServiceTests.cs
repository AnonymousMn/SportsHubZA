using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class AnnouncementServiceTests
{
    [Fact]
    public async Task PublishedList_ExcludesDraftsAndExpiredItems()
    {
        using var database = new TestDatabase();
        var service = new AnnouncementService(database);
        var now = DateTime.UtcNow;

        await service.SaveAsync(new Announcement { Title = "Live", Message = "Now", Category = "Update", IsPublished = true, PublishAt = now.AddMinutes(-1) });
        await service.SaveAsync(new Announcement { Title = "Draft", Message = "Later", Category = "Update" });
        await service.SaveAsync(new Announcement { Title = "Expired", Message = "Past", Category = "Update", IsPublished = true, ExpiresAt = now.AddMinutes(-1) });

        var results = await service.GetPublishedAsync(now);

        var item = Assert.Single(results);
        Assert.Equal("Live", item.Title);
    }

    [Fact]
    public async Task Save_RejectsExpiryBeforePublication()
    {
        using var database = new TestDatabase();
        var service = new AnnouncementService(database);

        var result = await service.SaveAsync(new Announcement
        {
            Title = "Holiday",
            Message = "Closed",
            Category = "Notice",
            PublishAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });

        Assert.False(result.Succeeded);
    }
}
