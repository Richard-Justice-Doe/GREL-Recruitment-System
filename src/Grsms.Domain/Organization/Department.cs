using Grsms.Domain.Common;

namespace Grsms.Domain.Organization;

/// <summary>
/// A GREL department, for example "Finance" or "Sales and Marketing".
///
/// Every recruitment activity (request, vacancy, interview, ...) belongs to exactly one department.
/// The department's Head of Department (HOD) is automatically included in that department's
/// recruitment workflow, together with the recruiter, the HR Manager and the Managing Director.
/// </summary>
public class Department : BaseEntity
{
    /// <summary>Full name shown on screens, e.g. "Health, Safety &amp; Environment (HSE)". Must be unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code used in reference numbers, e.g. "HSE". Must be unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional notes about what the department does.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The user (by Identity user id) who is Head of this department. Optional because a department
    /// may not have a HOD account yet. The HOD receives this department's recruitment requests.
    /// </summary>
    public string? HeadOfDepartmentUserId { get; set; }

    /// <summary>
    /// Inactive departments stay in the database (old records still point to them) but can no longer
    /// be chosen for new recruitment activity.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The sub-sections (units) inside this department, e.g. Finance → Payroll.</summary>
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}
