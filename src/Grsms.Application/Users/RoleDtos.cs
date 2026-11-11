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

/// <summary>
/// One line of the Roles &amp; permissions change history, in plain words, e.g. "gave Open reports to Head of Department".
/// Built from the audit trail (record types RoleClaim, Role and UserRole).
/// </summary>
/// <param name="Kind">"permission", "role" or "assignment" (for the icon and the filter).</param>
/// <param name="Granted">True for something given or added, false for something taken away or deleted, null for a change.</param>
/// <param name="AuditId">The audit-trail row, to open it there.</param>
public record RoleHistoryEntryDto(long AuditId, DateTime Timestamp, string By, string? IpAddress, string Kind, bool? Granted,
    string RoleName, string Text);

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
