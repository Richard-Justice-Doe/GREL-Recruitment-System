using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Domain.Auditing;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>The account-management safety rules in UserAdminService (Phase 14: Super Admin and privileged roles).</summary>
public class UserAdminServiceTests : IDisposable
{
    private const string Password = "Temp#Pass2026";
    private readonly TestDatabase _db = new();
    public void Dispose() => _db.Dispose();

    // Most tests act as a Super Admin, who may give any staff role.
    public UserAdminServiceTests() => _db.CurrentUser.SignInAs("super-id", "Super Admin", Roles.SuperAdmin);

    private async Task<Grsms.Application.Common.Result<string>> TryCreateAsync(string email, params string[] roles)
    {
        using var scope = _db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<IUserAdminService>().CreateAsync(new UserFormDto
        {
            FullName = email.Split('@')[0],
            Email = email,
            DepartmentId = 1,
            SelectedRoles = [.. roles],
            TemporaryPassword = Password
        });
    }

    private async Task<string> CreateAsync(string email, params string[] roles)
    {
        var result = await TryCreateAsync(email, roles);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Value!;
    }

    [Fact]
    public async Task New_staff_account_gets_roles_must_change_password_and_audit_rows()
    {
        var id = await CreateAsync("ama@grel.test", Roles.RecruitmentOfficer, Roles.Employee);

        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == id);
        Assert.True(user.MustChangePassword);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(2, await db.UserRoles.CountAsync(r => r.UserId == id));

        // The user row and each role assignment are audited; the password hash never is.
        var audits = await db.AuditLogs.Where(a => a.EntityName == "User" || a.EntityName == "UserRole").ToListAsync();
        Assert.Contains(audits, a => a.EntityName == "User" && a.Action == AuditActions.Created);
        Assert.Equal(2, audits.Count(a => a.EntityName == "UserRole" && a.Action == AuditActions.Created));
        Assert.DoesNotContain(audits, a => (a.NewValues ?? "").Contains("PasswordHash"));
    }

    [Fact]
    public async Task Staff_cannot_be_given_the_candidate_role()
    {
        Assert.False((await TryCreateAsync("x@grel.test", Roles.Candidate)).Succeeded);
    }

    [Fact]
    public async Task The_last_active_super_admin_cannot_be_deactivated_or_lose_the_role()
    {
        var admin = await CreateAsync("admin@grel.test", Roles.SuperAdmin);
        var other = await CreateAsync("other@grel.test", Roles.Employee);

        using var scope = _db.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var result = await service.SetActiveAsync(admin, false, actingUserId: other);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("last active Super Admin"));

        // Nobody can deactivate themselves, or remove their own Super Admin role.
        Assert.False((await service.SetActiveAsync(other, false, actingUserId: other)).Succeeded);
        var form = (await service.GetForEditAsync(admin))!;
        form.SelectedRoles = [Roles.Employee];
        var own = await service.UpdateAsync(form, actingUserId: admin);
        Assert.Contains(own.Errors, e => e.Contains("your own Super Admin role"));
    }

    [Fact]
    public async Task Hr_administrator_gives_everyday_roles_only_and_cannot_touch_privileged_accounts()
    {
        var hrm = await CreateAsync("hrm@grel.test", Roles.HrManager, Roles.Employee);
        var hrAdmin = await CreateAsync("hradmin@grel.test", Roles.HrAdministrator, Roles.Employee);
        _db.CurrentUser.SignInAs(hrAdmin, "HR Admin", Roles.HrAdministrator, Roles.Employee);

        // Everyday accounts: fine.
        var employee = await CreateAsync("plain@grel.test", Roles.Employee, Roles.PanelMember);

        // Privileged roles: refused, whether creating or adding to an existing account.
        foreach (var role in new[] { Roles.HrManager, Roles.RecruitmentOfficer, Roles.HeadOfDepartment, Roles.HrAdministrator, Roles.ManagingDirector, Roles.SuperAdmin })
        {
            var refused = await TryCreateAsync($"try-{role.Replace(' ', '-')}@grel.test", Roles.Employee, role);
            Assert.False(refused.Succeeded);
            Assert.Contains(refused.Errors, e => e.Contains("Only a Super Admin"));
        }

        using var scope = _db.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var promote = (await service.GetForEditAsync(employee))!;
        promote.SelectedRoles = [Roles.Employee, Roles.RecruitmentOfficer];
        Assert.False((await service.UpdateAsync(promote, hrAdmin)).Succeeded);

        // A privileged account cannot be edited (e.g. a password reset to take it over) or deactivated.
        var takeover = (await service.GetForEditAsync(hrm))!;
        takeover.TemporaryPassword = "Taken#Over2026";
        var edit = await service.UpdateAsync(takeover, hrAdmin);
        Assert.False(edit.Succeeded);
        Assert.Contains(edit.Errors, e => e.Contains("an Admin or Super Admin must change this account"));
        Assert.False((await service.SetActiveAsync(hrm, false, hrAdmin)).Succeeded);

        // Unlocking stays allowed: it gives no new access.
        Assert.True((await service.UnlockAsync(hrm)).Succeeded);
    }

    [Fact]
    public async Task Admin_manages_every_account_except_admins_and_super_admins_but_gives_no_privileged_role()
    {
        var hod = await CreateAsync("hod@grel.test", Roles.HeadOfDepartment, Roles.Employee);
        var hrm = await CreateAsync("hrm@grel.test", Roles.HrManager, Roles.Employee);
        var otherAdmin = await CreateAsync("admin2@grel.test", Roles.Admin, Roles.Employee);
        var super = await CreateAsync("super@grel.test", Roles.SuperAdmin);
        var admin = await CreateAsync("admin@grel.test", Roles.Admin, Roles.Employee);
        _db.CurrentUser.SignInAs(admin, "Admin", Roles.Admin, Roles.Employee);

        using var scope = _db.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        // A HOD's account: details and a password reset are fine, and the HOD role stays (the form does not send locked roles).
        var form = (await service.GetForEditAsync(hod))!;
        form.FullName = "Kwesi Appiah";
        form.TemporaryPassword = "Reset#Pass2026";
        form.SelectedRoles = [Roles.Employee, Roles.PanelMember];
        var edited = await service.UpdateAsync(form, admin);
        Assert.True(edited.Succeeded, string.Join(" ", edited.Errors));
        var hodForm = (await service.GetForEditAsync(hod))!;
        Assert.Equal("Kwesi Appiah", hodForm.FullName);
        Assert.Contains(Roles.HeadOfDepartment, hodForm.SelectedRoles);
        Assert.Contains(Roles.PanelMember, hodForm.SelectedRoles);

        // An HR Manager can be deactivated...
        Assert.True((await service.SetActiveAsync(hrm, false, admin)).Succeeded);
        // ...but no privileged role can be given, and Admin / Super Admin accounts are off limits.
        var promote = (await service.GetForEditAsync(hrm))!;
        promote.SelectedRoles = [Roles.Employee, Roles.HrManager, Roles.RecruitmentOfficer];
        Assert.Contains((await service.UpdateAsync(promote, admin)).Errors, e => e.Contains("Only a Super Admin"));
        Assert.False((await TryCreateAsync("new.admin@grel.test", Roles.Admin)).Succeeded);
        Assert.False((await service.SetActiveAsync(otherAdmin, false, admin)).Succeeded);
        var superForm = (await service.GetForEditAsync(super))!;
        superForm.TemporaryPassword = "Taken#Over2026";
        Assert.Contains((await service.UpdateAsync(superForm, admin)).Errors, e => e.Contains("a Super Admin must change this account"));
    }

    [Fact]
    public async Task My_audit_trail_shows_what_i_did_and_what_others_did_to_my_account_without_their_ip()
    {
        var ama = await CreateAsync("ama@grel.test", Roles.Employee);       // created by the Super Admin
        var kofi = await CreateAsync("kofi@grel.test", Roles.Employee);

        // The Super Admin (IP 10.0.0.1) gives Ama a role and renames her.
        using (var scope = _db.NewScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
            var form = (await service.GetForEditAsync(ama))!;
            form.FullName = "Ama Mensah";
            form.SelectedRoles = [Roles.Employee, Roles.PanelMember];
            Assert.True((await service.UpdateAsync(form, "super-id")).Succeeded);
        }
        // Ama does something herself, from her own address.
        _db.CurrentUser.SignInAs(ama, "ama@grel.test", Roles.Employee);
        _db.CurrentUser.IpAddress = "10.9.9.9";
        using (var scope = _db.NewScope())
            await scope.ServiceProvider.GetRequiredService<Grsms.Application.Auditing.IAuditLogService>().LogAsync(AuditActions.SignedIn, ama, "ama@grel.test");

        using (var scope = _db.NewScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<Grsms.Application.Auditing.IAuditLogService>();
            var mine = (await audit.SearchMineAsync(ama, "ama@grel.test", new Grsms.Application.Auditing.AuditLogQuery())).Items;

            Assert.Contains(mine, x => x.Action == AuditActions.SignedIn && x.IpAddress == "10.9.9.9");             // mine, with my IP
            Assert.Contains(mine, x => x.EntityName == "User" && x.Action == AuditActions.Updated && x.IpAddress is null); // done to me, no IP
            Assert.Contains(mine, x => x.EntityName == "UserRole" && x.EntityId!.StartsWith(ama) && x.Action == AuditActions.Created);
            Assert.DoesNotContain(mine, x => x.EntityId == kofi || (x.EntityId ?? "").StartsWith(kofi));               // not someone else's
        }
    }

    [Fact]
    public async Task A_hod_assigned_to_a_department_keeps_the_hod_role_until_replaced()
    {
        var hod = await CreateAsync("hod@grel.test", Roles.HeadOfDepartment, Roles.Employee);
        var admin = await CreateAsync("admin@grel.test", Roles.SuperAdmin);

        using (var scope = _db.NewScope())
        {
            var departments = scope.ServiceProvider.GetRequiredService<IDepartmentService>();
            var form = (await departments.GetForEditAsync(6))!; // Sales and Marketing
            form.HeadOfDepartmentUserId = hod;
            Assert.True((await departments.UpdateAsync(form)).Succeeded);
        }

        using (var scope = _db.NewScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
            var form = (await service.GetForEditAsync(hod))!;
            form.SelectedRoles = [Roles.Employee]; // try to drop the HOD role

            var result = await service.UpdateAsync(form, admin);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Contains("Sales and Marketing"));
        }
    }

    [Fact]
    public async Task Only_users_with_the_hod_role_can_be_set_as_head_of_department()
    {
        var employee = await CreateAsync("plain@grel.test", Roles.Employee);

        using var scope = _db.NewScope();
        var departments = scope.ServiceProvider.GetRequiredService<IDepartmentService>();
        var form = (await departments.GetForEditAsync(2))!;
        form.HeadOfDepartmentUserId = employee;

        Assert.False((await departments.UpdateAsync(form)).Succeeded);
    }
}
