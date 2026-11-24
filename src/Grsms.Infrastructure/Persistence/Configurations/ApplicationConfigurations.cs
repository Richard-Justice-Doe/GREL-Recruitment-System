using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Organization;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 5 tables. Enums are stored as readable text.

/// <summary>Table "CandidateProfiles": one per account.</summary>
internal class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> b)
    {
        b.ToTable("CandidateProfiles");
        b.ConfigureBase();

        b.Property(p => p.UserId).HasMaxLength(255).IsRequired();
        b.HasIndex(p => p.UserId).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);

        b.Property(p => p.Phone).HasMaxLength(30);
        b.Property(p => p.Location).HasMaxLength(150);
        b.Property(p => p.HighestEducation).HasConversion<string>().HasMaxLength(30);
        b.Property(p => p.FieldOfStudy).HasMaxLength(200);
        b.Property(p => p.CurrentEmployer).HasMaxLength(150);
        b.Property(p => p.CurrentPosition).HasMaxLength(150);
        b.Property(p => p.Skills).HasMaxLength(3000);
        b.Property(p => p.Certifications).HasMaxLength(3000);
        b.Property(p => p.LinkedInUrl).HasMaxLength(300);
        b.Property(p => p.Summary).HasMaxLength(2000);
    }
}

/// <summary>Table "JobApplications".</summary>
internal class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.ToTable("JobApplications");
        b.ConfigureBase();

        b.Property(a => a.ApplicationNumber).HasMaxLength(40).IsRequired();
        b.HasIndex(a => a.ApplicationNumber).IsUnique();

        // ONE application per person per vacancy: enforced by the database as well as by the service.
        b.HasIndex(a => new { a.VacancyId, a.ApplicantUserId }).IsUnique();

        // Long free-text columns are TEXT: MySQL limits a row to 64 KB (4 bytes per character in utf8mb4), and TEXT
        // values are stored outside the row. Their lengths are still limited by the forms and services.
        b.Property(a => a.ApplicantUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.RequestingHodUserId).HasMaxLength(255).IsRequired();
        b.Property(a => a.Source).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.HighestEducation).HasConversion<string>().HasMaxLength(30);

        b.Property(a => a.FullName).HasMaxLength(150).IsRequired();
        b.Property(a => a.Email).HasMaxLength(256).IsRequired();
        b.Property(a => a.Phone).HasMaxLength(30).IsRequired();
        b.Property(a => a.Location).HasMaxLength(150);
        b.Property(a => a.FieldOfStudy).HasMaxLength(200);
        b.Property(a => a.CurrentEmployer).HasMaxLength(150);
        b.Property(a => a.CurrentPosition).HasMaxLength(150);
        b.Property(a => a.Skills).HasColumnType("text");
        b.Property(a => a.Certifications).HasColumnType("text");
        b.Property(a => a.LinkedInUrl).HasMaxLength(300);
        b.Property(a => a.CoverLetter).HasColumnType("text");
        b.Property(a => a.OutcomeReason).HasColumnType("text");

        // Phase 6: screening and shortlisting
        b.Property(a => a.ScreeningDetails).HasColumnType("text");
        b.Property(a => a.RecruiterRecommendation).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.HodRecommendation).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.RecruiterNote).HasColumnType("text");
        b.Property(a => a.HodNote).HasColumnType("text");
        b.Property(a => a.RecruiterRecommendedByUserId).HasMaxLength(255);
        b.Property(a => a.HodRecommendedByUserId).HasMaxLength(255);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.RecruiterRecommendedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.HodRecommendedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(a => a.Version).IsConcurrencyToken();

        // Phase 15: ATS. The CV text and what was read from it can be long, so they are MEDIUMTEXT (up to 16 MB).
        b.Property(a => a.AtsClassification).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.CvParsed).HasColumnType("mediumtext");
        b.Property(a => a.CvText).HasColumnType("mediumtext");
        b.Property(a => a.AtsDecision).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.AtsRejectionReason).HasColumnType("text");
        b.Property(a => a.AtsDecisionByUserId).HasMaxLength(255);
        b.Property(a => a.AtsDecisionNote).HasColumnType("text");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.AtsDecisionByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(a => a.KnockoutAnswers).WithOne().HasForeignKey(k => k.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.VacancyId, a.AtsDecision });   // "awaiting HR" lists

        b.HasOne<Vacancy>().WithMany().HasForeignKey(a => a.VacancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JobAdvert>().WithMany().HasForeignKey(a => a.JobAdvertId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(a => a.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ApplicantUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(a => a.Actions).WithOne().HasForeignKey(x => x.JobApplicationId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(a => new { a.VacancyId, a.Status });
        b.HasIndex(a => new { a.DivisionId, a.Status });   // reports by division
        b.HasIndex(a => new { a.DepartmentId, a.Status });
        b.HasIndex(a => a.ApplicantUserId);                 // "My applications"
        b.HasIndex(a => a.SubmittedAt);                     // reports by period
        b.HasIndex(a => new { a.VacancyId, a.ScreeningScore });   // the ranked shortlisting board
    }
}

/// <summary>Phase 15: table "JobApplicationKnockoutAnswers".</summary>
internal class KnockoutAnswerConfiguration : IEntityTypeConfiguration<KnockoutAnswer>
{
    public void Configure(EntityTypeBuilder<KnockoutAnswer> b)
    {
        b.ToTable("JobApplicationKnockoutAnswers");
        b.HasKey(k => k.Id);
        b.Property(k => k.Question).HasMaxLength(300).IsRequired();
    }
}

/// <summary>Phase 15: table "AtsSettings" (one row).</summary>
internal class AtsSettingsConfiguration : IEntityTypeConfiguration<AtsSettings>
{
    public void Configure(EntityTypeBuilder<AtsSettings> b)
    {
        b.ToTable("AtsSettings");
        b.ConfigureBase();
    }
}

/// <summary>Table "JobApplicationActions": an application's history.</summary>
internal class ApplicationActionConfiguration : IEntityTypeConfiguration<ApplicationActionEntry>
{
    public void Configure(EntityTypeBuilder<ApplicationActionEntry> b)
    {
        b.ToTable("JobApplicationActions");
        b.ConfigureBase();
        b.Property(a => a.Action).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ToStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(a => a.ActorUserId).HasMaxLength(255);
        b.Property(a => a.ActorName).HasMaxLength(150).IsRequired();
        b.Property(a => a.Comments).HasMaxLength(2000);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.JobApplicationId, a.ActionAt });
    }
}

/// <summary>12 November 2026 (docs/34): table "JobAlertSubscriptions", one opt-in job alert per person.</summary>
internal class JobAlertSubscriptionConfiguration : IEntityTypeConfiguration<Grsms.Domain.Applications.JobAlertSubscription>
{
    public void Configure(EntityTypeBuilder<Grsms.Domain.Applications.JobAlertSubscription> b)
    {
        b.ToTable("JobAlertSubscriptions");
        b.ConfigureBase();
        b.Property(s => s.UserId).HasMaxLength(255).IsRequired();
        b.HasIndex(s => s.UserId).IsUnique();
        b.Property(s => s.Keywords).HasMaxLength(300);
        b.HasOne<Grsms.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => s.IsActive);
    }
}
