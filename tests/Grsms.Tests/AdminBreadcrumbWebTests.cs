using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>11 November 2026: every administration screen shows "Administration › screen", with a way back to the Administration page.</summary>
public class AdminBreadcrumbWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    [Fact]
    public async Task Administration_screens_link_back_to_the_administration_page()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { UserName = "bc.admin@grel.test", Email = "bc.admin@grel.test", EmailConfirmed = true, FullName = "Admin", DepartmentId = 1, CreatedAt = DateTime.UtcNow };
            await users.CreateAsync(admin, "Test#Pass2026");
            await users.AddToRoleAsync(admin, Roles.HrAdministrator);
        }
        var client = factory.NewClient();
        await client.SignInAsync("bc.admin@grel.test", "Test#Pass2026");

        foreach (var page in new[] { "/Divisions", "/Departments", "/Sites", "/Users", "/Grades", "/AuditLogs" })
        {
            var html = await client.GetStringAsync(page);
            Assert.Contains("aria-label=\"Breadcrumb\"", html);
            Assert.Contains("href=\"/Administration\"", html);
        }
        var divisions = await client.GetStringAsync("/Divisions");
        Assert.Contains("<li class=\"breadcrumb-item active\" aria-current=\"page\">Divisions</li>", divisions);

        // A page inside a screen links back to that screen's list too.
        var add = await client.GetStringAsync("/Sites/Create");
        Assert.Contains("<a href=\"/Sites\">Sites</a>", add);

        // Not on other pages, nor on the Administration page itself.
        Assert.DoesNotContain("aria-label=\"Breadcrumb\"", await client.GetStringAsync("/Administration"));
        Assert.DoesNotContain("aria-label=\"Breadcrumb\"", await client.GetStringAsync("/"));
    }
}
