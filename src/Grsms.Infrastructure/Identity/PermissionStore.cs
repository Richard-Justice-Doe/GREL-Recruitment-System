using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Infrastructure.Identity;

/// <summary>
/// Phase 14: which permissions each role holds (RoleClaims rows of type <see cref="Permissions.ClaimType"/>).
///
/// Every page asks this many times, so the whole table (a few dozen rows) is kept in memory and re-read at most once a
/// minute. A change on the Roles &amp; permissions screen calls <see cref="Refresh"/>, so it applies straight away on
/// this server; with several servers the others follow within the minute.
///
/// Registered as a singleton (one copy for the whole application); it opens its own short database scope to read.
/// </summary>
public sealed class PermissionStore(IServiceScopeFactory scopes) : IPermissionStore
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);
    private readonly Lock _lock = new();
    private IReadOnlyDictionary<string, IReadOnlySet<string>>? _byRole;
    private DateTime _loadedAt;

    public IReadOnlySet<string> GrantedTo(string role) =>
        Current().TryGetValue(role, out var set) ? set : Empty;

    public void Refresh()
    {
        lock (_lock) _byRole = null;
    }

    private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

    private IReadOnlyDictionary<string, IReadOnlySet<string>> Current()
    {
        lock (_lock)
        {
            if (_byRole is not null && DateTime.UtcNow - _loadedAt < MaxAge) return _byRole;

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = (from c in db.RoleClaims.AsNoTracking()
                        join r in db.Roles.AsNoTracking() on c.RoleId equals r.Id
                        where c.ClaimType == Permissions.ClaimType
                        select new { Role = r.Name!, Permission = c.ClaimValue! }).ToList();

            _byRole = rows.GroupBy(x => x.Role)
                .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(x => x.Permission).ToHashSet());
            _loadedAt = DateTime.UtcNow;
            return _byRole;
        }
    }
}
