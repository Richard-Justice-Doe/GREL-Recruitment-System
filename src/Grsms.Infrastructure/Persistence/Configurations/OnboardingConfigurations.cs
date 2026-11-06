using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 9 tables. Enums are stored as readable text; long free text is TEXT (see docs/03-database.md, "MySQL row size").

/// <summary>Table "Onboardings": one per hired applicant, with the appointment letter and first-day details.</summary>
internal class OnboardingRecordConfiguration : IEntityTypeConfiguration<OnboardingRecord>
{
    public void Configure(EntityTypeBuilder<OnboardingRecord> b)
    {
        b.ToTable("Onboardings");
        b.ConfigureBase();
        b.Ignore(o => o.LetterIssued);
        b.Ignore(o => o.LetterAcknowledged);

        b.Property(o => o.Number).HasMaxLength(40).IsRequired();
        b.HasIndex(o => o.Number).IsUnique();
        b.Property(o => o.LetterNumber).HasMaxLength(40);
        b.HasIndex(o => o.LetterNumber).IsUnique();
        b.HasIndex(o => o.JobApplicationId).IsUnique();
        b.HasIndex(o => o.OfferId).IsUnique();

        b.Property(o => o.EmployeeName).HasMaxLength(200).IsRequired();
        b.Property(o => o.JobTitle).HasMaxLength(150).IsRequired();
        b.Property(o => o.MonthlySalary).HasPrecision(18, 2);
        b.Property(o => o.EmployeeNumber).HasMaxLength(30);
        b.Property(o => o.ReportLocation).HasMaxLength(300).IsRequired();
        b.Property(o => o.FirstDayNotes).HasColumnType("text");
        b.Property(o => o.LetterTerms).HasColumnType("text");
        b.Property(o => o.LetterAcknowledgedName).HasMaxLength(200);
        b.Property(o => o.LetterAcknowledgedIp).HasMaxLength(64);
        b.Property(o => o.CancelReason).HasColumnType("text");
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(40); // Phase 18: the 8 workflow stages
        b.Property(o => o.Notes).HasColumnType("text");
        b.HasMany(o => o.Actions).WithOne().HasForeignKey(a => a.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        foreach (var p in new[] { nameof(OnboardingRecord.RequestingHodUserId), nameof(OnboardingRecord.EmployeeUserId),
                     nameof(OnboardingRecord.ContactPersonUserId), nameof(OnboardingRecord.LetterIssuedByUserId), nameof(OnboardingRecord.CreatedByUserId) })
            b.Property(p).HasMaxLength(255);
        b.Property(o => o.Version).IsConcurrencyToken();

        b.HasOne<JobApplication>().WithMany().HasForeignKey(o => o.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Offer>().WithMany().HasForeignKey(o => o.OfferId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vacancy>().WithMany().HasForeignKey(o => o.VacancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(o => o.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(o => o.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(o => o.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.ContactPersonUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.LetterIssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(o => new { o.Status, o.StartDate });       // in progress, by start date
        b.HasIndex(o => new { o.DivisionId, o.Status });      // reports by division
        b.HasIndex(o => new { o.DepartmentId, o.Status });    // HOD's view
        b.HasIndex(o => o.EmployeeUserId);                    // the new employee's portal
    }
}

/// <summary>Table "OnboardingTasks": the checklist.</summary>
internal class OnboardingTaskConfiguration : IEntityTypeConfiguration<OnboardingTask>
{
    public void Configure(EntityTypeBuilder<OnboardingTask> b)
    {
        b.ToTable("OnboardingTasks");
        b.ConfigureBase();
        b.Property(t => t.Team).HasConversion<string>().HasMaxLength(30);
        b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.Title).HasMaxLength(300).IsRequired();
        b.Property(t => t.Notes).HasColumnType("text");
        b.Property(t => t.AssignedToUserId).HasMaxLength(255);
        b.Property(t => t.CompletedByUserId).HasMaxLength(255);

        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(t => t.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CompletedByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(t => new { t.OnboardingRecordId, t.SortOrder });
        b.HasIndex(t => new { t.AssignedToUserId, t.Status }); // "my onboarding tasks"
    }
}

/// <summary>Phase 18: table "OnboardingActions": the onboarding's workflow history.</summary>
internal class OnboardingActionEntryConfiguration : IEntityTypeConfiguration<OnboardingActionEntry>
{
    public void Configure(EntityTypeBuilder<OnboardingActionEntry> b)
    {
        b.ToTable("OnboardingActions");
        b.ConfigureBase();
        b.Property(a => a.Action).HasConversion<string>().HasMaxLength(40);
        b.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(40);
        b.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(40);
        b.Property(a => a.ActorUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.ActorName).HasMaxLength(200).IsRequired();
        b.Property(a => a.Comments).HasColumnType("text");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.OnboardingRecordId, a.ActionAt });
    }
}

/// <summary>
/// Phase 18: table "EmployeeDocuments": the New Hire Administrative Records checklist, one row per document type per
/// onboarding. The files are Attachments rows (OwnerType "EmployeeDocument").
/// </summary>
internal class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> b)
    {
        b.ToTable("EmployeeDocuments");
        b.ConfigureBase();
        b.Property(d => d.DocumentType).HasConversion<string>().HasMaxLength(40);
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.EmployeeUserId).HasMaxLength(255).IsRequired();
        b.Property(d => d.VerifiedByUserId).HasMaxLength(255);
        b.Property(d => d.NotApplicableReason).HasMaxLength(500);
        b.Property(d => d.RejectionReason).HasMaxLength(1000);

        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(d => d.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(d => new { d.OnboardingRecordId, d.DocumentType }).IsUnique(); // one row per document type
        b.HasIndex(d => d.Status);                                                 // "documents awaiting verification"
    }
}
