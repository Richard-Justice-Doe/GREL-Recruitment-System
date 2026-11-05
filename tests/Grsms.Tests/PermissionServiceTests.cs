using Grsms.Application.Users;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>Phase 14: default permissions, the Super Admin rules, and custom roles.</summary>
public class PermissionServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    public void Dispose() => _db.Dispose();

    public PermissionServiceTests() => _db.CurrentUser.SignInAs("super-id", "Super Admin", Roles.SuperAdmin);

    private async Task SeedAsync()
    {
        using var scope = _db.NewScope();
        await PermissionSeeder.ApplyNewDefaultsAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<IReadOnlyList<RoleSummaryDto>> RolesAsync()
    {
        using var scope = _db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<IRoleAdminService>().GetAllAsync();
    }

    [Fact]
    public async Task Defaults_reproduce_the_old_access_and_are_applied_only_once()
    {
        await SeedAsync();
        var roles = await RolesAsync();
        Assert.Equal(Permissions.Defaults[Roles.HeadOfDepartment].ToHashSet(), roles.Single(r => r.Name == Roles.HeadOfDepartment).Permissions);
        Assert.Equal(Permissions.Catalogue.Count, roles.Single(r => r.Name == Roles.SuperAdmin).Permissions.Count);
        Assert.Empty(roles.Single(r => r.Name == Roles.Candidate).Permissions);

        // The Super Admin takes "Open reports" away from HODs ...
        var hod = roles.Single(r => r.Name == Roles.HeadOfDepartment);
        using (var scope = _db.NewScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IRoleAdminService>().SavePermissionsAsync(
                new Dictionary<string, IReadOnlyCollection<string>> { [hod.Name] = hod.Permissions.Where(p => p != Permissions.ViewReports).ToList() });
            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
            Assert.Equal((0, 1), result.Value);
        }

        // ... and the next start-up does not give it back.
        await SeedAsync();
        Assert.DoesNotContain(Permissions.ViewReports, (await RolesAsync()).Single(r => r.Name == Roles.HeadOfDepartment).Permissions);

        // The change is in the audit trail...
        using (var scope = _db.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityName == "RoleClaim" && a.Action == "Deleted" && a.UserName == "Super Admin"));
        }
        // ...and in the page's change history, in plain words, newest first, and filtered by role.
        using (var scope = _db.NewScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IRoleAdminService>();
            var history = await service.GetHistoryAsync(null);
            Assert.Equal("took \"Open reports\" away from Head of Department", history[0].Text);
            Assert.Equal(("Super Admin", false, "permission"), (history[0].By, history[0].Granted, history[0].Kind));
            Assert.Contains(history, h => h.Text == "gave \"Read the audit trail\" to HR Manager"); // the start-up defaults ("system" in the real app)
            var hodOnly = await service.GetHistoryAsync("head-of-department");
            Assert.All(hodOnly, h => Assert.Equal(Roles.HeadOfDepartment, h.RoleName));
        }
    }

    [Fact]
    public async Task A_built_in_role_added_later_gets_its_defaults_once_without_touching_earlier_choices()
    {
        await SeedAsync();
        Assert.Equal(Permissions.Defaults[Roles.Admin].ToHashSet(), (await RolesAsync()).Single(r => r.Name == Roles.Admin).Permissions);

        // Simulate an installation from before the Admin role: no Admin grants, no "role:Admin" marker,
        // and a Super Admin who had taken "Open reports" away from HODs.
        using (var scope = _db.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RoleClaims.RemoveRange(db.RoleClaims.Where(c => c.RoleId == "admin" ||
                (c.RoleId == "head-of-department" && c.ClaimValue == Permissions.ViewReports)));
            db.KnownPermissions.Remove(await db.KnownPermissions.SingleAsync(k => k.Name == "role:Admin"));
            await db.SaveChangesAsync();
        }
        await SeedAsync();

        var roles = await RolesAsync();
        Assert.Equal(Permissions.Defaults[Roles.Admin].ToHashSet(), roles.Single(r => r.Name == Roles.Admin).Permissions);
        Assert.DoesNotContain(Permissions.ViewReports, roles.Single(r => r.Name == Roles.HeadOfDepartment).Permissions);
        Assert.DoesNotContain(Permissions.ManagePermissions, roles.Single(r => r.Name == Roles.Admin).Permissions); // Super Admin only
    }

    [Fact]
    public async Task Only_a_super_admin_manages_permissions_and_super_admin_only_ones_cannot_be_given_away()
    {
        await SeedAsync();
        using var scope = _db.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<IRoleAdminService>();

        var giveAway = await service.SavePermissionsAsync(new Dictionary<string, IReadOnlyCollection<string>>
            { [Roles.HrAdministrator] = [Permissions.ManageUsers, Permissions.ManagePermissions] });
        Assert.False(giveAway.Succeeded);
        Assert.Contains(giveAway.Errors, e => e.Contains("Super Admin only"));

        var candidate = await service.SavePermissionsAsync(new Dictionary<string, IReadOnlyCollection<string>>
            { [Roles.Candidate] = [Permissions.ViewVacancies] });
        Assert.False(candidate.Succeeded);

        _db.CurrentUser.SignInAs("hradmin", "HR Admin", Roles.HrAdministrator);
        Assert.False((await service.SavePermissionsAsync(new Dictionary<string, IReadOnlyCollection<string>>
            { [Roles.Employee] = [Permissions.ViewReports] })).Succeeded);
        Assert.False((await service.CreateAsync(new RoleFormDto { Name = "Sneaky" })).Succeeded);
    }

    [Fact]
    public async Task Custom_role_is_created_given_permissions_and_cannot_be_deleted_while_held()
    {
        await SeedAsync();
        string roleId;
        using (var scope = _db.NewScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IRoleAdminService>();
            Assert.False((await service.CreateAsync(new RoleFormDto { Name = "hr manager" })).Succeeded); // a built-in name
            var created = await service.CreateAsync(new RoleFormDto { Name = "Recruitment Auditor", Description = "Reads reports and the audit trail" });
            Assert.True(created.Succeeded, string.Join(" ", created.Errors));
            roleId = created.Value!;
            Assert.Equal("custom-recruitment-auditor", roleId);
            Assert.True((await service.SavePermissionsAsync(new Dictionary<string, IReadOnlyCollection<string>>
                { ["Recruitment Auditor"] = [Permissions.ViewReports, Permissions.ViewAuditTrail] })).Succeeded);
        }

        var auditor = (await RolesAsync()).Single(r => r.Name == "Recruitment Auditor");
        Assert.False(auditor.IsBuiltIn);
        Assert.Equal("Reads reports and the audit trail", auditor.Description);
        Assert.Equal(2, auditor.Permissions.Count);

        // A Super Admin can give the custom role to a user; it then cannot be deleted.
        var userId = await _db.CreateUserAsync("auditor@grel.test", 1, Roles.Employee, "Recruitment Auditor");

        // The role's change history: added, its permissions, and who holds it.
        using (var scope = _db.NewScope())
        {
            var texts = (await scope.ServiceProvider.GetRequiredService<IRoleAdminService>().GetHistoryAsync(roleId)).Select(h => h.Text).ToList();
            Assert.Contains("added the role Recruitment Auditor", texts);
            Assert.Contains("gave \"Open reports\" to Recruitment Auditor", texts);
            Assert.Contains("gave the role Recruitment Auditor to auditor (auditor@grel.test)", texts);
            Assert.Contains("changed the description of Recruitment Auditor", texts);
        }
        using (var scope = _db.NewScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IRoleAdminService>();
            var refused = await service.DeleteAsync(roleId);
            Assert.False(refused.Succeeded);
            Assert.Contains(refused.Errors, e => e.Contains("still hold"));
            Assert.False((await service.DeleteAsync("hr-manager")).Succeeded); // built-in
        }

        // HR Administrators cannot hand out custom roles (they could carry any permission).
        _db.CurrentUser.SignInAs("hradmin", "HR Admin", Roles.HrAdministrator);
        using (var scope = _db.NewScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
            var form = new UserFormDto { FullName = "Other", Email = "other@grel.test", SelectedRoles = [Roles.Employee, "Recruitment Auditor"], TemporaryPassword = "Temp#Pass2026" };
            Assert.False((await users.CreateAsync(form)).Succeeded);
            Assert.Contains("Recruitment Auditor", await users.GetAssignableRolesAsync());
        }
        Assert.NotNull(userId);
    }
}
