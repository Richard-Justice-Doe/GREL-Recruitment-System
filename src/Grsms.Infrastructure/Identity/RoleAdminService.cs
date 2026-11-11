using System.Text.RegularExpressions;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Users;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Identity;

/// <summary>
/// Phase 14: role and permission management for the Super Admin.
///
///   * Permissions: which permissions each role holds (RoleClaims rows). The Super Admin column is fixed (it holds
///     everything); Candidate can hold none; "Super Admin only" permissions can never be given to another role.
///   * Custom roles: a Super Admin may add roles such as "Recruitment Auditor", give them permissions, rename them,
///     and delete them while nobody holds them. Built-in roles cannot be renamed or deleted, because the workflows
///     check them by name.
///
/// Every method checks that the signed-in user is a Super Admin, so the rule holds whichever screen calls it.
/// Every change is a database change, so it is in the audit trail automatically (record types "Role" and "RoleClaim").
/// </summary>
public partial class RoleAdminService(
    ApplicationDbContext db,
    RoleManager<IdentityRole> roleManager,
    ICurrentUserService me,
    IPermissionStore store) : IRoleAdminService
{
    /// <summary>Claim type holding a custom role's description (a built-in role's description is in code).</summary>
    public const string DescriptionClaimType = "grsms:description";

    private const string NotAllowed = "Only a Super Admin can manage roles and permissions.";
    private bool IsSuperAdmin => me.IsInRole(Roles.SuperAdmin);

    public async Task<IReadOnlyList<RoleSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await db.Roles.AsNoTracking().ToListAsync(ct);
        var claims = await db.RoleClaims.AsNoTracking()
            .Where(c => c.ClaimType == Permissions.ClaimType || c.ClaimType == DescriptionClaimType)
            .ToListAsync(ct);
        var activeUsers = await (from ur in db.UserRoles
                                 join u in db.Users on ur.UserId equals u.Id
                                 where u.IsActive
                                 group ur by ur.RoleId into g
                                 select new { RoleId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        return roles
            .OrderBy(r => Roles.IsBuiltIn(r.Name!) ? Roles.All.ToList().IndexOf(r.Name!) : int.MaxValue)
            .ThenBy(r => r.Name)
            .Select(r =>
            {
                var mine = claims.Where(c => c.RoleId == r.Id).ToList();
                var perms = r.Name == Roles.SuperAdmin
                    ? Permissions.Catalogue.Select(p => p.Name).ToHashSet()
                    : mine.Where(c => c.ClaimType == Permissions.ClaimType).Select(c => c.ClaimValue!).ToHashSet();
                var description = Roles.IsBuiltIn(r.Name!)
                    ? Roles.Describe(r.Name!)
                    : mine.FirstOrDefault(c => c.ClaimType == DescriptionClaimType)?.ClaimValue ?? "";
                return new RoleSummaryDto(r.Id, r.Name!, description, Roles.IsBuiltIn(r.Name!), activeUsers.GetValueOrDefault(r.Id), perms);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<RoleHistoryEntryDto>> GetHistoryAsync(string? roleId, int take = 100, CancellationToken ct = default)
    {
        string[] types = ["RoleClaim", "Role", "UserRole"];
        var q = db.AuditLogs.AsNoTracking().Where(a => a.EntityName != null && types.Contains(a.EntityName));
        // A role's own rows: its permissions/description (RoleId in the values), the role itself (its id), its holders ("user,role").
        if (!string.IsNullOrEmpty(roleId))
            q = q.Where(a => a.EntityId == roleId || (a.EntityId != null && a.EntityId.EndsWith("," + roleId)) ||
                             (a.NewValues != null && a.NewValues.Contains("\"RoleId\":\"" + roleId + "\"")) ||
                             (a.OldValues != null && a.OldValues.Contains("\"RoleId\":\"" + roleId + "\"")));
        var rows = await q.OrderByDescending(a => a.Id).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);

        // Names for the ids in the rows: current role names (a deleted custom role keeps its readable id) and user names.
        var roleNames = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name!, ct);
        var userIds = rows.Where(r => r.EntityName == "UserRole").Select(r => (r.EntityId ?? "").Split(',')[0]).Distinct().ToList();
        var userNames = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FullName} ({u.Email})", ct);

        return rows.Select(a => Describe(a, roleNames, userNames)).OfType<RoleHistoryEntryDto>().ToList();
    }

    /// <summary>Turns one audit row into a plain-words line, or null if it is not one we describe.</summary>
    private static RoleHistoryEntryDto? Describe(Domain.Auditing.AuditLog a, IReadOnlyDictionary<string, string> roleNames,
        IReadOnlyDictionary<string, string> userNames)
    {
        var values = Values(a.NewValues ?? a.OldValues);
        var old = Values(a.OldValues);
        string Role(string? id) => id is null ? "?" : roleNames.GetValueOrDefault(id) ?? $"{id} (deleted)";
        var by = a.UserName ?? "system";
        var created = a.Action == Domain.Auditing.AuditActions.Created;
        var deleted = a.Action == Domain.Auditing.AuditActions.Deleted;

        switch (a.EntityName)
        {
            case "RoleClaim":
            {
                var role = Role(values.GetValueOrDefault("RoleId"));
                if (values.GetValueOrDefault("ClaimType") == DescriptionClaimType)
                    return new(a.Id, a.Timestamp, by, a.IpAddress, "role", null, role, $"changed the description of {role}");
                var perm = values.GetValueOrDefault("ClaimValue") ?? "?";
                var label = Permissions.Find(perm)?.Label ?? perm;
                return created ? new(a.Id, a.Timestamp, by, a.IpAddress, "permission", true, role, $"gave \"{label}\" to {role}")
                     : deleted ? new(a.Id, a.Timestamp, by, a.IpAddress, "permission", false, role, $"took \"{label}\" away from {role}")
                     : new(a.Id, a.Timestamp, by, a.IpAddress, "permission", null, role, $"changed a permission of {role}");
            }
            case "Role":
            {
                var name = values.GetValueOrDefault("Name") ?? Role(a.EntityId);
                if (created) return new(a.Id, a.Timestamp, by, a.IpAddress, "role", true, name, $"added the role {name}");
                if (deleted) return new(a.Id, a.Timestamp, by, a.IpAddress, "role", false, old.GetValueOrDefault("Name") ?? name,
                    $"deleted the role {old.GetValueOrDefault("Name") ?? name}");
                var newName = Values(a.NewValues).GetValueOrDefault("Name");
                return newName is null ? null
                    : new(a.Id, a.Timestamp, by, a.IpAddress, "role", null, newName, $"renamed the role {old.GetValueOrDefault("Name")} to {newName}");
            }
            case "UserRole":
            {
                var parts = (a.EntityId ?? "").Split(',');
                var role = Role(parts.ElementAtOrDefault(1));
                var user = userNames.GetValueOrDefault(parts[0]) ?? "a deleted account";
                return created ? new(a.Id, a.Timestamp, by, a.IpAddress, "assignment", true, role, $"gave the role {role} to {user}")
                     : deleted ? new(a.Id, a.Timestamp, by, a.IpAddress, "assignment", false, role, $"took the role {role} from {user}")
                     : null;
            }
            default:
                return null;
        }
    }

    /// <summary>The audit trail's JSON values as text (e.g. {"RoleId":"hr-manager"} → RoleId = hr-manager).</summary>
    private static Dictionary<string, string?> Values(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name,
                p => p.Value.ValueKind == System.Text.Json.JsonValueKind.String ? p.Value.GetString() : p.Value.ToString());
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public async Task<Result<(int Added, int Removed)>> SavePermissionsAsync(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> grants, CancellationToken ct = default)
    {
        if (!IsSuperAdmin) return Result.Failure<(int, int)>(NotAllowed);

        var errors = new List<string>();
        foreach (var (role, permissions) in grants)
        {
            if (role == Roles.SuperAdmin) continue; // fixed: holds everything
            if (role == Roles.Candidate && permissions.Count > 0)
                errors.Add("Candidates (external job seekers) cannot be given staff permissions.");
            foreach (var p in permissions)
            {
                if (Permissions.Find(p) is not { } info) errors.Add($"Unknown permission \"{p}\".");
                else if (info.SuperAdminOnly) errors.Add($"\"{info.Label}\" belongs to the Super Admin only and cannot be given to {role}.");
            }
        }
        if (errors.Count > 0) return Result.Failure<(int, int)>([.. errors.Distinct()]);

        var roles = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id, ct);
        var unknownRole = grants.Keys.FirstOrDefault(r => !roles.ContainsKey(r));
        if (unknownRole is not null) return Result.Failure<(int, int)>($"The role \"{unknownRole}\" no longer exists. Reload the page.");

        var existing = await db.RoleClaims.Where(c => c.ClaimType == Permissions.ClaimType).ToListAsync(ct);
        int added = 0, removed = 0;
        foreach (var (role, permissions) in grants)
        {
            if (role == Roles.SuperAdmin) continue;
            var roleId = roles[role];
            var current = existing.Where(c => c.RoleId == roleId).ToList();
            foreach (var row in current.Where(c => !permissions.Contains(c.ClaimValue!)))
            {
                db.RoleClaims.Remove(row);
                removed++;
            }
            foreach (var p in permissions.Distinct().Where(p => current.All(c => c.ClaimValue != p)))
            {
                db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = roleId, ClaimType = Permissions.ClaimType, ClaimValue = p });
                added++;
            }
        }
        await db.SaveChangesAsync(ct);
        store.Refresh();
        return Result.Success((added, removed));
    }

    public async Task<RoleFormDto?> GetForEditAsync(string id, CancellationToken ct = default)
    {
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null || Roles.IsBuiltIn(role.Name!)) return null;
        var description = await db.RoleClaims.AsNoTracking()
            .Where(c => c.RoleId == id && c.ClaimType == DescriptionClaimType).Select(c => c.ClaimValue).FirstOrDefaultAsync(ct);
        return new RoleFormDto { Id = role.Id, Name = role.Name!, Description = description };
    }

    public async Task<Result<string>> CreateAsync(RoleFormDto form, CancellationToken ct = default)
    {
        if (!IsSuperAdmin) return Result.Failure<string>(NotAllowed);
        Normalize(form);
        if (await CheckNameAsync(form.Name, null, ct) is { } error) return Result.Failure<string>(error);

        // A readable id ("custom-recruitment-auditor") like the built-in roles, so the audit trail is easy to read.
        var id = "custom-" + SlugRegex().Replace(form.Name.ToLowerInvariant(), "-").Trim('-');
        if (await db.Roles.AnyAsync(r => r.Id == id, ct)) id += "-" + Guid.NewGuid().ToString("N")[..6];

        var role = new IdentityRole(form.Name) { Id = id };
        var created = await roleManager.CreateAsync(role);
        if (!created.Succeeded) return Result.Failure<string>(created.Errors.Select(e => e.Description).ToArray());
        await SetDescriptionAsync(id, form.Description, ct);
        return Result.Success(id);
    }

    public async Task<Result> UpdateAsync(RoleFormDto form, CancellationToken ct = default)
    {
        if (!IsSuperAdmin) return Result.Failure(NotAllowed);
        var role = await roleManager.FindByIdAsync(form.Id ?? "");
        if (role is null) return Result.Failure("Role not found.");
        if (Roles.IsBuiltIn(role.Name!)) return Result.Failure("Built-in roles cannot be renamed: the workflows check them by name.");
        Normalize(form);
        if (await CheckNameAsync(form.Name, role.Id, ct) is { } error) return Result.Failure(error);

        if (role.Name != form.Name)
        {
            // Users keep the role; their sign-in picks up the new name within 5 minutes (security stamp check).
            var renamed = await roleManager.SetRoleNameAsync(role, form.Name);
            if (renamed.Succeeded) renamed = await roleManager.UpdateAsync(role);
            if (!renamed.Succeeded) return Result.Failure(renamed.Errors.Select(e => e.Description).ToArray());
            store.Refresh();
        }
        await SetDescriptionAsync(role.Id, form.Description, ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken ct = default)
    {
        if (!IsSuperAdmin) return Result.Failure(NotAllowed);
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return Result.Failure("Role not found.");
        if (Roles.IsBuiltIn(role.Name!)) return Result.Failure("Built-in roles cannot be deleted.");
        var holders = await db.UserRoles.CountAsync(ur => ur.RoleId == id, ct);
        if (holders > 0)
            return Result.Failure($"{holders} user(s) still hold the role \"{role.Name}\". Remove it from them first (Users & roles).");

        // Remove the permissions and description first, so each removal is in the audit trail.
        db.RoleClaims.RemoveRange(await db.RoleClaims.Where(c => c.RoleId == id).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        var deleted = await roleManager.DeleteAsync(role);
        store.Refresh();
        return deleted.Succeeded ? Result.Success() : Result.Failure(deleted.Errors.Select(e => e.Description).ToArray());
    }

    // ===================================================================== Helpers

    private static void Normalize(RoleFormDto form)
    {
        form.Name = Regex.Replace(form.Name?.Trim() ?? "", @"\s+", " ");
        form.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
    }

    private async Task<string?> CheckNameAsync(string name, string? exceptId, CancellationToken ct)
    {
        if (name.Length < 3) return "Enter a role name of at least 3 characters.";
        if (Roles.All.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase)))
            return $"\"{name}\" is a built-in role name. Choose another name.";
        var normalized = name.ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.NormalizedName == normalized && r.Id != exceptId, ct))
            return $"A role called \"{name}\" already exists.";
        return null;
    }

    private async Task SetDescriptionAsync(string roleId, string? description, CancellationToken ct)
    {
        var row = await db.RoleClaims.FirstOrDefaultAsync(c => c.RoleId == roleId && c.ClaimType == DescriptionClaimType, ct);
        if (description is null)
        {
            if (row is not null) db.RoleClaims.Remove(row);
        }
        else if (row is null)
            db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = roleId, ClaimType = DescriptionClaimType, ClaimValue = description });
        else if (row.ClaimValue != description)
            row.ClaimValue = description;
        await db.SaveChangesAsync(ct);
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
