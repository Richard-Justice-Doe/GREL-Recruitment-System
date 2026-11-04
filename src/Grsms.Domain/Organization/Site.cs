using Grsms.Domain.Common;

namespace Grsms.Domain.Organization;

/// <summary>
/// A physical work location, for example "Head Office" or a plantation estate.
///
/// Sites are independent of departments: the same department (e.g. Security) can have staff at
/// several sites. Each recruitment request records where the new hire will work.
/// </summary>
public class Site : BaseEntity
{
    /// <summary>Site name shown on screens. Must be unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, e.g. "HQ". Must be unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What kind of site it is, e.g. "Rubber Plantation Estate" or "Rubber Processing Factory". Optional.</summary>
    public string? SiteType { get; set; }

    /// <summary>Town, region or address, so applicants know where the job is.</summary>
    public string? Location { get; set; }

    /// <summary>Inactive sites are kept for history but cannot be chosen for new requests.</summary>
    public bool IsActive { get; set; } = true;
}
