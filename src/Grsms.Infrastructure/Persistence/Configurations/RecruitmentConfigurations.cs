using Grsms.Domain.Documents;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 2 tables. Enum columns are stored as readable text (e.g. 'HrApproval') via HasConversion<string>().

/// <summary>Table "RecruitmentRequests".</summary>
internal class RecruitmentRequestConfiguration : IEntityTypeConfiguration<RecruitmentRequest>
{
    public void Configure(EntityTypeBuilder<RecruitmentRequest> b)
    {
        b.ToTable("RecruitmentRequests");
        b.ConfigureBase();

        b.Property(r => r.RequestNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(r => r.RequestNumber).IsUnique();

        b.Property(r => r.PositionTitle).HasMaxLength(150).IsRequired();
        b.Property(r => r.ReplacedEmployee).HasMaxLength(150);
        b.Property(r => r.Justification).HasMaxLength(4000).IsRequired();
        b.Property(r => r.BudgetCode).HasMaxLength(50).IsRequired();
        b.Property(r => r.EstimatedAnnualCost).HasPrecision(18, 2);   // DECIMAL(18,2): exact money, no rounding errors

        b.Property(r => r.EmploymentType).HasConversion<string>().HasMaxLength(30);
        b.Property(r => r.VacancyReason).HasConversion<string>().HasMaxLength(30);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);

        // QF-81-11 fields (November 2026). Long free text is TEXT, stored outside the row (see docs/03-database.md).
        b.Property(r => r.StaffCategory).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.PurposeOther).HasMaxLength(200);
        b.Property(r => r.PostDescription).HasColumnType("text");
        b.Property(r => r.EducationLevel).HasConversion<string>().HasMaxLength(30);
        b.Property(r => r.QualificationCategory).HasMaxLength(200);
        b.Property(r => r.TrainingLevel).HasMaxLength(200);
        b.Property(r => r.CertificateRequired).HasMaxLength(500);
        b.Property(r => r.ProfessionalExperience).HasColumnType("text");
        b.Property(r => r.DrivingLicence).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.DrivingLicenceClass).HasMaxLength(50);
        b.Property(r => r.Remarks).HasColumnType("text");

        b.Property(r => r.RequestedByUserId).HasMaxLength(255).IsRequired();
        b.Property(r => r.AssignedRecruiterUserId).HasMaxLength(255);
        b.Property(r => r.BudgetValidatedByUserId).HasMaxLength(255);

        // Optimistic concurrency: UPDATE ... WHERE Version = <value when loaded>. See RecruitmentRequest.Version.
        b.Property(r => r.Version).IsConcurrencyToken();

        // Foreign keys. RESTRICT everywhere: master data and users cannot be hard-deleted while requests use them.
        b.HasOne<Division>().WithMany().HasForeignKey(r => r.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unit>().WithMany().HasForeignKey(r => r.UnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(r => r.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.AssignedRecruiterUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.BudgetValidatedByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(r => r.Actions)
            .WithOne(a => a.RecruitmentRequest)
            .HasForeignKey(a => a.RecruitmentRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for the list page filters and "Awaiting my action".
        b.HasIndex(r => r.Status);
        b.HasIndex(r => r.RequestedByUserId);
        b.HasIndex(r => new { r.DepartmentId, r.Status });
        b.HasIndex(r => new { r.DivisionId, r.Status }); // reports by division
    }
}

/// <summary>Table "RecruitmentRequestActions": the request's workflow history.</summary>
internal class RecruitmentRequestActionConfiguration : IEntityTypeConfiguration<RecruitmentRequestAction>
{
    public void Configure(EntityTypeBuilder<RecruitmentRequestAction> b)
    {
        b.ToTable("RecruitmentRequestActions");
        b.ConfigureBase();

        b.Property(a => a.Action).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ActorUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.ActorName).HasMaxLength(150).IsRequired();
        b.Property(a => a.Comments).HasMaxLength(2000);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.RecruitmentRequestId, a.ActionAt });
    }
}

/// <summary>Table "Attachments": descriptions of uploaded files, for every module.</summary>
internal class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("Attachments");
        b.ConfigureBase();

        b.Property(a => a.OwnerType).HasMaxLength(50).IsRequired();
        b.Property(a => a.Category).HasConversion<string>().HasMaxLength(40);
        b.Property(a => a.OriginalFileName).HasMaxLength(200).IsRequired();
        b.Property(a => a.StoredFileName).HasMaxLength(200).IsRequired();
        b.Property(a => a.ContentType).HasMaxLength(150).IsRequired();
        b.Property(a => a.Sha256).HasMaxLength(64).IsRequired();

        // No database foreign key for OwnerId, because it can point at different tables
        // (OwnerType says which). The owning module's service checks it instead.
        b.HasIndex(a => new { a.OwnerType, a.OwnerId });
        b.HasIndex(a => a.StoredFileName).IsUnique();
    }
}

/// <summary>Table "NumberSequences": the last number used for each reference-number series.</summary>
internal class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> b)
    {
        b.ToTable("NumberSequences");
        b.HasKey(s => s.Key);
        b.Property(s => s.Key).HasMaxLength(100);
    }
}
