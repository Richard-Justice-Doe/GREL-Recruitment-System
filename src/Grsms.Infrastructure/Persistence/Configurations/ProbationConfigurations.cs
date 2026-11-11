using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Probation;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 21 tables: probation and confirmation. Enums as readable text; long text as TEXT.

internal class ProbationRecordConfiguration : IEntityTypeConfiguration<ProbationRecord>
{
    public void Configure(EntityTypeBuilder<ProbationRecord> b)
    {
        b.ToTable("ProbationRecords");
        b.ConfigureBase();
        b.Ignore(p => p.FirstAssessmentDueDate);
        b.Ignore(p => p.InitialMeetingSigned);
        b.Property(p => p.Number).HasMaxLength(40).IsRequired();
        b.HasIndex(p => p.Number).IsUnique();
        b.Property(p => p.EmployeeUserId).HasMaxLength(255).IsRequired();
        b.Property(p => p.EmployeeName).HasMaxLength(200).IsRequired();
        b.Property(p => p.StaffNumber).HasMaxLength(30);
        b.Property(p => p.JobTitle).HasMaxLength(150).IsRequired();
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(p => p.HodUserId).HasMaxLength(255);
        b.Property(p => p.HrManagerUserId).HasMaxLength(255);
        b.Property(p => p.InitialMeetingNotes).HasColumnType("text");
        b.Property(p => p.EmployeeSignatureName).HasMaxLength(200);
        b.Property(p => p.HodSignatureName).HasMaxLength(200);
        b.Property(p => p.ExtensionReason).HasColumnType("text");
        b.Property(p => p.ExtensionImprovementPlan).HasColumnType("text");
        b.Property(p => p.Version).IsConcurrencyToken();

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.HodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.HrManagerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(p => p.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.OnboardingRecordId).IsUnique();
        b.HasIndex(p => new { p.Status, p.EndDate });
        b.HasIndex(p => new { p.DepartmentId, p.Status });
        b.HasIndex(p => p.EmployeeUserId);

        b.HasMany(p => p.Objectives).WithOne().HasForeignKey(x => x.ProbationRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.DevelopmentPlan).WithOne().HasForeignKey(x => x.ProbationRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Actions).WithOne().HasForeignKey(x => x.ProbationRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ProbationObjectiveConfiguration : IEntityTypeConfiguration<ProbationObjective>
{
    public void Configure(EntityTypeBuilder<ProbationObjective> b)
    {
        b.ToTable("ProbationObjectives");
        b.Property(x => x.Objective).HasMaxLength(500).IsRequired();
        b.Property(x => x.SuccessIndicators).HasMaxLength(1000).IsRequired();
    }
}

internal class ProbationDevelopmentPlanConfiguration : IEntityTypeConfiguration<ProbationDevelopmentPlan>
{
    public void Configure(EntityTypeBuilder<ProbationDevelopmentPlan> b)
    {
        b.ToTable("ProbationDevelopmentPlans");
        b.Property(x => x.TrainingNeed).HasMaxLength(500);
        b.Property(x => x.DevelopmentNeed).HasMaxLength(500);
        b.Property(x => x.Timeline).HasMaxLength(200);
    }
}

internal class ProbationAssessmentConfiguration : IEntityTypeConfiguration<ProbationAssessment>
{
    public void Configure(EntityTypeBuilder<ProbationAssessment> b)
    {
        b.ToTable("ProbationAssessments");
        b.ConfigureBase();
        b.Property(a => a.Kind).HasConversion<string>().HasMaxLength(10);
        foreach (var p in new[] { nameof(ProbationAssessment.ApplicationOfSkills), nameof(ProbationAssessment.QualityOfWork), nameof(ProbationAssessment.Productivity),
                     nameof(ProbationAssessment.Cooperation), nameof(ProbationAssessment.Dependability), nameof(ProbationAssessment.AbilityToSupervise) })
            b.Property(p).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.AssessorUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.Comments).HasColumnType("text").IsRequired();
        b.Property(a => a.ImprovementPlan).HasColumnType("text");
        b.Property(a => a.ExtensionReason).HasColumnType("text");
        b.Property(a => a.HrComments).HasColumnType("text");
        b.HasOne<ProbationRecord>().WithMany().HasForeignKey(a => a.ProbationRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.AssessorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.ProbationRecordId, a.Kind });
    }
}

internal class ProbationActionEntryConfiguration : IEntityTypeConfiguration<ProbationActionEntry>
{
    public void Configure(EntityTypeBuilder<ProbationActionEntry> b)
    {
        b.ToTable("ProbationActions");
        b.ConfigureBase();
        b.Property(a => a.Action).HasConversion<string>().HasMaxLength(40);
        b.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ActorUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.ActorName).HasMaxLength(200).IsRequired();
        b.Property(a => a.Comments).HasColumnType("text");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.ProbationRecordId, a.ActionAt });
    }
}

internal class ConfirmationRecordConfiguration : IEntityTypeConfiguration<ConfirmationRecord>
{
    public void Configure(EntityTypeBuilder<ConfirmationRecord> b)
    {
        b.ToTable("ConfirmationRecords");
        b.ConfigureBase();
        b.Property(c => c.EmployeeUserId).HasMaxLength(255).IsRequired();
        b.Property(c => c.LetterNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(c => c.LetterNumber).IsUnique();
        b.Property(c => c.ApprovedByUserId).HasMaxLength(255).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProbationRecord>().WithMany().HasForeignKey(c => c.ProbationRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonnelFileDocument>().WithMany().HasForeignKey(c => c.PersonnelFileDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => c.ProbationRecordId).IsUnique();
    }
}
