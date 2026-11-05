using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Users;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Identity;

/// <summary>
/// Staff account management for the Super Admin and the HR Administrator.
///
/// Built on ASP.NET Core Identity's <see cref="UserManager{TUser}"/>, which handles password hashing,
/// password rules, unique emails and lockout. Every change is written to the audit trail automatically.
///
/// Safety rules enforced here (so they hold no matter which screen calls this service):
///   * The Candidate role cannot be given to staff; candidates register themselves.
///   * Phase 14: only a Super Admin may give or remove any role other than Employee and Interview Panel Member
///     (HR Managers, Recruitment Officers, HODs, HR Administrators, the MD, Super Admins and custom roles), and only a
///     Super Admin may edit, reset the password of, or deactivate an account that holds one. Anyone else could
///     otherwise raise their own access, or take over a privileged account by resetting its password.
///   * You cannot deactivate yourself or remove your own Super Admin role.
///   * There must always be at least one active Super Admin.
///   * A user who is set as some department's HOD keeps the HOD role (and stays active)
///     until another HOD is chosen for that department.
/// </summary>
public class UserAdminService(UserManager<ApplicationUser> userManager, ApplicationDbContext db, ICurrentUserService me) : IUserAdminService
{
    private const string SuperAdminNeeded =
        "Only a Super Admin can give or change the {0} role. HR Administrators can give Employee and Interview Panel Member only.";

    private bool IsSuperAdmin => me.IsInRole(Roles.SuperAdmin);

    /// <summary>The acting user's administrative roles (all that <see cref="Roles.CanManageAccount"/> needs to know).</summary>
    private IEnumerable<string> MyRoles => new[] { Roles.SuperAdmin, Roles.Admin, Roles.HrAdministrator }.Where(me.IsInRole);

    /// <summary>Who may manage an account with these roles, for messages.</summary>
    private static string ManagerOf(IEnumerable<string> roles) =>
        roles.Any(r => r is Roles.SuperAdmin or Roles.Admin) ? "a Super Admin" : "an Admin or Super Admin";

    /// <summary>Every role a staff account may be given: the built-in staff roles, then custom roles.</summary>
    public async Task<IReadOnlyList<string>> GetAssignableRolesAsync(CancellationToken ct = default)
    {
        var custom = (await db.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync(ct)).Where(n => !Roles.IsBuiltIn(n)).OrderBy(n => n);
        return [.. Roles.Staff, .. custom];
    }
    public async Task<IReadOnlyList<UserListItemDto>> GetAllAsync(bool includeCandidates, CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => includeCandidates || !u.IsCandidate)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        // Load all role assignments and lookup names in a few simple queries instead of one per user.
        var roleRows = await (from ur in db.UserRoles
                              join r in db.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, r.Name }).ToListAsync(ct);
        var rolesByUser = roleRows.ToLookup(x => x.UserId, x => x.Name!);
        var deptNames = await db.Departments.ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var siteNames = await db.Sites.ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var now = DateTimeOffset.UtcNow;

        return users.Select(u => new UserListItemDto(
                u.Id,
                u.FullName,
                u.Email ?? "",
                u.StaffNumber,
                u.DepartmentId is int d ? deptNames.GetValueOrDefault(d) : null,
                u.SiteId is int s ? siteNames.GetValueOrDefault(s) : null,
                rolesByUser[u.Id].OrderBy(r => r).ToList(),
                u.IsActive,
                u.LockoutEnd > now,
                u.IsCandidate,
                u.LastSignInAt))
            .ToList();
    }

    public async Task<UserFormDto?> GetForEditAsync(string id, CancellationToken ct = default)
    {
        var u = await userManager.FindByIdAsync(id);
        if (u is null || u.IsCandidate) return null; // candidate accounts are not edited on the staff form

        return new UserFormDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? "",
            PhoneNumber = u.PhoneNumber,
            StaffNumber = u.StaffNumber,
            DepartmentId = u.DepartmentId,
            SiteId = u.SiteId,
            SelectedRoles = (await userManager.GetRolesAsync(u)).ToList(),
            IsActive = u.IsActive
        };
    }

    public async Task<Result<string>> CreateAsync(UserFormDto form, CancellationToken ct = default)
    {
        Normalize(form);
        var errors = await ValidateCommonAsync(form, ct);
        errors.AddRange(CheckRoleChangesAllowed(form.SelectedRoles));
        if (string.IsNullOrEmpty(form.TemporaryPassword))
            errors.Add("Enter a temporary password for the new account.");
        if (errors.Count > 0) return Result.Failure<string>([.. errors]);

        var user = new ApplicationUser
        {
            UserName = form.Email,
            Email = form.Email,
            EmailConfirmed = true,          // HR created it, so the address is trusted
            MustChangePassword = true,      // the user picks their own password at first sign-in
            CreatedAt = DateTime.UtcNow
        };
        Apply(form, user);

        // One transaction: if adding roles fails, the half-created user is rolled back too.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var created = await userManager.CreateAsync(user, form.TemporaryPassword!);
        if (!created.Succeeded) return Result.Failure<string>(Describe(created));

        var roles = await userManager.AddToRolesAsync(user, form.SelectedRoles);
        if (!roles.Succeeded) return Result.Failure<string>(Describe(roles));

        await tx.CommitAsync(ct);
        return Result.Success(user.Id);
    }

    public async Task<Result<string>> CreateCandidateAsync(string fullName, string email, CancellationToken ct = default)
    {
        fullName = (fullName ?? "").Trim();
        email = (email ?? "").Trim();
        if (fullName.Length is < 2 or > 150) return Result.Failure<string>("Enter the person's full name (2 to 150 characters).");
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _) || email.Length > 256) return Result.Failure<string>("Enter a valid email address.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,   // created by HR for a known person; the welcome email proves the address works
            FullName = fullName,
            IsCandidate = true,
            CreatedAt = DateTime.UtcNow
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var created = await userManager.CreateAsync(user); // no password: they choose one through the emailed link
        if (!created.Succeeded) return Result.Failure<string>(Describe(created));
        var role = await userManager.AddToRoleAsync(user, Roles.Candidate);
        if (!role.Succeeded) return Result.Failure<string>(Describe(role));
        await tx.CommitAsync(ct);
        return Result.Success(user.Id);
    }

    public async Task<Result> UpdateAsync(UserFormDto form, string actingUserId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(form.Id ?? "");
        if (user is null || user.IsCandidate) return Result.Failure("User not found.");

        Normalize(form);
        var currentRoles = await userManager.GetRolesAsync(user);
        if (!Roles.CanManageAccount(MyRoles, currentRoles))
            return Result.Failure($"{user.FullName} holds a role you cannot manage ({string.Join(", ", currentRoles.Where(r => Roles.NeedsSuperAdmin([r])))}), so {ManagerOf(currentRoles)} must change this account.");

        // Roles the editor may not touch are shown locked on the form, so the browser does not send them: keep them.
        // (A privileged role being ADDED still arrives in the form and is refused below.)
        if (!IsSuperAdmin)
            form.SelectedRoles = [.. form.SelectedRoles.Union(currentRoles.Where(r => Roles.NeedsSuperAdmin([r])))];
        var errors = await ValidateCommonAsync(form, ct);
        var removing = currentRoles.Except(form.SelectedRoles).ToList();
        var adding = form.SelectedRoles.Except(currentRoles).ToList();
        errors.AddRange(CheckRoleChangesAllowed([.. adding, .. removing]));
        errors.AddRange(await CheckSafetyRulesAsync(user, actingUserId, form.IsActive, removing, ct));
        if (errors.Count > 0) return Result.Failure([.. errors]);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var wasActive = user.IsActive;
        Apply(form, user);
        user.UserName = form.Email; // the email is also the sign-in name
        user.Email = form.Email;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return Result.Failure(Describe(updated));

        if (removing.Count > 0)
        {
            var r = await userManager.RemoveFromRolesAsync(user, removing);
            if (!r.Succeeded) return Result.Failure(Describe(r));
        }
        if (adding.Count > 0)
        {
            var r = await userManager.AddToRolesAsync(user, adding);
            if (!r.Succeeded) return Result.Failure(Describe(r));
        }

        // Optional password reset by the administrator.
        if (!string.IsNullOrEmpty(form.TemporaryPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, form.TemporaryPassword);
            if (!reset.Succeeded) return Result.Failure(Describe(reset));
            user.MustChangePassword = true;
            await userManager.UpdateAsync(user);
        }

        // Changing the security stamp signs the user out everywhere within a few minutes, so a
        // deactivated user or one who lost a role cannot keep using an old session.
        if ((wasActive && !user.IsActive) || removing.Count > 0)
            await userManager.UpdateSecurityStampAsync(user);

        await tx.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(string id, bool isActive, string actingUserId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Result.Failure("User not found.");
        var targetRoles = await userManager.GetRolesAsync(user);
        if (!Roles.CanManageAccount(MyRoles, targetRoles))
            return Result.Failure($"{user.FullName} holds a role you cannot manage, so {ManagerOf(targetRoles)} must activate or deactivate this account.");

        var errors = await CheckSafetyRulesAsync(user, actingUserId, isActive, [], ct);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return Result.Failure(Describe(result));
        if (!isActive) await userManager.UpdateSecurityStampAsync(user);
        return Result.Success();
    }

    public async Task<Result> UnlockAsync(string id, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Result.Failure("User not found.");

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    private static void Normalize(UserFormDto form)
    {
        form.FullName = form.FullName.Trim();
        form.Email = form.Email.Trim();
        form.StaffNumber = string.IsNullOrWhiteSpace(form.StaffNumber) ? null : form.StaffNumber.Trim().ToUpperInvariant();
        form.PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber.Trim();
        form.SelectedRoles = form.SelectedRoles.Distinct().ToList();
    }

    /// <summary>Checks that apply to both create and edit.</summary>
    private async Task<List<string>> ValidateCommonAsync(UserFormDto form, CancellationToken ct)
    {
        var errors = new List<string>();

        if (form.SelectedRoles.Count == 0)
            errors.Add("Tick at least one role.");
        var assignable = await GetAssignableRolesAsync(ct);
        if (form.SelectedRoles.Any(r => !assignable.Contains(r)))
            errors.Add("Staff accounts can only be given staff roles. Candidates register themselves on the portal.");

        if (form.StaffNumber is not null &&
            await db.Users.AnyAsync(u => u.StaffNumber == form.StaffNumber && u.Id != form.Id, ct))
            errors.Add($"Staff number {form.StaffNumber} is already used by another account.");

        if (form.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct))
            errors.Add("The selected department does not exist.");
        if (form.SiteId is int s && !await db.Sites.AnyAsync(x => x.Id == s, ct))
            errors.Add("The selected site does not exist.");

        // A HOD must belong to a department, otherwise their requests cannot be routed.
        if (form.SelectedRoles.Contains(Roles.HeadOfDepartment) && form.DepartmentId is null)
            errors.Add("Choose a department for a Head of Department.");

        return errors;
    }

    /// <summary>The rules that stop an administrator locking everyone out (see class summary).</summary>
    private async Task<List<string>> CheckSafetyRulesAsync(
        ApplicationUser user, string actingUserId, bool willBeActive, IReadOnlyCollection<string> removingRoles, CancellationToken ct)
    {
        var errors = new List<string>();
        var deactivating = user.IsActive && !willBeActive;
        var isSelf = user.Id == actingUserId;

        if (isSelf && deactivating)
            errors.Add("You cannot deactivate your own account.");
        if (isSelf && removingRoles.Contains(Roles.SuperAdmin))
            errors.Add("You cannot remove your own Super Admin role. Ask another Super Admin.");

        if ((deactivating || removingRoles.Contains(Roles.SuperAdmin)) &&
            await userManager.IsInRoleAsync(user, Roles.SuperAdmin))
        {
            var otherActiveAdmins = (await userManager.GetUsersInRoleAsync(Roles.SuperAdmin))
                .Count(u => u.Id != user.Id && u.IsActive);
            if (otherActiveAdmins == 0)
                errors.Add("This is the last active Super Admin. Give another user that role first.");
        }

        if (deactivating || removingRoles.Contains(Roles.HeadOfDepartment))
        {
            var hodOf = await db.Departments
                .Where(d => d.HeadOfDepartmentUserId == user.Id)
                .Select(d => d.Name)
                .ToListAsync(ct);
            if (hodOf.Count > 0)
                errors.Add($"{user.FullName} is Head of Department for {string.Join(", ", hodOf)}. Choose a new HOD for that department first.");
        }

        return errors;
    }

    /// <summary>Phase 14: a role outside Employee / Interview Panel Member may only be given or removed by a Super Admin.</summary>
    private IEnumerable<string> CheckRoleChangesAllowed(IEnumerable<string> changedRoles) =>
        IsSuperAdmin ? [] : changedRoles.Where(r => Roles.NeedsSuperAdmin([r])).Distinct().Select(r => string.Format(SuperAdminNeeded, r));

    private static void Apply(UserFormDto form, ApplicationUser user)
    {
        user.FullName = form.FullName;
        user.PhoneNumber = form.PhoneNumber;
        user.StaffNumber = form.StaffNumber;
        user.DepartmentId = form.DepartmentId;
        user.SiteId = form.SiteId;
        user.IsActive = form.IsActive;
    }

    private static string[] Describe(IdentityResult result) => result.Errors.Select(e => e.Description).ToArray();
}
