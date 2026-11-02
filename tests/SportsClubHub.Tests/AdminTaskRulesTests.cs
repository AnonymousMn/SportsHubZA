using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class AdminTaskRulesTests
{
    private static readonly DateTime Today = new(2026, 10, 14);

    private static AdminTask Task(string title, int? dueInDays, AdminTaskPriority priority = AdminTaskPriority.Normal, AdminTaskStatus status = AdminTaskStatus.Open) => new()
    {
        Title = title,
        Priority = priority,
        Status = status,
        DueDate = dueInDays is { } d ? Today.AddDays(d) : null
    };

    private static AdminTaskItem Item(AdminTask task) => new(task, null, "creator@example.test");

    [Fact]
    public void Today_UsesSouthAfricanTime()
    {
        // 22:30 UTC is already half past midnight the next day in South Africa.
        Assert.Equal(new DateTime(2026, 10, 15), AdminTaskRules.Today(new DateTime(2026, 10, 14, 22, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 10, 14), AdminTaskRules.Today(new DateTime(2026, 10, 14, 21, 59, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData(-3, TaskSection.Overdue, "Overdue by 3 days")]
    [InlineData(-1, TaskSection.Overdue, "Overdue by 1 day")]
    [InlineData(0, TaskSection.Today, "Due today")]
    [InlineData(1, TaskSection.ThisWeek, "Due tomorrow")]
    [InlineData(4, TaskSection.ThisWeek, "Due in 4 days")]
    [InlineData(7, TaskSection.ThisWeek, "Due in 7 days")]
    [InlineData(8, TaskSection.Later, "Due 22 Oct 2026")]
    public void Section_AndLabel_FollowTheDueDate(int dueInDays, TaskSection section, string label)
    {
        var task = Task("Check", dueInDays);
        Assert.Equal(section, AdminTaskRules.SectionOf(task, Today));
        Assert.Equal(label, AdminTaskRules.DueLabel(task.DueDate, Today));
    }

    [Fact]
    public void TaskWithoutDueDate_HasItsOwnSection()
    {
        Assert.Equal(TaskSection.NoDate, AdminTaskRules.SectionOf(Task("Someday", null), Today));
        Assert.Equal("No due date", AdminTaskRules.DueLabel(null, Today));
    }

    [Fact]
    public void DoneTask_IsNeverOverdue()
    {
        var done = Task("Finished", -5, status: AdminTaskStatus.Done);
        Assert.False(AdminTaskRules.IsOverdue(done, Today));
        Assert.Equal(TaskSection.Done, AdminTaskRules.SectionOf(done, Today));
        Assert.True(AdminTaskRules.IsOverdue(Task("Late", -5), Today));
    }

    [Fact]
    public void Group_OrdersSectionsThenUrgencyThenDueDate()
    {
        var items = new[]
        {
            Item(Task("later", 20)),
            Item(Task("normal today", 0)),
            Item(Task("urgent today", 0, AdminTaskPriority.Urgent)),
            Item(Task("overdue", -2)),
            Item(Task("no date", null)),
            Item(Task("done", -1, status: AdminTaskStatus.Done)),
        };

        var groups = AdminTaskRules.Group(items, Today);

        Assert.Equal(new[] { TaskSection.Overdue, TaskSection.Today, TaskSection.Later, TaskSection.NoDate, TaskSection.Done }, groups.Select(g => g.Section));
        Assert.Equal(new[] { "urgent today", "normal today" }, groups[1].Items.Select(i => i.Task.Title));
    }

    [Fact]
    public void Normalise_CleansTitleAndKeepsNoteLineBreaks()
    {
        var (clean, error) = AdminTaskRules.Normalise(new AdminTaskInput
        {
            Title = "  Call   the   municipality ",
            Notes = "  First line\r\nSecond line  ",
            DueDate = new DateTime(2026, 10, 20, 15, 30, 0)
        }, Today);

        Assert.Null(error);
        Assert.Equal("Call the municipality", clean!.Title);
        Assert.Equal("First line\nSecond line", clean.Notes);
        Assert.Equal(new DateTime(2026, 10, 20), clean.DueDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Normalise_RequiresATitle(string? title) =>
        Assert.Equal("Give the task a title.", AdminTaskRules.Normalise(new AdminTaskInput { Title = title }, Today).Error);

    [Fact]
    public void Normalise_RejectsOverlongTextAndAbsurdDates()
    {
        Assert.NotNull(AdminTaskRules.Normalise(new AdminTaskInput { Title = new string('a', 151) }, Today).Error);
        Assert.NotNull(AdminTaskRules.Normalise(new AdminTaskInput { Title = "ok", Notes = new string('a', 2001) }, Today).Error);
        Assert.NotNull(AdminTaskRules.Normalise(new AdminTaskInput { Title = "ok", DueDate = Today.AddYears(6) }, Today).Error);
        Assert.NotNull(AdminTaskRules.Normalise(new AdminTaskInput { Title = "ok", DueDate = Today.AddYears(-2) }, Today).Error);
        Assert.Null(AdminTaskRules.Normalise(new AdminTaskInput { Title = "ok", DueDate = Today.AddDays(-30) }, Today).Error);
    }

    [Fact]
    public void Permissions_AdministratorsAndOwnersCanChangeATask()
    {
        var task = new AdminTask { CreatedByAdminUserId = 1, AssignedToAdminUserId = 2 };

        Assert.True(AdminTaskRules.CanModify(task, actorId: 1, isAdministrator: false));  // creator
        Assert.True(AdminTaskRules.CanModify(task, actorId: 2, isAdministrator: false));  // assignee
        Assert.False(AdminTaskRules.CanModify(task, actorId: 3, isAdministrator: false)); // someone else
        Assert.True(AdminTaskRules.CanModify(task, actorId: 3, isAdministrator: true));   // administrator
    }

    [Fact]
    public void Claiming_IsOnlyForUnassignedTasksAndOnlyForYourself()
    {
        Assert.True(AdminTaskRules.CanClaim(new AdminTask(), actorId: 3, newAssigneeId: 3));
        Assert.False(AdminTaskRules.CanClaim(new AdminTask(), actorId: 3, newAssigneeId: 4));
        Assert.False(AdminTaskRules.CanClaim(new AdminTask { AssignedToAdminUserId = 2 }, actorId: 3, newAssigneeId: 3));
    }
}
