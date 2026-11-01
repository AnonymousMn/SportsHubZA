using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using SportsClubHub.Domain.Common;
using SportsClubHub.Web.Components.Pages.Admin;
using SportsClubHub.Web.Components.Shared;

namespace SportsClubHub.Tests;

/// <summary>Role boundaries for the whole administration area, checked automatically so a new page cannot ship unprotected.</summary>
public class AdminAuthorizationTests
{
    private static List<Type> AdminPages() =>
        typeof(AdminNav).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<RouteAttribute>().Any(r => r.Template.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)))
            .ToList();

    [Fact]
    public void EveryAdminPage_RequiresAnAdministratorOrStaffRole()
    {
        var pages = AdminPages();
        Assert.NotEmpty(pages);

        foreach (var page in pages)
        {
            var roles = page.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).ToList();
            Assert.True(roles.Count > 0, $"{page.Name} has no [Authorize] attribute.");
            Assert.All(roles, r => Assert.False(string.IsNullOrWhiteSpace(r), $"{page.Name} authorises any signed-in user."));
        }
    }

    [Fact]
    public void StaffAccounts_AreAdministratorOnly()
    {
        var roles = typeof(AdminAccounts).GetCustomAttribute<AuthorizeAttribute>()?.Roles;
        Assert.Equal(AppRoles.Administrator, roles);
    }
}
