using System.Linq.Expressions;
using Grsms.Domain.Common;

namespace Grsms.Application.Common.Interfaces;

/// <summary>
/// Generic data access for one kind of record (Repository pattern).
///
/// Services talk to this interface instead of the database directly. That keeps business rules
/// free of database code, and lets tests swap in a fake repository.
/// The real implementation (Infrastructure/Persistence/Repository.cs) uses Entity Framework Core.
///
/// Soft-deleted rows are never returned: the database layer filters them out automatically.
/// </summary>
/// <typeparam name="T">Any entity that inherits <see cref="BaseEntity"/>.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Finds one record by primary key, or null if it does not exist (or was deleted).</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns all records that match <paramref name="filter"/> (or every record when it is null).</summary>
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default);

    /// <summary>True if at least one record matches. Used for "is this code already taken?" checks.</summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>Counts the records that match <paramref name="filter"/> (or all records).</summary>
    Task<int> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default);

    /// <summary>Marks a new record to be inserted on the next <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(T entity);

    /// <summary>
    /// Marks a record to be deleted on the next save. It is a SOFT delete: the row stays in the
    /// table with IsDeleted = true, so the audit history is kept.
    /// </summary>
    void Remove(T entity);
}

/// <summary>
/// Saves every pending change from all repositories in ONE database transaction,
/// so a business operation either completes fully or not at all.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Writes all pending changes. Audit fields and audit-trail rows are added automatically.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
