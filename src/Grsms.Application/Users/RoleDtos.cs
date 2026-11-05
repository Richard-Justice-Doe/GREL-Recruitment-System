using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;

namespace Grsms.Application.Users;

/// <summary>One role on the Roles &amp; permissions screen (Phase 14).</summary>
public record RoleSummaryDto(
    string Id,
    string Name,
    string Description,
    bool IsBuiltIn,
    int ActiveUsers,
    IReadOnlySet<string> Permissions);

/// <summary>Add or edit a custom role (built-in roles have fixed names).</summary>
public class RoleFormDto
{
    /// <summary>Null when adding.</summary>
    public string? Id { get; set; }

    [Required(ErrorMessage = "Enter the role's name.")]
    [StringLength(60, MinimumLength = 3, ErrorMessage = "The name must be 3 to 60 characters.")]
    [RegularExpression(@"^[A-Za-z][A-Za-z0-9 &'\-]*$", ErrorMessage = "Use letters, numbers, spaces, ' & and - only.")]
    [Display(Name = "Role name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "What is this role for?")]
    public string? Description { get; set; }
}

/// <summary>
/// Phase 14: role and permission management. Every method refuses unless the signed-in user is a Super Admin, and every
/// change is written to the audit trail.
/// </summary>
public interface IRoleAdminService
{
    /// <summary>Every role (built-in first, then custom) with its permissions and number of active users.</summary>
    Task<IReadOnlyList<RoleSummaryDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Names of the custom roles (for the user form).</summary>
    Task<IReadOnlyList<string>> GetCustomRoleNamesAsync(CancellationToken ct = default);

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
