using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Data;

namespace SportsClubHub.Web.Services;

/// <summary>
/// The administrators' shared to-do list. Every method receives the signed-in person's id and role from the page,
/// but permission is decided here (see <see cref="AdminTaskRules"/>), not in the browser.
/// </summary>
public class AdminTaskService
{
    private const int ListLimit = 500;
    private const int DoneLimit = 50;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly EmailService? _email;

    public AdminTaskService(IDbContextFactory<AppDbContext> factory, EmailService? email = null)
    {
        _factory = factory;
        _email = email;
    }

    /// <summary>Active staff who can be given a task.</summary>
    public async Task<List<AdminAssignee>> ListAssigneesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AdminUsers
            .Where(a => a.IsActive)
            .OrderBy(a => a.Email)
            .Select(a => new AdminAssignee(a.AdminUserId, a.Email, a.Role))
            .ToListAsync();
    }

    public async Task<List<AdminTaskItem>> ListAsync(AdminTaskView view, int actorId, string? search = null)
    {
        await using var db = await _factory.CreateDbContextAsync();

        IQueryable<AdminTask> query = db.AdminTasks;
        query = view switch
        {
            AdminTaskView.Mine => query.Where(t => t.Status != AdminTaskStatus.Done && t.AssignedToAdminUserId == actorId),
            AdminTaskView.TeamOpen => query.Where(t => t.Status != AdminTaskStatus.Done),
            AdminTaskView.Unassigned => query.Where(t => t.Status != AdminTaskStatus.Done && t.AssignedToAdminUserId == null),
            _ => query.Where(t => t.Status == AdminTaskStatus.Done)
        };

        var term = InputNormaliser.Text(search);
        if (term.Length > 0)
        {
            query = query.Where(t => t.Title.Contains(term) || (t.Notes != null && t.Notes.Contains(term)));
        }

        query = view == AdminTaskView.Done
            ? query.OrderByDescending(t => t.CompletedAt).Take(DoneLimit)
            : query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).Take(ListLimit);

        return await query
            .Select(t => new AdminTaskItem(
                t,
                t.AssignedTo == null ? null : t.AssignedTo.Email,
                t.CreatedBy.Email))
            .ToListAsync();
    }

    public async Task<AdminTaskCounts> GetCountsAsync(int actorId, DateTime? utcNow = null)
    {
        var today = AdminTaskRules.Today(utcNow ?? DateTime.UtcNow);
        await using var db = await _factory.CreateDbContextAsync();

        var open = await db.AdminTasks
            .Where(t => t.Status != AdminTaskStatus.Done)
            .Select(t => new { t.AssignedToAdminUserId, t.DueDate })
            .ToListAsync();

        bool Overdue(DateTime? due) => due is { } d && d.Date < today;
        return new AdminTaskCounts(
            MyOpen: open.Count(t => t.AssignedToAdminUserId == actorId),
            MyOverdue: open.Count(t => t.AssignedToAdminUserId == actorId && Overdue(t.DueDate)),
            OpenTotal: open.Count,
            TeamOverdue: open.Count(t => Overdue(t.DueDate)),
            Unassigned: open.Count(t => t.AssignedToAdminUserId == null));
    }

    public async Task<OperationResult> CreateAsync(AdminTaskInput input, int actorId)
    {
        var (clean, error) = AdminTaskRules.Normalise(input, AdminTaskRules.Today(DateTime.UtcNow));
        if (clean is null) { return OperationResult.Fail(error!); }

        await using var db = await _factory.CreateDbContextAsync();
        var actorEmail = await db.AdminUsers.Where(a => a.AdminUserId == actorId && a.IsActive).Select(a => a.Email).FirstOrDefaultAsync();
        if (actorEmail is null) { return OperationResult.Fail("Your account could not be found. Please sign in again."); }

        var assigneeError = await CheckAssigneeAsync(db, clean.AssignedToAdminUserId);
        if (assigneeError is not null) { return OperationResult.Fail(assigneeError); }

        var now = DateTime.UtcNow;
        var task = new AdminTask
        {
            Title = clean.Title!,
            Notes = clean.Notes,
            Priority = clean.Priority,
            DueDate = clean.DueDate,
            AssignedToAdminUserId = clean.AssignedToAdminUserId,
            CreatedByAdminUserId = actorId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AdminTasks.Add(task);
        await db.SaveChangesAsync();

        await NotifyAssigneeAsync(db, task, actorId, actorEmail);
        return OperationResult.Success();
    }

    public async Task<OperationResult> UpdateAsync(int taskId, AdminTaskInput input, int actorId, bool isAdministrator)
    {
        var (clean, error) = AdminTaskRules.Normalise(input, AdminTaskRules.Today(DateTime.UtcNow));
        if (clean is null) { return OperationResult.Fail(error!); }

        await using var db = await _factory.CreateDbContextAsync();
        var task = await db.AdminTasks.FirstOrDefaultAsync(t => t.AdminTaskId == taskId);
        if (task is null) { return OperationResult.Fail("That task no longer exists."); }
        if (!AdminTaskRules.CanModify(task, actorId, isAdministrator))
        {
            return OperationResult.Fail("You can only edit tasks you created or that are assigned to you.");
        }

        var assigneeChanged = task.AssignedToAdminUserId != clean.AssignedToAdminUserId;
        if (assigneeChanged)
        {
            var assigneeError = await CheckAssigneeAsync(db, clean.AssignedToAdminUserId);
            if (assigneeError is not null) { return OperationResult.Fail(assigneeError); }
        }

        task.Title = clean.Title!;
        task.Notes = clean.Notes;
        task.Priority = clean.Priority;
        task.DueDate = clean.DueDate;
        task.AssignedToAdminUserId = clean.AssignedToAdminUserId;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (assigneeChanged) { await NotifyAssigneeAsync(db, task, actorId, null); }
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetStatusAsync(int taskId, AdminTaskStatus status, int actorId, bool isAdministrator)
    {
        if (!Enum.IsDefined(status)) { return OperationResult.Fail("Choose a status."); }

        await using var db = await _factory.CreateDbContextAsync();
        var task = await db.AdminTasks.FirstOrDefaultAsync(t => t.AdminTaskId == taskId);
        if (task is null) { return OperationResult.Fail("That task no longer exists."); }
        if (!AdminTaskRules.CanModify(task, actorId, isAdministrator))
        {
            return OperationResult.Fail("You can only update tasks you created or that are assigned to you.");
        }

        if (task.Status != status)
        {
            var now = DateTime.UtcNow;
            task.Status = status;
            task.CompletedAt = status == AdminTaskStatus.Done ? now : null;
            task.UpdatedAt = now;
            await db.SaveChangesAsync();
        }

        return OperationResult.Success();
    }

    /// <summary>Hands the task to someone (or, with null, unassigns it). Anyone may claim an unassigned task.</summary>
    public async Task<OperationResult> AssignAsync(int taskId, int? assigneeId, int actorId, bool isAdministrator)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var task = await db.AdminTasks.FirstOrDefaultAsync(t => t.AdminTaskId == taskId);
        if (task is null) { return OperationResult.Fail("That task no longer exists."); }
        if (!AdminTaskRules.CanModify(task, actorId, isAdministrator) && !AdminTaskRules.CanClaim(task, actorId, assigneeId))
        {
            return OperationResult.Fail("You can only reassign tasks you created or that are assigned to you.");
        }

        if (task.AssignedToAdminUserId == assigneeId) { return OperationResult.Success(); }

        var assigneeError = await CheckAssigneeAsync(db, assigneeId);
        if (assigneeError is not null) { return OperationResult.Fail(assigneeError); }

        task.AssignedToAdminUserId = assigneeId;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await NotifyAssigneeAsync(db, task, actorId, null);
        return OperationResult.Success();
    }

    /// <summary>Only administrators and the person who created a task can delete it.</summary>
    public async Task<OperationResult> DeleteAsync(int taskId, int actorId, bool isAdministrator)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var task = await db.AdminTasks.FirstOrDefaultAsync(t => t.AdminTaskId == taskId);
        if (task is null) { return OperationResult.Success(); }
        if (!isAdministrator && task.CreatedByAdminUserId != actorId)
        {
            return OperationResult.Fail("Only an administrator or the person who created a task can delete it.");
        }

        db.AdminTasks.Remove(task);
        await db.SaveChangesAsync();
        return OperationResult.Success();
    }

    private static async Task<string?> CheckAssigneeAsync(AppDbContext db, int? assigneeId)
    {
        if (assigneeId is null) { return null; }

        return await db.AdminUsers.AnyAsync(a => a.AdminUserId == assigneeId && a.IsActive)
            ? null
            : "Choose an active staff member to assign the task to.";
    }

    /// <summary>E-mails the new assignee. Never emails people about tasks they gave themselves, and never fails the save.</summary>
    private async Task NotifyAssigneeAsync(AppDbContext db, AdminTask task, int actorId, string? actorEmail)
    {
        if (_email is null || task.AssignedToAdminUserId is not { } assigneeId || assigneeId == actorId) { return; }

        var assigneeEmail = await db.AdminUsers.Where(a => a.AdminUserId == assigneeId).Select(a => a.Email).FirstOrDefaultAsync();
        actorEmail ??= await db.AdminUsers.Where(a => a.AdminUserId == actorId).Select(a => a.Email).FirstOrDefaultAsync();
        if (assigneeEmail is null) { return; }

        var due = task.DueDate is { } date ? date.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : "no due date";
        await _email.TrySendAsync(
            assigneeEmail,
            $"Task assigned: {task.Title}",
            $"Hello,\n\n{actorEmail ?? "A colleague"} assigned you a task in the Sports Club Hub SA admin area.\n\n" +
            $"{task.Title}\nPriority: {task.Priority}\nDue: {due}\n\nSign in and open Tasks to see the details.\n\nSports Club Hub SA",
            "task assignment");
    }
}
