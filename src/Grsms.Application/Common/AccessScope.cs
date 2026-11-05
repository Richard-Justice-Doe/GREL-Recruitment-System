using Grsms.Application.Common.Interfaces;
using Grsms.Application.Recruitment;
using Grsms.Application.Users;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;

namespace Grsms.Application.Common;

/// <summary>
/// Works out which departments' records the current user may see. Shared by every departmental module
/// (recruitment requests, vacancies, ...), so the rule is written once:
///
///   * Holders of the "See all departments" permission see every department. By default (Phase 14) that is the
///     Recruitment Officers, HR Manager, Managing Director, HR Administrator, and always the Super Admin.
///   * A Head of Department sees the departments they head: those where they are set as HOD,
///     plus their own home department.
///   * Everyone always sees records they created themselves (checked by each module).
/// </summary>
public class AccessScope(ICurrentUserService me, IRepository<Department> departments, IUserDirectory users)
{
    /// <summary>True when the current user sees every department's records (permission "data.all-departments").</summary>
    public bool SeesAllDepartments => me.HasPermission(Permissions.SeeAllDepartments);

    public string UserId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    public string UserName => me.FullName ?? me.UserName ?? "Unknown";
    public bool IsInRole(string role) => me.IsInRole(role);

    /// <summary>Departments the current user heads (empty if they are not a Head of Department).</summary>
    public async Task<HashSet<int>> HodDepartmentIdsAsync(CancellationToken ct = default)
    {
        if (!me.IsInRole(Roles.HeadOfDepartment)) return [];

        var ids = (await departments.ListAsync(d => d.HeadOfDepartmentUserId == UserId, ct)).Select(d => d.Id).ToHashSet();
        if (await users.GetDepartmentIdAsync(UserId, ct) is int home) ids.Add(home);
        return ids;
    }

    /// <summary>The current user's visibility, for queries.</summary>
    public async Task<RequestVisibility> VisibilityAsync(CancellationToken ct = default) =>
        new(SeesAllDepartments, UserId, await HodDepartmentIdsAsync(ct));

    /// <summary>True if a record in <paramref name="departmentId"/> created by <paramref name="ownerUserId"/> is visible.</summary>
    public static bool CanSee(RequestVisibility v, int departmentId, string? ownerUserId) =>
        v.SeeAll || ownerUserId == v.UserId || v.HodDepartmentIds.Contains(departmentId);
}
