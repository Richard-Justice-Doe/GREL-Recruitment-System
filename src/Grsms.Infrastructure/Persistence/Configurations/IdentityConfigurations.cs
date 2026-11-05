using Grsms.Domain.Auditing;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table "Users" (ASP.NET Identity's AspNetUsers, renamed). Adds the GREL columns and their foreign keys.
/// </summary>
internal class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.ToTable("Users");

        b.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        b.Property(u => u.StaffNumber).HasMaxLength(30);

        // Staff numbers are unique. MySQL allows many NULLs in a unique index, so candidates
        // (who have no staff number) do not clash with each other.
        b.HasIndex(u => u.StaffNumber).IsUnique();
        b.HasIndex(u => u.IsCandidate);

        // RESTRICT: a department/site cannot be hard-deleted while users still belong to it.
        b.HasOne<Department>().WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(u => u.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Table "Roles", seeded with the built-in GRSMS roles (Super Admin added in Phase 14). Custom roles are added by a Super Admin.</summary>
internal class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> b)
    {
        b.ToTable("Roles");

        // Role ids are readable words (e.g. "hr-manager") instead of random GUIDs, so the audit trail
        // and the UserRoles table can be understood without looking anything up.
        b.HasData(Roles.All.Select(name => new IdentityRole
        {
            Id = RoleId(name),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = RoleId(name)
        }));
    }

    /// <summary>"Head of Department" → "head-of-department".</summary>
    public static string RoleId(string roleName) => roleName.ToLowerInvariant().Replace(' ', '-');
}

/// <summary>Gives the remaining Identity tables short names and safe key lengths for MySQL.</summary>
internal class IdentityTableNames :
    IEntityTypeConfiguration<IdentityUserRole<string>>,
    IEntityTypeConfiguration<IdentityUserClaim<string>>,
    IEntityTypeConfiguration<IdentityUserLogin<string>>,
    IEntityTypeConfiguration<IdentityUserToken<string>>,
    IEntityTypeConfiguration<IdentityRoleClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> b) => b.ToTable("UserRoles");
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> b) => b.ToTable("UserClaims");
    // Phase 14: a role's permissions are rows here (ClaimType "grsms:permission"). The index makes the
    // "does this role already hold it?" check and the duplicate guard cheap.
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<string>> b)
    {
        b.ToTable("RoleClaims");
        b.Property(c => c.ClaimType).HasMaxLength(100);
        b.Property(c => c.ClaimValue).HasMaxLength(300);
        b.HasIndex(c => new { c.RoleId, c.ClaimType, c.ClaimValue }).IsUnique();
    }

    // These two tables have multi-column text primary keys. MySQL limits an index to 3072 bytes
    // (each utf8mb4 character can take 4 bytes), so the key columns are kept to 128 characters.
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> b)
    {
        b.ToTable("UserLogins");
        b.Property(l => l.LoginProvider).HasMaxLength(128);
        b.Property(l => l.ProviderKey).HasMaxLength(128);
    }

    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> b)
    {
        b.ToTable("UserTokens");
        b.Property(t => t.LoginProvider).HasMaxLength(128);
        b.Property(t => t.Name).HasMaxLength(128);
    }
}

/// <summary>Phase 14: table "KnownPermissions" (see <see cref="KnownPermission"/>).</summary>
internal class KnownPermissionConfiguration : IEntityTypeConfiguration<KnownPermission>
{
    public void Configure(EntityTypeBuilder<KnownPermission> b)
    {
        b.ToTable("KnownPermissions");
        b.HasKey(k => k.Name);
        b.Property(k => k.Name).HasMaxLength(100);
    }
}

/// <summary>Table "AuditLogs": append-only history. Indexed for the filters on the Audit Trail screen.</summary>
internal class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(a => a.Id);

        b.Property(a => a.UserId).HasMaxLength(255);
        b.Property(a => a.UserName).HasMaxLength(256);
        b.Property(a => a.Action).HasMaxLength(50).IsRequired();
        b.Property(a => a.EntityName).HasMaxLength(100);
        b.Property(a => a.EntityId).HasMaxLength(255);
        b.Property(a => a.IpAddress).HasMaxLength(45); // 45 = longest possible IPv6 text
        // OldValues/NewValues have no max length → MySQL "longtext", which fits any JSON.

        b.HasIndex(a => a.Timestamp);
        b.HasIndex(a => new { a.EntityName, a.EntityId });
        b.HasIndex(a => a.UserName);
        b.HasIndex(a => a.Action);

        // No foreign key to Users on purpose: audit rows must survive even if a user record is removed.
    }
}
