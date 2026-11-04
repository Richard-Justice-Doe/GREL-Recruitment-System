using Grsms.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared column rules for the standard audit and soft-delete columns that every
/// <see cref="BaseEntity"/> table has. Each table's configuration calls this first.
/// </summary>
internal static class BaseEntityConfiguration
{
    /// <summary>Applies lengths and an index on IsDeleted to the standard columns.</summary>
    public static void ConfigureBase<T>(this EntityTypeBuilder<T> b) where T : BaseEntity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.CreatedBy).HasMaxLength(256);
        b.Property(e => e.UpdatedBy).HasMaxLength(256);
        b.Property(e => e.DeletedBy).HasMaxLength(256);

        // Every query filters on IsDeleted, so index it.
        b.HasIndex(e => e.IsDeleted);
    }

    /// <summary>
    /// Fixed timestamp written into seed rows. Seed data must never change between builds,
    /// otherwise EF Core would generate a new migration every time.
    /// </summary>
    public static readonly DateTime SeedDate = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Name recorded in CreatedBy for rows inserted by the setup scripts.</summary>
    public const string SeedUser = "system";
}
