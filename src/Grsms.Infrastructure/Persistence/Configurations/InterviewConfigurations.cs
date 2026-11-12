using Grsms.Domain.Applications;
using Grsms.Domain.Interviews;
using Grsms.Domain.Organization;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 7 tables. Enums are stored as readable text; long free text is TEXT (see docs/03-database.md, "MySQL row size").

/// <summary>Table "Interviews": interviews and assessments.</summary>
internal class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> b)
    {
        b.ToTable("Interviews");
        b.ConfigureBase();

        b.Property(i => i.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.Mode).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.Location).HasMaxLength(500).IsRequired();
        b.Property(i => i.CandidateInstructions).HasColumnType("text");
        b.Property(i => i.StatusReason).HasColumnType("text");
        b.Property(i => i.RequestingHodUserId).HasMaxLength(255).IsRequired();
        b.Property(i => i.ScheduledByUserId).HasMaxLength(255).IsRequired();
        b.Property(i => i.ValidatedByUserId).HasMaxLength(255);   // score validation (9 Nov 2026)
        b.Property(i => i.ValidationNote).HasColumnType("text");
        b.Property(i => i.Version).IsConcurrencyToken();

        b.HasOne<JobApplication>().WithMany().HasForeignKey(i => i.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vacancy>().WithMany().HasForeignKey(i => i.VacancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(i => i.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(i => i.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.ScheduledByUserId).OnDelete(DeleteBehavior.Restrict);

        // Panel and criteria belong to the interview (replaced as a set while it is scheduled).
        b.HasMany(i => i.Panel).WithOne().HasForeignKey(p => p.InterviewId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(i => i.Criteria).WithOne().HasForeignKey(c => c.InterviewId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(i => i.Scorecards).WithOne().HasForeignKey(s => s.InterviewId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(i => new { i.JobApplicationId, i.Round }).IsUnique();
        b.HasIndex(i => new { i.VacancyId, i.Status });
        b.HasIndex(i => new { i.Status, i.StartsAt });     // upcoming sessions, pending scorecards
        b.HasIndex(i => new { i.DivisionId, i.Status });   // reports by division
        b.HasIndex(i => new { i.DepartmentId, i.Status });
    }
}

internal class InterviewPanelMemberConfiguration : IEntityTypeConfiguration<InterviewPanelMember>
{
    public void Configure(EntityTypeBuilder<InterviewPanelMember> b)
    {
        b.ToTable("InterviewPanelMembers");
        b.HasKey(p => p.Id);
        b.Property(p => p.UserId).HasMaxLength(255).IsRequired();
        b.Property(p => p.Attendance).HasConversion<string>().HasMaxLength(20);   // score validation (9 Nov 2026)
        b.Property(p => p.AttendanceNote).HasMaxLength(1000);
        b.Property(p => p.AttendanceRecordedByUserId).HasMaxLength(255);
        b.Property(p => p.Response).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PanelResponse.Pending);   // docs/35: RSVP
        b.Property(p => p.DeclineReason).HasMaxLength(500);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.InterviewId, p.UserId }).IsUnique();
        b.HasIndex(p => p.UserId); // "My interviews"
    }
}

internal class InterviewCriterionConfiguration : IEntityTypeConfiguration<InterviewCriterion>
{
    public void Configure(EntityTypeBuilder<InterviewCriterion> b)
    {
        b.ToTable("InterviewCriteria");
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).HasMaxLength(150).IsRequired();
    }
}

/// <summary>Table "Scorecards": one per panel member per interview.</summary>
internal class ScorecardConfiguration : IEntityTypeConfiguration<Scorecard>
{
    public void Configure(EntityTypeBuilder<Scorecard> b)
    {
        b.ToTable("Scorecards");
        b.ConfigureBase();
        b.Property(s => s.PanelUserId).HasMaxLength(255).IsRequired();
        b.Property(s => s.Recommendation).HasConversion<string>().HasMaxLength(30);
        b.Property(s => s.Strengths).HasColumnType("text");
        b.Property(s => s.Concerns).HasColumnType("text");
        b.Property(s => s.WeightedScore).HasPrecision(5, 1);
        b.Property(s => s.ReturnReason).HasColumnType("text");             // score validation (9 Nov 2026)
        b.Property(s => s.ReturnedByUserId).HasMaxLength(255);
        b.Ignore(s => s.IsValid);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.PanelUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(s => s.Ratings).WithOne().HasForeignKey(r => r.ScorecardId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(s => new { s.InterviewId, s.PanelUserId }).IsUnique();
    }
}

internal class ScorecardRatingConfiguration : IEntityTypeConfiguration<ScorecardRating>
{
    public void Configure(EntityTypeBuilder<ScorecardRating> b)
    {
        b.ToTable("ScorecardRatings");
        b.HasKey(r => r.Id);
        // RESTRICT (not cascade) from criteria: a criterion with ratings cannot disappear.
        b.HasOne<InterviewCriterion>().WithMany().HasForeignKey(r => r.CriterionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.ScorecardId, r.CriterionId }).IsUnique();
    }
}
