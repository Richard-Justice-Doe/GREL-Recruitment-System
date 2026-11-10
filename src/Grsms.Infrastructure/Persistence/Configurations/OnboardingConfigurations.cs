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
        b.Property(o => o.CorrectionMessage).HasColumnType("text"); // Phase 27
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
        // Phase 20: departmental induction
        b.Property(t => t.Area).HasConversion<string>().HasMaxLength(40);
        b.Property(t => t.Description).HasColumnType("text");
        b.Property(t => t.VerifiedByUserId).HasMaxLength(255);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);

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
        // Phase 27: the type code (an OnboardingDocumentTypes.Code). The column keeps its Phase 18 name and values.
        b.Property(d => d.DocumentTypeCode).HasColumnName("DocumentType").HasMaxLength(40).IsRequired();
        b.Property(d => d.RejectionReasonCode).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.ReviewStartedByUserId).HasMaxLength(255);
        b.Property(d => d.Version).IsConcurrencyToken();
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.EmployeeUserId).HasMaxLength(255).IsRequired();
        b.Property(d => d.VerifiedByUserId).HasMaxLength(255);
        b.Property(d => d.NotApplicableReason).HasMaxLength(500);
        b.Property(d => d.RejectionReason).HasMaxLength(1000);

        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(d => d.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.EmployeeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(d => new { d.OnboardingRecordId, d.DocumentTypeCode }).IsUnique(); // one row per document type (never deleted: see IsApplicable)
        b.HasIndex(d => d.Status);                                                 // "documents awaiting verification"
    }
}

/// <summary>
/// Phase 27: table "OnboardingDocumentTypes": the configurable checklist, seeded with the default GREL list (fixed ids 1-25).
/// The HR Administrator changes it afterwards on the Onboarding documents screen.
/// </summary>
internal class OnboardingDocumentTypeConfiguration : IEntityTypeConfiguration<OnboardingDocumentType>
{
    public void Configure(EntityTypeBuilder<OnboardingDocumentType> b)
    {
        b.ToTable("OnboardingDocumentTypes");
        b.ConfigureBase();
        b.Property(t => t.Code).HasMaxLength(40).IsRequired();
        b.Property(t => t.Name).HasMaxLength(150).IsRequired();
        b.Property(t => t.Description).HasMaxLength(500);
        b.Property(t => t.Group).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.Requirement).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.Version).IsConcurrencyToken();
        b.HasOne<PersonnelFileCategory>().WithMany().HasForeignKey(t => t.PersonnelFileCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => t.Code).IsUnique();

        // Seed rows must never change between builds: fixed dates and versions.
        b.HasData(DefaultDocumentTypes.All.Select(t => new OnboardingDocumentType
        {
            Id = t.Id, Code = t.Code, Name = t.Name, Description = t.Description, Group = t.Group, Requirement = t.Requirement,
            AllowsWordFiles = t.AllowsWordFiles, MinFiles = t.MinFiles, PersonnelFileCategoryId = t.PersonnelFileCategoryId, SortOrder = t.SortOrder,
            IsActive = t.IsActive, IsLegacy = t.IsLegacy, CreatedAt = BaseEntityConfiguration.SeedDate, CreatedBy = "system",
            Version = new Guid($"00000000-0000-0000-0027-{t.Id:000000000000}")
        }));
    }
}

/// <summary>Phase 27: table "EmployeeDocumentEvents": the history of every new-hire document (never edited).</summary>
internal class EmployeeDocumentEventConfiguration : IEntityTypeConfiguration<EmployeeDocumentEvent>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentEvent> b)
    {
        b.ToTable("EmployeeDocumentEvents");
        b.ConfigureBase();
        b.Property(e => e.Action).HasConversion<string>().HasMaxLength(30);
        b.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.RejectionReason).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.FileName).HasMaxLength(200);
        b.Property(e => e.Comments).HasColumnType("text");
        b.Property(e => e.ActorUserId).HasMaxLength(255).IsRequired();
        b.Property(e => e.ActorName).HasMaxLength(200).IsRequired();
        b.Property(e => e.IpAddress).HasMaxLength(64);
        b.HasOne<EmployeeDocument>().WithMany().HasForeignKey(e => e.EmployeeDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(e => e.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.EmployeeDocumentId, e.ActionAt });
    }
}
