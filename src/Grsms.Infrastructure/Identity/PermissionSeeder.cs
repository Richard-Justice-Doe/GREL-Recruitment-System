using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Identity;

/// <summary>
/// Phase 14: gives each NEW permission to its default roles (<see cref="Permissions.Defaults"/>), once.
///
/// Runs at every start-up (from DbInitializer) and does nothing when there is nothing new. A permission counts as
/// "already handled" once it has a row in KnownPermissions, so the Super Admin's later choices are never overwritten.
/// The grants are ordinary RoleClaims rows and appear in the audit trail as changes made by "system".
/// </summary>
public static class PermissionSeeder
{
    public static async Task ApplyNewDefaultsAsync(ApplicationDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        var known = (await db.KnownPermissions.Select(k => k.Name).ToListAsync(ct)).ToHashSet();
        var fresh = Permissions.Catalogue.Where(p => !known.Contains(p.Name)).ToList();
        if (fresh.Count == 0) return;

        var roleIds = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id, ct);
        var now = DateTime.UtcNow;
        foreach (var p in fresh)
        {
            if (!p.SuperAdminOnly) // a Super Admin holds every permission without a row
                foreach (var (role, granted) in Permissions.Defaults)
                    if (granted.Contains(p.Name) && roleIds.TryGetValue(role, out var roleId))
                        db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = roleId, ClaimType = Permissions.ClaimType, ClaimValue = p.Name });
            db.KnownPermissions.Add(new KnownPermission { Name = p.Name, AddedAt = now });
        }
        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Default role permissions applied for {Count} new permission(s): {Names}",
            fresh.Count, string.Join(", ", fresh.Select(p => p.Name)));
    }
}
