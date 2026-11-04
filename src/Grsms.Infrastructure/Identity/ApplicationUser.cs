using Microsoft.AspNetCore.Identity;

namespace Grsms.Infrastructure.Identity;

/// <summary>
/// A person who can sign in: either GREL staff (created by the HR Administrator or via Microsoft 365)
/// or an external candidate (who registered on the public portal).
///
/// ASP.NET Core Identity already provides email, password hash, phone, lockout, etc.
/// (see <see cref="IdentityUser"/>); this class adds the GREL-specific fields.
/// Stored in the "Users" table.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Name shown across the system, e.g. "Ama Mensah".</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>GREL staff number. Empty for candidates. Unique when set.</summary>
    public string? StaffNumber { get; set; }

    /// <summary>Home department of a staff member (used to route HOD work). Null for candidates.</summary>
    public int? DepartmentId { get; set; }

    /// <summary>Where the staff member is based. Null for candidates.</summary>
    public int? SiteId { get; set; }

    /// <summary>True for external candidates who self-registered; false for GREL staff.</summary>
    public bool IsCandidate { get; set; }

    /// <summary>
    /// False when the HR Administrator has disabled the account. Disabled users cannot sign in,
    /// but their history (approvals, audit rows) is kept.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True after an administrator sets a temporary password. The user is sent to the
    /// "Change password" page and cannot use the system until they choose their own.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>When the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the user last signed in successfully (UTC).</summary>
    public DateTime? LastSignInAt { get; set; }
}
