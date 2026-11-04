using Grsms.Domain.Organization;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Grsms.Infrastructure.Persistence.Configurations.BaseEntityConfiguration;

namespace Grsms.Infrastructure.Persistence.Configurations;

/// <summary>Table "Divisions": the top level of the organisation (Corporate, Operations).</summary>
internal class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public const int CorporateId = 1, OperationsId = 2;

    public void Configure(EntityTypeBuilder<Division> b)
    {
        b.ToTable("Divisions");
        b.ConfigureBase();

        b.Property(d => d.Name).HasMaxLength(100).IsRequired();
        b.Property(d => d.Code).HasMaxLength(20).IsRequired();
        b.Property(d => d.Description).HasMaxLength(1000);
        b.HasIndex(d => d.Name).IsUnique();
        b.HasIndex(d => d.Code).IsUnique();

        b.HasData(
            new Division { Id = CorporateId, Name = "Corporate", Code = "CORP", IsActive = true, CreatedAt = SeedDate, CreatedBy = SeedUser },
            new Division { Id = OperationsId, Name = "Operations", Code = "OPS", IsActive = true, CreatedAt = SeedDate, CreatedBy = SeedUser });
    }
}

/// <summary>Table "Departments": columns, unique indexes, division and HOD foreign keys, and GREL's departments.</summary>
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

        // Every department belongs to one division. RESTRICT: a division cannot be hard-deleted while it has departments.
        b.HasOne(d => d.Division)
            .WithMany(v => v.Departments)
            .HasForeignKey(d => d.DivisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // GREL's 17 departments in its two divisions (from the GREL requirements). HR Administrators can add
        // more, rename, move or deactivate them from the Departments screen.
        //
        // Ids are permanent: rows 1-24 were the original placeholder list. Departments GREL does not have are
        // kept but inactive (Active = false), so ids never shift and old records stay valid; GREL's departments
        // that were missing were added from id 25. Inactive rows still need a division; the nearest fit is used.
        const int C = DivisionConfiguration.CorporateId, O = DivisionConfiguration.OperationsId;
        (string Name, string Code, bool Active, int Division)[] seed =
        [
            ("Human Resources", "HR", true, C),                       // 1
            ("Information Technology", "IT", true, C),                // 2
            ("Finance", "FIN", true, C),                              // 3
            ("Procurement", "PROC", true, C),                         // 4
            ("Supply Chain", "SCM", true, C),                         // 5
            ("Sales and Marketing", "SALES", false, C),               // 6  placeholder
            ("Production", "PROD", false, O),                         // 7  placeholder
            ("Engineering", "ENG", false, O),                         // 8  placeholder
            ("Maintenance", "MAINT", false, O),                       // 9  placeholder
            ("Health, Safety & Environment (HSE)", "HSE", false, O),  // 10 placeholder
            ("Progress and Quality", "PQ", false, O),                 // 11 not in the requirements list
            ("Research & Development", "RND", false, C),              // 12 placeholder
            ("Legal & Compliance", "LEGAL", false, C),                // 13 placeholder
            ("Security", "SEC", false, C),                            // 14 placeholder
            ("Administration", "ADMIN", true, C),                     // 15
            ("Corporate Affairs", "CA", true, C),                     // 16
            ("Transport", "TRANS", true, C),                          // 17 (was "Transport & Logistics")
            ("Warehouse", "WH", false, O),                            // 18 placeholder
            ("Project", "PROJ", false, O),                            // 19 not in the requirements list
            ("Internal Audit", "IA", true, C),                        // 20
            ("Medical Services", "MED", false, C),                    // 21 placeholder
            ("Community Relations", "CR", false, C),                  // 22 placeholder
            ("Sustainability", "SUS", false, C),                      // 23 placeholder
            ("Business Development", "BD", false, C),                 // 24 placeholder
            ("Civil & Infrastructure", "CIV", true, O),               // 25
            ("Stores", "STR", true, O),                               // 26
            ("Garage", "GAR", true, O),                               // 27
            ("Estates", "EST", false, O),                             // 28 not in the requirements list
            ("RuTAP", "RUTAP", true, O),                              // 29
            ("DTC", "DTC", true, O),                                  // 30
            ("Factory APM", "APM", true, O),                          // 31
            ("Factory TBU", "TBU", true, O),                          // 32
            ("Nucleus Operations", "NUC", true, O),                   // 33
        ];
        b.HasData(seed.Select((d, i) => new Department
        {
            Id = i + 1,
            Name = d.Name,
            Code = d.Code,
            IsActive = d.Active,
            DivisionId = d.Division,
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

        b.Property(s => s.SiteType).HasMaxLength(150);

        // GREL's 12 sites, as listed in GREL's own systems, with the site types from the GREL requirements.
        // Tsibu is the TBU factory. Sites without a known type are left blank for HR to fill in.
        // Ids 13-14 were briefly separate sites ("TBU Factory", "Nucleus Operations Areas"); they are kept but
        // inactive so ids never shift. HR can add, edit or deactivate sites.
        const string Estate = "Rubber Plantation Estate", Factory = "Rubber Processing Factory";
        (string Name, string Code, string? Type, string? Location, bool Active)[] seed =
        [
            ("Head Office", "HQ", "Corporate Headquarters", "Takoradi", true),                               // 1
            ("Abura", "ABU", Estate, null, true),                                                            // 2
            ("Apeminim", "APM", Factory, null, true),                                                        // 3
            ("RuTAP", "RUTAP", "Rubber Outgrower & Technical Assistance Programme Operations", null, true), // 4
            ("Tsibu", "TSI", Factory + " (1D1F Expansion Project)", null, true),                             // 5  the TBU factory
            ("Allabokazo", "ALB", null, null, true),                                                         // 6
            ("Assin Adubease", "ASA", null, null, true),                                                     // 7
            ("Manso Amenfi", "MAN", null, null, true),                                                       // 8
            ("Subri", "SUB", Estate, null, true),                                                            // 9
            ("Awodua", "AWO", null, null, true),                                                             // 10
            ("Nsuame", "NSU", null, null, true),                                                             // 11
            ("Agona Abream", "AGA", null, null, true),                                                       // 12
            ("TBU Factory", "TBU", Factory + " (1D1F Expansion Project)", null, false),                      // 13 = Tsibu; inactive
            ("Nucleus Operations Areas", "NUC", "Estate Management and Plantation Operations", null, false), // 14 not a GREL site; inactive
        ];
        b.HasData(seed.Select((s, i) => new Site
        {
            Id = i + 1,
            Name = s.Name,
            Code = s.Code,
            SiteType = s.Type,
            Location = s.Location,
            IsActive = s.Active,
            CreatedAt = SeedDate,
            CreatedBy = SeedUser
        }));
    }
}
