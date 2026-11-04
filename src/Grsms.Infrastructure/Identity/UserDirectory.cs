using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Identity;

/// <inheritdoc cref="IUserDirectory"/>
public class UserDirectory(ApplicationDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();

        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    public Task<bool> IsActiveUserInRoleAsync(string userId, string role, CancellationToken ct = default) =>
        UsersInRole(role).AnyAsync(u => u.Id == userId, ct);

    public async Task<IReadOnlyList<LookupItem>> GetActiveUsersInRoleAsync(string role, CancellationToken ct = default) =>
        await UsersInRole(role)
            .OrderBy(u => u.FullName)
            .Select(u => new LookupItem(u.Id, u.FullName + " (" + u.Email + ")"))
            .ToListAsync(ct);

    /// <summary>Active users who hold the named role (joins Users → UserRoles → Roles).</summary>
    private IQueryable<ApplicationUser> UsersInRole(string role) =>
        from u in db.Users.AsNoTracking()
        join ur in db.UserRoles on u.Id equals ur.UserId
        join r in db.Roles on ur.RoleId equals r.Id
        where r.Name == role && u.IsActive
        select u;
}
