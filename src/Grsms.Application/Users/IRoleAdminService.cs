using Grsms.Application.Common;

namespace Grsms.Application.Users;

/// <summary>
/// Phase 14: role and permission management. Every method refuses unless the signed-in user is a Super Admin, and every
/// change is written to the audit trail.
/// </summary>
public interface IRoleAdminService
{
    /// <summary>Every role (built-in first, then custom) with its permissions and number of active users.</summary>
    Task<IReadOnlyList<RoleSummaryDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// The change history of roles and permissions, newest first: permissions given and taken away, custom roles added,
    /// renamed and deleted, and roles given to or taken from users. Optionally for one role (its id).
    /// </summary>
    Task<IReadOnlyList<RoleHistoryEntryDto>> GetHistoryAsync(string? roleId, int take = 100, CancellationToken ct = default);

    /// <summary>Replaces the permissions of every role with <paramref name="grants"/> (role name → permission names).</summary>
    /// <returns>How many grants were added and removed.</returns>
    Task<Result<(int Added, int Removed)>> SavePermissionsAsync(IReadOnlyDictionary<string, IReadOnlyCollection<string>> grants, CancellationToken ct = default);

    Task<RoleFormDto?> GetForEditAsync(string id, CancellationToken ct = default);

    /// <summary>Adds a custom role (with no permissions yet).</summary>
    Task<Result<string>> CreateAsync(RoleFormDto form, CancellationToken ct = default);

    /// <summary>Renames a custom role or changes its description.</summary>
    Task<Result> UpdateAsync(RoleFormDto form, CancellationToken ct = default);

    /// <summary>Deletes a custom role that nobody holds.</summary>
    Task<Result> DeleteAsync(string id, CancellationToken ct = default);
}
