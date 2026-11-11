namespace Grsms.Domain.Security;

/// <summary>
/// Phase 14: one row per permission whose default grants have already been applied (table "KnownPermissions").
///
/// At start-up GRSMS gives each NEW permission in <see cref="Permissions.Catalogue"/> to its default roles and then
/// records it here. Because of this row, a permission the Super Admin later removes from a role is never given back
/// at the next start-up.
/// </summary>
public class KnownPermission
{
    /// <summary>The permission name, e.g. "vacancies.view". Primary key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When its defaults were applied (UTC).</summary>
    public DateTime AddedAt { get; set; }
}
