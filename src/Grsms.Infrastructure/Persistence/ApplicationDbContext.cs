using System.Linq.Expressions;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Auditing;
using Grsms.Domain.Common;
using Grsms.Domain.Organization;
using Grsms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core "database context": the single gateway between C# objects and MySQL.
///
/// * Each DbSet below is one table.
/// * Table/column details (lengths, indexes, foreign keys, seed rows) are NOT here; they are in the
///   Configurations folder, one file per table, so this class stays short.
/// * It inherits IdentityDbContext, which adds the sign-in tables (Users, Roles, UserRoles, ...).
/// * It implements <see cref="IUnitOfWork"/>, so services can save all their changes in one go.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options), IUnitOfWork
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity must configure its tables first; our configuration then renames/extends them.
        base.OnModelCreating(builder);

        // Load every IEntityTypeConfiguration class in this project (Persistence/Configurations/*.cs).
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // SOFT DELETE: for every entity that inherits BaseEntity, add the filter "WHERE IsDeleted = 0"
        // to every query automatically. Deleted rows are therefore invisible everywhere unless code
        // explicitly asks for them with .IgnoreQueryFilters().
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            var param = Expression.Parameter(entityType.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(param, nameof(BaseEntity.IsDeleted)),
                    Expression.Constant(false)),
                param);
            builder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    /// <inheritdoc />
    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);
}
