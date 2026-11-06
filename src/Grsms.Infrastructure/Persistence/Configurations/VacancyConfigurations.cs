using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 3 tables. Enums are stored as readable text.

/// <summary>Table "Vacancies".</summary>
internal class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> b)
    {
        b.ToTable("Vacancies");
        b.ConfigureBase();

        b.Property(v => v.VacancyNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(v => v.VacancyNumber).IsUnique();
        b.Property(v => v.RequisitionNumber).HasMaxLength(40);
        b.HasIndex(v => v.RequisitionNumber).IsUnique(); // several NULLs (older vacancies) are allowed by MySQL

        // ONE vacancy per approved request: enforced by the database as well as by the service.
        b.HasIndex(v => v.RecruitmentRequestId).IsUnique();

        b.Property(v => v.JobTitle).HasMaxLength(150).IsRequired();
        b.Property(v => v.JobDescription).IsRequired(); // no max length → LONGTEXT (the form limits it to 8,000 characters)
        b.Property(v => v.FieldOfStudy).HasMaxLength(200);
        b.Property(v => v.QualificationNotes).HasMaxLength(1000);
        b.Property(v => v.ExperienceNotes).HasMaxLength(1000);
        b.Property(v => v.SalaryMin).HasPrecision(18, 2);
        b.Property(v => v.SalaryMax).HasPrecision(18, 2);

        b.Property(v => v.EmploymentType).HasConversion<string>().HasMaxLength(30);
        b.Property(v => v.VacancyType).HasConversion<string>().HasMaxLength(30);
        b.Property(v => v.MinimumEducation).HasConversion<string>().HasMaxLength(30);
        b.Property(v => v.Status).HasConversion<string>().HasMaxLength(30);

        b.Property(v => v.ReportingManagerUserId).HasMaxLength(255);
        b.Property(v => v.RecruiterUserId).HasMaxLength(255).IsRequired();
        b.Property(v => v.RequestingHodUserId).HasMaxLength(255).IsRequired();
        b.Property(v => v.Version).IsConcurrencyToken();

        b.HasOne<RecruitmentRequest>().WithMany().HasForeignKey(v => v.RecruitmentRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(v => v.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(v => v.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unit>().WithMany().HasForeignKey(v => v.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(v => v.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.ReportingManagerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.RecruiterUserId).OnDelete(DeleteBehavior.Restrict);

        // Requirement rows belong to the vacancy and are replaced on every edit (CASCADE removes orphans).
        b.HasMany(v => v.Requirements).WithOne().HasForeignKey(r => r.VacancyId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(v => v.KnockoutQuestions).WithOne().HasForeignKey(q => q.VacancyId).OnDelete(DeleteBehavior.Cascade); // Phase 15
        b.HasMany(v => v.Actions).WithOne().HasForeignKey(a => a.VacancyId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(v => v.Status);
        b.HasIndex(v => new { v.DepartmentId, v.Status });
        b.HasIndex(v => new { v.DivisionId, v.Status }); // reports by division
        b.HasIndex(v => new { v.Status, v.ClosingDate }); // the automatic-closing query
    }
}

/// <summary>Phase 15: table "VacancyKnockoutQuestions".</summary>
internal class VacancyKnockoutQuestionConfiguration : IEntityTypeConfiguration<VacancyKnockoutQuestion>
{
    public void Configure(EntityTypeBuilder<VacancyKnockoutQuestion> b)
    {
        b.ToTable("VacancyKnockoutQuestions");
        b.HasKey(q => q.Id);
        b.Property(q => q.Text).HasMaxLength(300).IsRequired();
    }
}

/// <summary>Table "VacancyRequirements": skills and certifications, essential or desirable.</summary>
internal class VacancyRequirementConfiguration : IEntityTypeConfiguration<VacancyRequirement>
{
    public void Configure(EntityTypeBuilder<VacancyRequirement> b)
    {
        b.ToTable("VacancyRequirements");
        b.HasKey(r => r.Id);
        b.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(r => new { r.VacancyId, r.Type });

        // Lets later phases ask "which vacancies need skill X?" quickly.
        b.HasIndex(r => r.Name);
    }
}

/// <summary>Table "VacancyActions": a vacancy's history.</summary>
internal class VacancyActionConfiguration : IEntityTypeConfiguration<VacancyActionEntry>
{
    public void Configure(EntityTypeBuilder<VacancyActionEntry> b)
    {
        b.ToTable("VacancyActions");
        b.ConfigureBase();
        b.Property(a => a.Action).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ActorUserId).HasMaxLength(255); // NULL = the system (automatic closing)
        b.Property(a => a.ActorName).HasMaxLength(150).IsRequired();
        b.Property(a => a.Comments).HasMaxLength(2000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.VacancyId, a.ActionAt });
    }
}
