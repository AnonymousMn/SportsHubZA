using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class AdminTaskServiceTests
{
    private sealed class RecordingTransport : IEmailTransport
    {
        public List<(string To, string Subject)> Sent { get; } = new();

        public Task SendAsync(EmailSettings settings, MailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add((message.To[0].Address, message.Subject));
            return Task.CompletedTask;
        }
    }

    private static async Task<AdminUser> AddAdminAsync(TestDatabase database, string email, string role = AppRoles.Staff, bool active = true)
    {
        await using var db = database.CreateDbContext();
        var user = new AdminUser { Email = email, PasswordHash = "not-a-real-hash", Role = role, IsActive = active };
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static (AdminTaskService Service, RecordingTransport Mail) Create(TestDatabase database)
    {
        var transport = new RecordingTransport();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Enabled"] = "true",
            ["Email:Host"] = "smtp.example.test",
            ["Email:Username"] = "sender@example.test",
            ["Email:Password"] = "secret"
        }).Build();
        var email = new EmailService(configuration, NullLogger<EmailService>.Instance, transport);
        return (new AdminTaskService(database, email), transport);
    }

    private static AdminTaskInput Input(string title, int? assignee = null) => new() { Title = title, AssignedToAdminUserId = assignee };

    [Fact]
    public async Task Create_AssignedToSomeoneElse_SavesAndEmailsThemOnce()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var helper = await AddAdminAsync(database, "helper@example.test");
        var (service, mail) = Create(database);

        var result = await service.CreateAsync(Input("Verify Netball Club", helper.AdminUserId), boss.AdminUserId);

        Assert.True(result.Succeeded);
        var item = Assert.Single(await service.ListAsync(AdminTaskView.Mine, helper.AdminUserId));
        Assert.Equal("Verify Netball Club", item.Task.Title);
        Assert.Equal("boss@example.test", item.CreatorEmail);
        var sent = Assert.Single(mail.Sent);
        Assert.Equal("helper@example.test", sent.To);
        Assert.Contains("Verify Netball Club", sent.Subject);
    }

    [Fact]
    public async Task Create_AssignedToYourself_SendsNoEmail()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var (service, mail) = Create(database);

        Assert.True((await service.CreateAsync(Input("Own task", boss.AdminUserId), boss.AdminUserId)).Succeeded);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task Create_RejectsInactiveOrUnknownAssigneesAndBadInput()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var gone = await AddAdminAsync(database, "gone@example.test", active: false);
        var (service, _) = Create(database);

        Assert.False((await service.CreateAsync(Input("Task", gone.AdminUserId), boss.AdminUserId)).Succeeded);
        Assert.False((await service.CreateAsync(Input("Task", 9999), boss.AdminUserId)).Succeeded);
        Assert.False((await service.CreateAsync(Input("   "), boss.AdminUserId)).Succeeded);
        Assert.False((await service.CreateAsync(Input("Task"), 9999)).Succeeded);
        Assert.Empty(await service.ListAsync(AdminTaskView.TeamOpen, boss.AdminUserId));
    }

    [Fact]
    public async Task Staff_CannotChangeTasksThatAreNotTheirs_ButAdministratorsCan()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var owner = await AddAdminAsync(database, "owner@example.test");
        var outsider = await AddAdminAsync(database, "outsider@example.test");
        var (service, _) = Create(database);
        await service.CreateAsync(Input("Owned task", owner.AdminUserId), boss.AdminUserId);
        var taskId = (await service.ListAsync(AdminTaskView.Mine, owner.AdminUserId)).Single().Task.AdminTaskId;

        Assert.False((await service.SetStatusAsync(taskId, AdminTaskStatus.Done, outsider.AdminUserId, false)).Succeeded);
        Assert.False((await service.UpdateAsync(taskId, Input("Hijacked"), outsider.AdminUserId, false)).Succeeded);
        Assert.False((await service.AssignAsync(taskId, outsider.AdminUserId, outsider.AdminUserId, false)).Succeeded);
        Assert.False((await service.DeleteAsync(taskId, outsider.AdminUserId, false)).Succeeded);

        Assert.True((await service.SetStatusAsync(taskId, AdminTaskStatus.InProgress, owner.AdminUserId, false)).Succeeded);
        Assert.True((await service.SetStatusAsync(taskId, AdminTaskStatus.Done, boss.AdminUserId, true)).Succeeded);
    }

    [Fact]
    public async Task AnyoneCanClaimAnUnassignedTask_ButNotHandItToSomeoneElse()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var helper = await AddAdminAsync(database, "helper@example.test");
        var other = await AddAdminAsync(database, "other@example.test");
        var (service, _) = Create(database);
        await service.CreateAsync(Input("Nobody's task"), boss.AdminUserId);
        var taskId = (await service.ListAsync(AdminTaskView.Unassigned, helper.AdminUserId)).Single().Task.AdminTaskId;

        Assert.False((await service.AssignAsync(taskId, other.AdminUserId, helper.AdminUserId, false)).Succeeded);
        Assert.True((await service.AssignAsync(taskId, helper.AdminUserId, helper.AdminUserId, false)).Succeeded);
        Assert.Single(await service.ListAsync(AdminTaskView.Mine, helper.AdminUserId));
        Assert.Empty(await service.ListAsync(AdminTaskView.Unassigned, helper.AdminUserId));
    }

    [Fact]
    public async Task Completing_RecordsWhenAndReopeningClearsIt()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var (service, _) = Create(database);
        await service.CreateAsync(Input("Finish me", boss.AdminUserId), boss.AdminUserId);
        var taskId = (await service.ListAsync(AdminTaskView.Mine, boss.AdminUserId)).Single().Task.AdminTaskId;

        await service.SetStatusAsync(taskId, AdminTaskStatus.Done, boss.AdminUserId, true);
        var done = Assert.Single(await service.ListAsync(AdminTaskView.Done, boss.AdminUserId));
        Assert.NotNull(done.Task.CompletedAt);
        Assert.Empty(await service.ListAsync(AdminTaskView.Mine, boss.AdminUserId));

        await service.SetStatusAsync(taskId, AdminTaskStatus.Open, boss.AdminUserId, true);
        Assert.Null(Assert.Single(await service.ListAsync(AdminTaskView.Mine, boss.AdminUserId)).Task.CompletedAt);
    }

    [Fact]
    public async Task Delete_IsForAdministratorsAndCreatorsOnly()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var creator = await AddAdminAsync(database, "creator@example.test");
        var assignee = await AddAdminAsync(database, "assignee@example.test");
        var (service, _) = Create(database);
        await service.CreateAsync(Input("Task A", assignee.AdminUserId), creator.AdminUserId);
        await service.CreateAsync(Input("Task B", assignee.AdminUserId), creator.AdminUserId);
        var ids = (await service.ListAsync(AdminTaskView.TeamOpen, boss.AdminUserId)).Select(i => i.Task.AdminTaskId).ToList();

        Assert.False((await service.DeleteAsync(ids[0], assignee.AdminUserId, false)).Succeeded); // assigned, not creator
        Assert.True((await service.DeleteAsync(ids[0], creator.AdminUserId, false)).Succeeded);
        Assert.True((await service.DeleteAsync(ids[1], boss.AdminUserId, true)).Succeeded);
        Assert.Empty(await service.ListAsync(AdminTaskView.TeamOpen, boss.AdminUserId));
    }

    [Fact]
    public async Task Counts_SeparateMineTeamOverdueAndUnassigned()
    {
        using var database = new TestDatabase();
        var boss = await AddAdminAsync(database, "boss@example.test", AppRoles.Administrator);
        var helper = await AddAdminAsync(database, "helper@example.test");
        var (service, _) = Create(database);
        var now = DateTime.UtcNow;
        await service.CreateAsync(new AdminTaskInput { Title = "Mine overdue", AssignedToAdminUserId = boss.AdminUserId, DueDate = now.AddDays(-3) }, boss.AdminUserId);
        await service.CreateAsync(new AdminTaskInput { Title = "Mine later", AssignedToAdminUserId = boss.AdminUserId, DueDate = now.AddDays(10) }, boss.AdminUserId);
        await service.CreateAsync(new AdminTaskInput { Title = "Theirs overdue", AssignedToAdminUserId = helper.AdminUserId, DueDate = now.AddDays(-1) }, boss.AdminUserId);
        await service.CreateAsync(Input("Nobody's"), boss.AdminUserId);

        var counts = await service.GetCountsAsync(boss.AdminUserId, now);

        Assert.Equal(2, counts.MyOpen);
        Assert.Equal(1, counts.MyOverdue);
        Assert.Equal(4, counts.OpenTotal);
        Assert.Equal(2, counts.TeamOverdue);
        Assert.Equal(1, counts.Unassigned);
    }
}
