using System.Net;
using System.Text.RegularExpressions;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/32 (11 November 2026): Users &amp; roles is searched, sorted and paged on the server, and its row buttons come back
/// to the same page and search (never to another site).
/// </summary>
public class UsersListWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    [Fact]
    public async Task The_users_list_pages_searches_and_returns_to_the_same_page()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { UserName = "ul.admin@grel.test", Email = "ul.admin@grel.test", EmailConfirmed = true, FullName = "Admin", CreatedAt = DateTime.UtcNow };
            await users.CreateAsync(admin, "Test#Pass2026");
            await users.AddToRoleAsync(admin, Roles.HrAdministrator);
            for (var i = 1; i <= 60; i++)
            {
                var u = new ApplicationUser { UserName = $"ul.staff{i:00}@grel.test", Email = $"ul.staff{i:00}@grel.test", EmailConfirmed = true,
                    FullName = $"Listed Person {i:00}", StaffNumber = $"UL-{i:00}", DepartmentId = 1, CreatedAt = DateTime.UtcNow };
                await users.CreateAsync(u, "Test#Pass2026");
                await users.AddToRoleAsync(u, Roles.Employee);
            }
        }
        var client = factory.NewClient();
        await client.SignInAsync("ul.admin@grel.test", "Test#Pass2026");

        var first = await client.GetStringAsync("/Users");
        Assert.Contains("page 1 of", first);
        Assert.DoesNotContain("Listed Person 60", first); // 50 per page
        Assert.Contains("Listed Person 60", await client.GetStringAsync("/Users?page=2"));

        var found = await client.GetStringAsync("/Users?search=person%2007");
        Assert.Contains("Listed Person 07", found);
        Assert.DoesNotContain("Listed Person 08", found);
        Assert.Contains("1 staff account ", found);
        Assert.Contains("Listed Person 12", await client.GetStringAsync("/Users?search=UL-12"));

        // Deactivating from page 2 of a search comes back to page 2 of that search; another site is never followed.
        var id = Regex.Match(await client.GetStringAsync("/Users?search=UL-12"), @"/Users/SetActive/([0-9a-f-]+)").Groups[1].Value;
        var back = await client.PostFormAsync("/Users?search=UL-12", $"/Users/SetActive/{id}",
            new() { ["active"] = "false", ["candidates"] = "false", ["returnUrl"] = "/Users?search=UL-12&page=1" });
        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        Assert.Equal("/Users?search=UL-12&page=1", back.Headers.Location!.ToString());
        var away = await client.PostFormAsync("/Users?search=UL-12", $"/Users/SetActive/{id}",
            new() { ["active"] = "true", ["candidates"] = "false", ["returnUrl"] = "https://evil.example/" });
        Assert.DoesNotContain("evil", away.Headers.Location!.ToString());
    }
}
