using System.Globalization;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;

namespace SportsClubHub.Web.Services;

public record AdminTaskItem(AdminTask Task, string? AssigneeEmail, string CreatorEmail);
public record AdminAssignee(int AdminUserId, string Email, string Role);
public record AdminTaskCounts(int MyOpen, int MyOverdue, int OpenTotal, int TeamOverdue, int Unassigned);

public enum AdminTaskView { Mine, TeamOpen, Unassigned, Done }
public enum TaskSection { Overdue, Today, ThisWeek, Later, NoDate, Done }

/// <summary>What the person typed into the task form, before it is cleaned and checked.</summary>
public class AdminTaskInput
{
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public AdminTaskPriority Priority { get; set; } = AdminTaskPriority.Normal;
    public DateTime? DueDate { get; set; }
    public int? AssignedToAdminUserId { get; set; }
}

/// <summary>Pure rules for admin tasks (dates, grouping, permissions, validation), kept apart from the database code so they are easy to test.</summary>
public static class AdminTaskRules
{
    public const int MaxTitleLength = 150;
    public const int MaxNotesLength = 2000;

    /// <summary>Today's date in South Africa (UTC+2 all year, no daylight saving), so tasks do not turn overdue at 22:00.</summary>
    public static DateTime Today(DateTime utcNow) => utcNow.AddHours(2).Date;

    public static bool IsOverdue(AdminTask task, DateTime today) =>
        task.Status != AdminTaskStatus.Done && task.DueDate is { } due && due.Date < today;

    public static TaskSection SectionOf(AdminTask task, DateTime today)
    {
        if (task.Status == AdminTaskStatus.Done) { return TaskSection.Done; }
        if (task.DueDate is not { } due) { return TaskSection.NoDate; }

        var days = (due.Date - today).Days;
        return days < 0 ? TaskSection.Overdue
            : days == 0 ? TaskSection.Today
            : days <= 7 ? TaskSection.ThisWeek
            : TaskSection.Later;
    }

    public static string DueLabel(DateTime? due, DateTime today)
    {
        if (due is not { } date) { return "No due date"; }

        var days = (date.Date - today).Days;
        return days switch
        {
            < -1 => $"Overdue by {-days} days",
            -1 => "Overdue by 1 day",
            0 => "Due today",
            1 => "Due tomorrow",
            <= 7 => $"Due in {days} days",
            _ => "Due " + date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Groups tasks into sections (empty sections are left out), most urgent first inside each section.</summary>
    public static List<(TaskSection Section, List<AdminTaskItem> Items)> Group(IEnumerable<AdminTaskItem> items, DateTime today) =>
        items
            .GroupBy(i => SectionOf(i.Task, today))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g
                .OrderByDescending(i => i.Task.Priority)
                .ThenBy(i => i.Task.DueDate ?? DateTime.MaxValue)
                .ThenBy(i => i.Task.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()))
            .ToList();

    public static string SectionTitle(TaskSection section) => section switch
    {
        TaskSection.Overdue => "Overdue",
        TaskSection.Today => "Due today",
        TaskSection.ThisWeek => "Next 7 days",
        TaskSection.Later => "Later",
        TaskSection.NoDate => "No due date",
        _ => "Done"
    };

    /// <summary>Administrators can change any task; staff can change tasks they created or are assigned to.</summary>
    public static bool CanModify(AdminTask task, int actorId, bool isAdministrator) =>
        isAdministrator || task.CreatedByAdminUserId == actorId || task.AssignedToAdminUserId == actorId;

    /// <summary>Anyone may take an unassigned task for themselves.</summary>
    public static bool CanClaim(AdminTask task, int actorId, int? newAssigneeId) =>
        task.AssignedToAdminUserId is null && newAssigneeId == actorId;

    /// <summary>Cleans the form text and checks it. Returns the cleaned input, or an error the person can fix.</summary>
    public static (AdminTaskInput? Clean, string? Error) Normalise(AdminTaskInput input, DateTime today)
    {
        var title = InputNormaliser.Text(input.Title);
        if (title.Length == 0) { return (null, "Give the task a title."); }
        if (title.Length > MaxTitleLength) { return (null, $"Keep the title to {MaxTitleLength} characters or fewer."); }

        // Notes keep their line breaks, so they are only trimmed.
        var notes = input.Notes?.Replace("\r\n", "\n").Trim();
        if (string.IsNullOrEmpty(notes)) { notes = null; }
        if (notes is { Length: > MaxNotesLength }) { return (null, $"Keep the notes to {MaxNotesLength} characters or fewer."); }

        if (!Enum.IsDefined(input.Priority)) { return (null, "Choose a priority."); }

        var due = input.DueDate?.Date;
        if (due is { } d && (d < today.AddYears(-1) || d > today.AddYears(5)))
        {
            return (null, "Choose a due date within the next five years.");
        }

        return (new AdminTaskInput
        {
            Title = title,
            Notes = notes,
            Priority = input.Priority,
            DueDate = due,
            AssignedToAdminUserId = input.AssignedToAdminUserId
        }, null);
    }
}
