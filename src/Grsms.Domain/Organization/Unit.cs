using Grsms.Domain.Common;

namespace Grsms.Domain.Organization;

/// <summary>
/// A sub-section inside a department, for example Finance → "Payroll" or IT → "Infrastructure".
/// A unit always belongs to exactly one department. A recruitment request can name a unit
/// so the vacancy is placed precisely.
/// </summary>
public class Unit : BaseEntity
{
    /// <summary>The department this unit belongs to (foreign key to Departments.Id).</summary>
    public int DepartmentId { get; set; }

    /// <summary>Navigation property to the parent department.</summary>
    public Department? Department { get; set; }

    /// <summary>Unit name. Must be unique within its department (two departments may both have "Admin").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, e.g. "PAY". Unique within its department.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional notes.</summary>
    public string? Description { get; set; }

    /// <summary>Inactive units are kept for history but cannot be chosen for new requests.</summary>
    public bool IsActive { get; set; } = true;
}
