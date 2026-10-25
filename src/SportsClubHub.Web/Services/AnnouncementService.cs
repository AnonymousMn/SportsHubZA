using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

public class AnnouncementService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AnnouncementService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<Announcement>> GetPublishedAsync(DateTime? now = null)
    {
        var current = now ?? DateTime.UtcNow;
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Announcements.AsNoTracking()
            .Where(a => a.IsPublished &&
                        (a.PublishAt == null || a.PublishAt <= current) &&
                        (a.ExpiresAt == null || a.ExpiresAt > current))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.Priority)
            .ThenByDescending(a => a.PublishAt)
            .ToListAsync();
    }

    public async Task<List<Announcement>> ListAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Announcements.AsNoTracking()
            .OrderByDescending(a => a.IsPublished)
            .ThenByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.Priority)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<OperationResult> SaveAsync(Announcement announcement)
    {
        announcement.Title = InputNormaliser.Text(announcement.Title);
        announcement.Message = InputNormaliser.Text(announcement.Message);
        announcement.Category = InputNormaliser.Text(announcement.Category);
        if (announcement.Title.Length == 0 || announcement.Message.Length == 0 || announcement.Category.Length == 0)
        {
            return OperationResult.Fail("Title, message, and category are required.");
        }

        if (announcement.ExpiresAt is not null &&
            announcement.PublishAt is not null &&
            announcement.ExpiresAt <= announcement.PublishAt)
        {
            return OperationResult.Fail("Expiry must be after the publication time.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        if (announcement.AnnouncementId == 0)
        {
            announcement.CreatedAt = DateTime.UtcNow;
            db.Announcements.Add(announcement);
        }
        else
        {
            var existing = await db.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == announcement.AnnouncementId);
            if (existing is null)
            {
                return OperationResult.Fail("Announcement not found.");
            }

            existing.Title = announcement.Title;
            existing.Message = announcement.Message;
            existing.Category = announcement.Category;
            existing.PublishAt = announcement.PublishAt;
            existing.ExpiresAt = announcement.ExpiresAt;
            existing.IsPublished = announcement.IsPublished;
            existing.IsPinned = announcement.IsPinned;
            existing.Priority = announcement.Priority;
        }

        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetPublishedAsync(int id, bool published)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == id);
        if (announcement is null)
        {
            return OperationResult.Fail("Announcement not found.");
        }

        announcement.IsPublished = published;
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }
}
