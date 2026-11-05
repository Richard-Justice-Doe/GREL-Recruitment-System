using System.Net;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 14 through the real pages: the seeded administrator is a Super Admin, only Super Admins open Roles &amp;
/// permissions, a permission change applies at once (no sign-out needed), and a custom role opens what it is given.
/// </summary>
public class PermissionWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<HttpClient> UserAsync(string email, params string[] roles)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(email) is null)
            {
                var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = 1, CreatedAt = DateTime.UtcNow };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                await users.AddToRolesAsync(user, roles);
            }
        }
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, Password)).StatusCode);
        return client;
    }

    private async Task<HttpClient> SuperAdminAsync()
    {
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword)).StatusCode);
        return client;
    }

    private static bool Denied(HttpResponseMessage r) =>
        r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied");

    /// <summary>Posts the permission grid with every role's current grants, changed by <paramref name="change"/>.</summary>
    private async Task SavePermissionsAsync(HttpClient superAdmin, Action<Dictionary<string, HashSet<string>>> change)
    {
        Dictionary<string, HashSet<string>> grid;
        using (var scope = factory.Services.CreateScope())
            grid = (await scope.ServiceProvider.GetRequiredService<Grsms.Application.Users.IRoleAdminService>().GetAllAsync())
                .Where(r => r.Name is not (Roles.SuperAdmin or Roles.Candidate))
                .ToDictionary(r => r.Name, r => r.Permissions.ToHashSet());
        change(grid);

        var fields = new List<KeyValuePair<string, string>>();
        foreach (var (role, perms) in grid)
        {
            fields.Add(new("roleNames", role));
            fields.AddRange(perms.Select(p => new KeyValuePair<string, string>("grant", $"{role}|{p}")));
        }
        var page = await superAdmin.GetStringAsync("/Roles");
        var token = System.Text.RegularExpressions.Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        fields.Add(new("__RequestVerificationToken", WebUtility.HtmlDecode(token)));
        var saved = await superAdmin.PostAsync("/Roles/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("Permissions saved", await superAdmin.GetStringAsync("/Roles"));
    }

    [Fact]
    public async Task Super_admin_opens_roles_and_permissions_and_hr_administrator_cannot()
    {
        var admin = await SuperAdminAsync();
        var page = await admin.GetStringAsync("/Roles");
        Assert.Contains("Roles &amp; permissions", page);
        Assert.Contains("See all departments", page);
        Assert.Contains("Roles &amp; permissions", await admin.GetStringAsync("/")); // in the menu

        var hrAdmin = await UserAsync("p.hradmin@grel.test", Roles.HrAdministrator, Roles.Employee);
        Assert.True(Denied(await hrAdmin.GetAsync("/Roles")));
        Assert.DoesNotContain("Roles &amp; permissions", await hrAdmin.GetStringAsync("/"));
        // The HR Administrator still runs Users & roles, but the privileged roles are locked there.
        var form = await hrAdmin.GetStringAsync("/Users/Create");
        Assert.Contains("can only be given by a Super Admin", form);
    }

    [Fact]
    public async Task Admin_sees_settings_read_only_and_everyone_sees_their_own_audit_trail()
    {
        var admin = await UserAsync("p.admin@grel.test", Roles.Admin, Roles.Employee);

        // Roles & permissions and ATS settings open read-only: no save button, every box locked, saving refused.
        var roles = await admin.GetStringAsync("/Roles");
        Assert.Contains("Read-only.", roles);
        Assert.DoesNotContain("Save permissions", roles);
        Assert.True(Denied(await admin.PostFormAsync("/Roles", "/Roles/Save", new() { ["roleNames"] = Roles.Employee, ["grant"] = $"{Roles.Employee}|{Permissions.ViewReports}" })));
        Assert.True(Denied(await admin.GetAsync("/Roles/Create")));
        var ats = await admin.GetStringAsync("/Ats/Settings");
        Assert.Contains("Read-only.", ats);
        Assert.Contains("<fieldset disabled=\"disabled\">", ats);
        // The Admin runs Users & roles, the audit trail and organisation data.
        foreach (var page in new[] { "/Users", "/AuditLogs", "/Departments" })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(page)).StatusCode);

        // My audit trail: the Admin's own sign-in is there, with their address; an Employee sees theirs too.
        var mine = await admin.GetStringAsync("/MyAuditTrail");
        Assert.Contains("My audit trail", mine);
        Assert.Contains("Signed in", mine);
        var employee = await UserAsync("p.employee@grel.test", Roles.Employee);
        Assert.Contains("Signed in", await employee.GetStringAsync("/MyAuditTrail"));
        Assert.True(Denied(await employee.GetAsync("/AuditLogs"))); // but not everyone else's
    }

    [Fact]
    public async Task A_permission_taken_away_applies_at_once_and_a_custom_role_opens_what_it_is_given()
    {
        var admin = await SuperAdminAsync();
        var hod = await UserAsync("p.hod@grel.test", Roles.HeadOfDepartment, Roles.Employee);
        Assert.Equal(HttpStatusCode.OK, (await hod.GetAsync("/Reports")).StatusCode);

        // Take "Open reports" away from Heads of Department: the HOD, still signed in, is refused straight away.
        await SavePermissionsAsync(admin, g => g[Roles.HeadOfDepartment].Remove(Permissions.ViewReports));
        Assert.True(Denied(await hod.GetAsync("/Reports")));

        // Give it back, so other tests sharing this site are unaffected.
        await SavePermissionsAsync(admin, g => g[Roles.HeadOfDepartment].Add(Permissions.ViewReports));
        Assert.Equal(HttpStatusCode.OK, (await hod.GetAsync("/Reports")).StatusCode);

        // A custom role holding only "Read the audit trail".
        var created = await admin.PostFormAsync("/Roles/Create", "/Roles/Create", new() { ["Name"] = "Audit Reader", ["Description"] = "Reads the audit trail" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        await SavePermissionsAsync(admin, g => g["Audit Reader"].Add(Permissions.ViewAuditTrail));

        var reader = await UserAsync("p.reader@grel.test", Roles.Employee, "Audit Reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/AuditLogs")).StatusCode);
        Assert.True(Denied(await reader.GetAsync("/Reports")));
        Assert.True(Denied(await reader.GetAsync("/Users")));

        // The permission changes are in the audit trail, and in plain words in the page's change history.
        var history = await admin.GetStringAsync("/Roles?historyRole=head-of-department");
        Assert.Contains("Change history", history);
        Assert.Contains("took &quot;Open reports&quot; away from Head of Department", history);
        Assert.Contains("gave &quot;Open reports&quot; to Head of Department", history);
        var trail = await admin.GetStringAsync("/AuditLogs?EntityName=RoleClaim");
        Assert.Contains("audit.view", trail);
    }
}
