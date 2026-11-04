using Grsms.Domain.Organization;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Grsms.Infrastructure.Persistence.Configurations.BaseEntityConfiguration;

namespace Grsms.Infrastructure.Persistence.Configurations;

/// <summary>Table "Departments": columns, unique indexes, HOD foreign key and the 24 standard GREL departments.</summary>
internal class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.ConfigureBase();

        b.Property(d => d.Name).HasMaxLength(150).IsRequired();
        b.Property(d => d.Code).HasMaxLength(20).IsRequired();
        b.Property(d => d.Description).HasMaxLength(1000);
        b.Property(d => d.HeadOfDepartmentUserId).HasMaxLength(255);

        // No two departments may share a name or a code.
        b.HasIndex(d => d.Name).IsUnique();
        b.HasIndex(d => d.Code).IsUnique();

        // HeadOfDepartmentUserId → Users.Id. If that user account is ever removed, the department
        // simply has no HOD (SET NULL) instead of the delete failing.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.HeadOfDepartmentUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // A department's units. RESTRICT: a department cannot be hard-deleted while it has units.
        b.HasMany(d => d.Units)
            .WithOne(u => u.Department)
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // The standard departments from the GRSMS specification. HR Administrators can add more,
        // rename or deactivate them from the Departments screen.
        (string Name, string Code)[] seed =
        [
            ("Human Resources", "HR"),
            ("Information Technology", "IT"),
            ("Finance", "FIN"),
            ("Procurement", "PROC"),
            ("Supply Chain", "SCM"),
            ("Sales and Marketing", "SALES"),
            ("Production", "PROD"),
            ("Engineering", "ENG"),
            ("Maintenance", "MAINT"),
            ("Health, Safety & Environment (HSE)", "HSE"),
            ("Quality Assurance", "QA"),
            ("Research & Development", "RND"),
            ("Legal & Compliance", "LEGAL"),
            ("Security", "SEC"),
            ("Administration", "ADMIN"),
            ("Corporate Affairs", "CA"),
            ("Transport & Logistics", "TRANS"),
            ("Warehouse", "WH"),
            ("Projects", "PROJ"),
            ("Internal Audit", "IA"),
            ("Medical Services", "MED"),
            ("Community Relations", "CR"),
            ("Sustainability", "SUS"),
            ("Business Development", "BD"),
        ];
        b.HasData(seed.Select((d, i) => new Department
        {
            Id = i + 1,
            Name = d.Name,
            Code = d.Code,
            IsActive = true,
            CreatedAt = SeedDate,
            CreatedBy = SeedUser
        }));
    }
}

/// <summary>Table "Units": sub-sections of departments. Names and codes are unique per department.</summary>
internal class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.ToTable("Units");
        b.ConfigureBase();

        b.Property(u => u.Name).HasMaxLength(150).IsRequired();
        b.Property(u => u.Code).HasMaxLength(20).IsRequired();
        b.Property(u => u.Description).HasMaxLength(1000);

        // Composite unique indexes: "Admin" may exist in Finance AND in IT, but only once in each.
        b.HasIndex(u => new { u.DepartmentId, u.Name }).IsUnique();
        b.HasIndex(u => new { u.DepartmentId, u.Code }).IsUnique();
    }
}

/// <summary>Table "Sites": physical work locations. Seeded with "Head Office".</summary>
internal class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> b)
    {
        b.ToTable("Sites");
        b.ConfigureBase();

        b.Property(s => s.Name).HasMaxLength(150).IsRequired();
        b.Property(s => s.Code).HasMaxLength(20).IsRequired();
        b.Property(s => s.Location).HasMaxLength(300);

        b.HasIndex(s => s.Name).IsUnique();
        b.HasIndex(s => s.Code).IsUnique();

        // Only Head Office is seeded; HR adds the real estates/locations from the Sites screen.
        b.HasData(new Site
        {
            Id = 1,
            Name = "Head Office",
            Code = "HQ",
            IsActive = true,
            CreatedAt = SeedDate,
            CreatedBy = SeedUser
        });
    }
}
