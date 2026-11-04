using System.Linq.Expressions;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core implementation of <see cref="IRepository{T}"/>.
/// One generic class serves every table; it is registered once as an "open generic" in
/// DependencyInjection.cs, so IRepository&lt;Department&gt;, IRepository&lt;Site&gt;, ... all work automatically.
///
/// The soft-delete filter set up in ApplicationDbContext applies to every query below.
/// </summary>
public class Repository<T>(ApplicationDbContext db) : IRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _set = db.Set<T>();

    /// <summary>The database context, for specialised repositories that add their own queries.</summary>
    protected ApplicationDbContext Db { get; } = db;

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        // FirstOrDefaultAsync (rather than FindAsync) guarantees the soft-delete filter is applied.
        _set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default) =>
        filter is null
            ? await _set.ToListAsync(ct)
            : await _set.Where(filter).ToListAsync(ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default) =>
        filter is null ? _set.CountAsync(ct) : _set.CountAsync(filter, ct);

    public void Add(T entity) => _set.Add(entity);

    // Marked as Deleted here; AuditSaveChangesInterceptor converts it into a soft delete when saving.
    public void Remove(T entity) => _set.Remove(entity);
}
