using System.ComponentModel.DataAnnotations;

namespace Grsms.Application.Organization;

// DTOs ("data transfer objects") are the shapes of data passed between screens and services.
// They are kept separate from the database entities so a screen can never accidentally change
// fields it should not (for example CreatedBy or IsDeleted).
//
//   *ListItemDto - one row in a list screen (read-only).
//   *FormDto     - what the user types into a create/edit form. The [Required]/[StringLength]
//                  attributes are checked in the browser AND again on the server.

// ---------------------------------------------------------------- Divisions

/// <summary>One row on the Divisions list.</summary>
public record DivisionListItemDto(
    int Id,
    string Name,
    string Code,
    string? Description,
    int ActiveDepartments,
    int InactiveDepartments,
    bool IsActive);

/// <summary>The Division create/edit form.</summary>
public class DivisionFormDto
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Division name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [RegularExpression("^[A-Za-z0-9&-]+$", ErrorMessage = "Use letters, numbers, '&' or '-' only (no spaces).")]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

// ---------------------------------------------------------------- Departments

/// <summary>One row on the Departments list.</summary>
public record DepartmentListItemDto(
    int Id,
    string Name,
    string Code,
    string DivisionName,
    string? HeadOfDepartmentName,
    int UnitCount,
    bool IsActive);

/// <summary>Everything shown on the Department details page.</summary>
public record DepartmentDetailsDto(
    int Id,
    string Name,
    string Code,
    int DivisionId,
    string DivisionName,
    string? Description,
    string? HeadOfDepartmentUserId,
    string? HeadOfDepartmentName,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    IReadOnlyList<UnitListItemDto> Units);

/// <summary>The Department create/edit form.</summary>
public class DepartmentFormDto
{
    /// <summary>0 when creating a new department; the record id when editing.</summary>
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Department name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [RegularExpression("^[A-Za-z0-9&-]+$", ErrorMessage = "Use letters, numbers, '&' or '-' only (no spaces).")]
    [Display(Name = "Code", Description = "Short code used in reference numbers, e.g. HSE")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose the division this department belongs to.")]
    [Display(Name = "Division")]
    public int? DivisionId { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "Head of Department")]
    public string? HeadOfDepartmentUserId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

// ---------------------------------------------------------------- Units

/// <summary>One row on the Units list (or in a department's unit table).</summary>
public record UnitListItemDto(
    int Id,
    int DepartmentId,
    string DepartmentName,
    string Name,
    string Code,
    bool IsActive);

/// <summary>The Unit create/edit form.</summary>
public class UnitFormDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Choose the department this unit belongs to.")]
    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Unit name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [RegularExpression("^[A-Za-z0-9&-]+$", ErrorMessage = "Use letters, numbers, '&' or '-' only (no spaces).")]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

// ---------------------------------------------------------------- Sites

/// <summary>One row on the Sites list.</summary>
public record SiteListItemDto(int Id, string Name, string Code, string? SiteType, string? Location, bool IsActive);

/// <summary>The Site create/edit form.</summary>
public class SiteFormDto
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Site name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [RegularExpression("^[A-Za-z0-9&-]+$", ErrorMessage = "Use letters, numbers, '&' or '-' only (no spaces).")]
    public string Code { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Site type", Description = "e.g. Rubber Plantation Estate")]
    public string? SiteType { get; set; }

    [StringLength(300)]
    [Display(Name = "Location / address")]
    public string? Location { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>A simple id + text pair used to fill drop-down lists.</summary>
public record LookupItem(string Value, string Text);

/// <summary>A drop-down item shown under a heading (e.g. a department under its division).</summary>
public record GroupedLookupItem(string Value, string Text, string Group);
