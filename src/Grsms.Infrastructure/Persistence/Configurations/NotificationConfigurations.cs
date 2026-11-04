using Grsms.Domain.Applications;
using Grsms.Domain.Notifications;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Surveys;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grsms.Infrastructure.Persistence.Configurations;

// Phase 10 tables.

/// <summary>Table "Notifications": the bell. Not soft-deleted (read ones are purged after a while).</summary>
internal class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(n => n.Id);
        b.Property(n => n.UserId).HasMaxLength(255).IsRequired();
        b.Property(n => n.Title).HasMaxLength(200).IsRequired();
        b.Property(n => n.Body).HasMaxLength(500);
        b.Property(n => n.Link).HasMaxLength(500);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(n => new { n.UserId, n.ReadAt });   // unread count
        b.HasIndex(n => new { n.UserId, n.Id });       // newest first
    }
}

/// <summary>Table "SentReminders": one row per reminder sent, so none is sent twice.</summary>
internal class SentReminderConfiguration : IEntityTypeConfiguration<SentReminder>
{
    public void Configure(EntityTypeBuilder<SentReminder> b)
    {
        b.ToTable("SentReminders");
        b.HasKey(r => r.Id);
        b.Property(r => r.Key).HasMaxLength(200).IsRequired();
        b.HasIndex(r => r.Key).IsUnique();
    }
}

/// <summary>Table "SurveyInvitations".</summary>
internal class SurveyInvitationConfiguration : IEntityTypeConfiguration<SurveyInvitation>
{
    public void Configure(EntityTypeBuilder<SurveyInvitation> b)
    {
        b.ToTable("SurveyInvitations");
        b.ConfigureBase();
        b.Property(s => s.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(s => s.Outcome).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.UserId).HasMaxLength(255).IsRequired();
        b.Property(s => s.RequestingHodUserId).HasMaxLength(255).IsRequired();

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JobApplication>().WithMany().HasForeignKey(s => s.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OnboardingRecord>().WithMany().HasForeignKey(s => s.OnboardingRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vacancy>().WithMany().HasForeignKey(s => s.VacancyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Division>().WithMany().HasForeignKey(s => s.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.RequestingHodUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(s => new { s.JobApplicationId, s.Type }).IsUnique(); // one invitation per survey per application
        b.HasIndex(s => new { s.UserId, s.CompletedAt });
        b.HasIndex(s => new { s.Type, s.DivisionId, s.DepartmentId }); // results by division / department
    }
}

/// <summary>Table "SurveyAnswers". Confidential: not in the audit trail.</summary>
internal class SurveyAnswerConfiguration : IEntityTypeConfiguration<SurveyAnswer>
{
    public void Configure(EntityTypeBuilder<SurveyAnswer> b)
    {
        b.ToTable("SurveyAnswers");
        b.ConfigureBase();
        b.Property(a => a.QuestionKey).HasMaxLength(30).IsRequired();
        b.Property(a => a.Text).HasColumnType("text");
        b.HasOne<SurveyInvitation>().WithMany().HasForeignKey(a => a.SurveyInvitationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.SurveyInvitationId, a.QuestionKey }).IsUnique();
    }
}
