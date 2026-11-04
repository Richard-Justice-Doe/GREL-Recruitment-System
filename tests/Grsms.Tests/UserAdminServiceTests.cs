using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Domain.Auditing;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>The account-management safety rules in UserAdminService.</summary>
public class UserAdminServiceTests : IDisposable
{
    private const string Password = "Temp#Pass2026";
    private readonly TestDatabase _db = new();
    public void Dispose() => _db.Dispose();

    private async Task<string> CreateAsync(string email, params string[] roles)
    {
        using var scope = _db.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUserAdminService>().CreateAsync(new UserFormDto
        {
            FullName = email.Split('@')[0],
            Email = email,
            DepartmentId = 1,
            SelectedRoles = [.. roles],
            TemporaryPassword = Password
        });
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
        using var scope = _db.NewScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUserAdminService>().CreateAsync(new UserFormDto
        {
            FullName = "X", Email = "x@grel.test", SelectedRoles = [Roles.Candidate], TemporaryPassword = Password
        });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task The_last_active_hr_administrator_cannot_be_deactivated()
    {
        var admin = await CreateAsync("admin@grel.test", Roles.HrAdministrator);
        var other = await CreateAsync("other@grel.test", Roles.Employee);

        using var scope = _db.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        // "other" is not an HR admin, but tries to deactivate the only one.
        var result = await service.SetActiveAsync(admin, false, actingUserId: other);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("last active HR Administrator"));

        // Nobody can deactivate themselves.
        Assert.False((await service.SetActiveAsync(other, false, actingUserId: other)).Succeeded);
    }

    [Fact]
    public async Task A_hod_assigned_to_a_department_keeps_the_hod_role_until_replaced()
    {
        var hod = await CreateAsync("hod@grel.test", Roles.HeadOfDepartment, Roles.Employee);
        var admin = await CreateAsync("admin@grel.test", Roles.HrAdministrator);

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
