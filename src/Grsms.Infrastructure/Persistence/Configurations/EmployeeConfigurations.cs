using Grsms.Domain.Employees;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 28 (11 November 2026, docs/30): the employee master record. New tables only; no existing table changes.

/// <summary>Table "Employees": one per person.</summary>
internal class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("Employees");
        b.ConfigureBase();
        b.Property(e => e.EmployeeNumber).HasMaxLength(30).IsRequired();
        b.HasIndex(e => e.EmployeeNumber).IsUnique();
        b.Property(e => e.UserId).HasMaxLength(255).IsRequired();
        b.HasIndex(e => e.UserId).IsUnique();
        b.Property(e => e.PositionTitle).HasMaxLength(150).IsRequired();
        b.Property(e => e.ReportsToUserId).HasMaxLength(255);
        b.Property(e => e.EmploymentType).HasConversion<string>().HasMaxLength(30);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.Version).IsConcurrencyToken();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.ReportsToUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(e => e.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(e => e.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Grade>().WithMany().HasForeignKey(e => e.GradeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.DepartmentId, e.Status });
    }
}

/// <summary>Table "EmployeeAssignments": the job history (rows are closed, never edited).</summary>
internal class EmployeeAssignmentConfiguration : IEntityTypeConfiguration<EmployeeAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeAssignment> b)
    {
        b.ToTable("EmployeeAssignments");
        b.ConfigureBase();
        b.Property(a => a.PositionTitle).HasMaxLength(150).IsRequired();
        b.Property(a => a.ReportsToUserId).HasMaxLength(255);
        b.Property(a => a.EmploymentType).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Note).HasMaxLength(500);
        b.Property(a => a.RecordedByUserId).HasMaxLength(255);
        b.HasOne<Employee>().WithMany(e => e.Assignments).HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(a => a.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Grade>().WithMany().HasForeignKey(a => a.GradeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ReportsToUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.EmployeeId, a.EffectiveFrom });
    }
}

/// <summary>Table "Grades": GREL's salary grades (none seeded; HR enters them).</summary>
internal class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> b)
    {
        b.ToTable("Grades");
        b.ConfigureBase();
        b.Property(g => g.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(g => g.Code).IsUnique();
        b.Property(g => g.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(g => g.Name).IsUnique();
        b.Property(g => g.MinMonthlySalary).HasPrecision(18, 2);
        b.Property(g => g.MaxMonthlySalary).HasPrecision(18, 2);
    }
}
