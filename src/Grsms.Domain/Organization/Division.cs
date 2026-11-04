using Grsms.Domain.Common;

namespace Grsms.Domain.Organization;

/// <summary>
/// The top level of GREL's two-level organisation structure: a division groups departments.
/// GREL has two: <b>Corporate</b> (HR, Finance, IT, ...) and <b>Operations</b> (the factories, Stores, Garage, ...).
///
/// Every department belongs to exactly one division. Recruitment records (requests, vacancies and, in later
/// phases, applications, interviews, hiring reports and onboarding) store the division they were raised in,
/// so reports can be filtered by division even if a department is later moved to another division.
/// </summary>
public class Division : BaseEntity
{
    /// <summary>Full name, e.g. "Corporate". Must be unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, e.g. "CORP". Must be unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional notes about the division.</summary>
    public string? Description { get; set; }

    /// <summary>Inactive divisions are kept for history but no department can be moved into them.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The departments in this division.</summary>
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
