using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SportsClubHub.Domain.Common;
using SportsClubHub.Domain.Entities;
using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class AdminAccountServiceTests
{
    [Fact]
    public async Task Create_StoresHashedActiveStaffAccount()
    {
        using var database = new TestDatabase();
        var service = CreateService(database);

        var result = await service.CreateAsync("Staff@Example.test", "a-secure-password", AppRoles.Staff);
        var accounts = await service.ListAsync();

        var account = Assert.Single(accounts);
        Assert.True(result.Succeeded);
        Assert.Equal("staff@example.test", account.Email);
        Assert.Equal(AppRoles.Staff, account.Role);
        Assert.True(account.IsActive);
        Assert.NotEqual("a-secure-password", account.PasswordHash);
        Assert.NotNull(await service.ValidateAsync("staff@example.test", "a-secure-password"));
    }

    [Fact]
    public async Task InactiveAccount_CannotSignIn()
    {
        using var database = new TestDatabase();
        var service = CreateService(database);
        await service.CreateAsync("Staff@Example.test", "a-secure-password", AppRoles.Staff);
        var account = Assert.Single(await service.ListAsync());

        Assert.True((await service.SetActiveAsync(account.AdminUserId, false, 999)).Succeeded);
        Assert.Null(await service.ValidateAsync(account.Email, "a-secure-password"));
    }

    [Fact]
    public async Task LastActiveAdministrator_CannotBeDeactivated()
    {
        using var database = new TestDatabase();
        var service = CreateService(database);
        await using (var db = database.CreateDbContext())
        {
            var admin = new AdminUser
            {
                Email = "admin@example.test",
                Role = AppRoles.Administrator,
                PasswordHash = "test"
            };
            db.AdminUsers.Add(admin);
            await db.SaveChangesAsync();
        }

        var account = Assert.Single(await service.ListAsync());
        var result = await service.SetActiveAsync(account.AdminUserId, false, 999);

        Assert.False(result.Succeeded);
    }

    private static AdminAccountService CreateService(TestDatabase database)
    {
        var configuration = new ConfigurationBuilder().Build();
        return new AdminAccountService(
            database,
            configuration,
            new TestHostEnvironment(),
            NullLogger<AdminAccountService>.Instance);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
