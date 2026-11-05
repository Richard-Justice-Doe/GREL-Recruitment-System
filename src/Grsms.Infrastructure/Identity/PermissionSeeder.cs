using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Identity;

/// <summary>
/// Phase 14: gives each NEW permission to its default roles (<see cref="Permissions.Defaults"/>), once; and gives each
/// built-in role added later (<see cref="Permissions.RolesAddedLater"/>, e.g. Admin) all its defaults, once.
///
/// Runs at every start-up (from DbInitializer) and does nothing when there is nothing new. What has been handled is
/// recorded in KnownPermissions (a permission by its name, a later role as "role:&lt;name&gt;"), so the Super Admin's
/// later choices are never overwritten. The grants are ordinary RoleClaims rows and appear in the audit trail as "system".
/// </summary>
public static class PermissionSeeder
{
    public const string RoleMarkerPrefix = "role:";

    public static async Task ApplyNewDefaultsAsync(ApplicationDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        var known = (await db.KnownPermissions.Select(k => k.Name).ToListAsync(ct)).ToHashSet();
        var freshPermissions = Permissions.Catalogue.Where(p => !known.Contains(p.Name)).ToList();
        var freshRoles = Permissions.RolesAddedLater.Where(r => !known.Contains(RoleMarkerPrefix + r)).ToList();
        if (freshPermissions.Count == 0 && freshRoles.Count == 0) return;

        var roleIds = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id, ct);
        var existing = (await db.RoleClaims.Where(c => c.ClaimType == Permissions.ClaimType).Select(c => new { c.RoleId, c.ClaimValue }).ToListAsync(ct))
            .Select(c => (c.RoleId, c.ClaimValue!)).ToHashSet();
        var now = DateTime.UtcNow;

        void Grant(string role, string permission)
        {
            if (Permissions.IsSuperAdminOnly(permission) || !roleIds.TryGetValue(role, out var roleId)) return; // the Super Admin needs no rows
            if (!existing.Add((roleId, permission))) return; // already held (e.g. a brand-new installation handles both lists)
            db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = roleId, ClaimType = Permissions.ClaimType, ClaimValue = permission });
        }

        foreach (var p in freshPermissions)
        {
            foreach (var (role, granted) in Permissions.Defaults)
                if (granted.Contains(p.Name)) Grant(role, p.Name);
            db.KnownPermissions.Add(new KnownPermission { Name = p.Name, AddedAt = now });
        }
        foreach (var role in freshRoles)
        {
            foreach (var p in Permissions.Defaults.GetValueOrDefault(role) ?? []) Grant(role, p);
            db.KnownPermissions.Add(new KnownPermission { Name = RoleMarkerPrefix + role, AddedAt = now });
        }
        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Default role permissions applied for {Count} new permission(s) {Names} and {RoleCount} new role(s) {Roles}",
            freshPermissions.Count, string.Join(", ", freshPermissions.Select(p => p.Name)), freshRoles.Count, string.Join(", ", freshRoles));
    }
}
