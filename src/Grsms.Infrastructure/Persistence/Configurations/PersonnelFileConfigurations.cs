using Grsms.Domain.Documents;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 20 tables: induction area settings and the electronic personnel file.

/// <summary>Table "InductionAreaSettings": one row per induction area (owner, default, tasks).</summary>
internal class InductionAreaSettingConfiguration : IEntityTypeConfiguration<InductionAreaSetting>
{
    public void Configure(EntityTypeBuilder<InductionAreaSetting> b)
    {
        b.ToTable("InductionAreaSettings");
        b.ConfigureBase();
        b.Property(s => s.Area).HasConversion<string>().HasMaxLength(40);
        b.HasIndex(s => s.Area).IsUnique();
        b.Property(s => s.OwnerUserId).HasMaxLength(255);
        b.Property(s => s.Tasks).HasColumnType("text");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Table "PersonnelFileCategories": the "folders", seeded with the seven built-in ones.</summary>
internal class PersonnelFileCategoryConfiguration : IEntityTypeConfiguration<PersonnelFileCategory>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCategory> b)
    {
        b.ToTable("PersonnelFileCategories");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.HasData(PersonnelFileCategories.All.Select(c => new PersonnelFileCategory { Id = c.Id, Name = c.Name, SortOrder = c.SortOrder }));
    }
}

/// <summary>Table "PersonnelFiles": one per employee.</summary>
internal class PersonnelFileConfiguration : IEntityTypeConfiguration<PersonnelFile>
{
    public void Configure(EntityTypeBuilder<PersonnelFile> b)
    {
        b.ToTable("PersonnelFiles");
        b.ConfigureBase();
        b.Property(f => f.FileNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(f => f.FileNumber).IsUnique();
        b.Property(f => f.EmployeeUserId).HasMaxLength(255).IsRequired();
        b.HasIndex(f => f.EmployeeUserId).IsUnique();
        b.Property(f => f.EmployeeName).HasMaxLength(200).IsRequired();
        b.Property(f => f.StaffNumber).HasMaxLength(30);
        b.Property(f => f.JobTitle).HasMaxLength(150).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(f => f.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(f => f.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(f => f.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(f => f.DepartmentId);
    }
}

/// <summary>Table "PersonnelFileDocuments": what is in each file, by category.</summary>
internal class PersonnelFileDocumentConfiguration : IEntityTypeConfiguration<PersonnelFileDocument>
{
    public void Configure(EntityTypeBuilder<PersonnelFileDocument> b)
    {
        b.ToTable("PersonnelFileDocuments");
        b.ConfigureBase();
        b.Property(d => d.Title).HasMaxLength(200).IsRequired();
        b.Property(d => d.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.Content).HasColumnType("text");
        b.Property(d => d.SourceType).HasMaxLength(50);
        b.Property(d => d.AddedByUserId).HasMaxLength(255);
        b.HasOne<PersonnelFile>().WithMany().HasForeignKey(d => d.PersonnelFileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonnelFileCategory>().WithMany().HasForeignKey(d => d.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Attachment>().WithMany().HasForeignKey(d => d.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.PersonnelFileId, d.CategoryId });
        b.HasIndex(d => new { d.SourceType, d.SourceId }); // "is this already filed?"
    }
}
