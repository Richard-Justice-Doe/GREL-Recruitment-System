using System.ComponentModel.DataAnnotations;

namespace Grsms.Application.Users;

/// <summary>One row on the Users list.</summary>
public record UserListItemDto(
    string Id,
    string FullName,
    string Email,
    string? StaffNumber,
    string? DepartmentName,
    string? SiteName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool IsLockedOut,
    bool IsCandidate,
    DateTime? LastSignInAt);

/// <summary>The staff-account create/edit form used by the HR Administrator.</summary>
public class UserFormDto
{
    /// <summary>Null when creating; the Identity user id when editing.</summary>
    public string? Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email (used to sign in)")]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [StringLength(30)]
    [Display(Name = "Staff number")]
    public string? StaffNumber { get; set; }

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Site")]
    public int? SiteId { get; set; }

    /// <summary>The roles ticked on the form. At least one is required.</summary>
    [Display(Name = "Roles")]
    public List<string> SelectedRoles { get; set; } = [];

    [Display(Name = "Active (can sign in)")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Required when creating an account; optional when editing (fill it in only to reset the password).
    /// The user must change it the first time they sign in.
    /// </summary>
    [DataType(DataType.Password)]
    [Display(Name = "Temporary password")]
    public string? TemporaryPassword { get; set; }
}

/// <summary>docs/32 (11 November 2026): the Users &amp; roles list's search, sort and page (server side).</summary>
public class UserSearch
{
    /// <summary>True: candidate (portal) accounts; false: GREL staff.</summary>
    public bool Candidates { get; set; }

    /// <summary>Part of the name, email or staff number.</summary>
    public string? Search { get; set; }

    /// <summary>"name" (default) or "recent" (latest sign-in first).</summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
