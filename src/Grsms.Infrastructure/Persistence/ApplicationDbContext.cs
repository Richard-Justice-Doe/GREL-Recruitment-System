using System.Linq.Expressions;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Auditing;
using Grsms.Domain.Common;
using Grsms.Domain.Documents;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core "database context": the single gateway between C# objects and MySQL.
///
/// * Each DbSet below is one table.
/// * Table/column details (lengths, indexes, foreign keys, seed rows) are NOT here; they are in the
///   Configurations folder, one file per table, so this class stays short.
/// * It inherits IdentityDbContext, which adds the sign-in tables (Users, Roles, UserRoles, ...).
/// * It implements <see cref="IUnitOfWork"/>, so services can save all their changes in one go.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options), IUnitOfWork
{
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Phase 2
    public DbSet<RecruitmentRequest> RecruitmentRequests => Set<RecruitmentRequest>();
    public DbSet<RecruitmentRequestAction> RecruitmentRequestActions => Set<RecruitmentRequestAction>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    // Phase 3
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<VacancyRequirement> VacancyRequirements => Set<VacancyRequirement>();
    public DbSet<VacancyActionEntry> VacancyActions => Set<VacancyActionEntry>();

    // Phase 4: job adverts
    public DbSet<Domain.Advertisements.JobAdvert> JobAdverts => Set<Domain.Advertisements.JobAdvert>();
    public DbSet<Domain.Advertisements.AdvertChannelPost> AdvertChannels => Set<Domain.Advertisements.AdvertChannelPost>();
    public DbSet<Domain.Advertisements.AdvertActionEntry> AdvertActions => Set<Domain.Advertisements.AdvertActionEntry>();

    // Phase 5: candidate profiles and applications
    public DbSet<Domain.Applications.CandidateProfile> CandidateProfiles => Set<Domain.Applications.CandidateProfile>();
    public DbSet<Domain.Applications.JobApplication> JobApplications => Set<Domain.Applications.JobApplication>();
    public DbSet<Domain.Applications.ApplicationActionEntry> ApplicationActions => Set<Domain.Applications.ApplicationActionEntry>();

    // Phase 7: interviews, assessments and scorecards
    public DbSet<Domain.Interviews.Interview> Interviews => Set<Domain.Interviews.Interview>();
    public DbSet<Domain.Interviews.InterviewPanelMember> InterviewPanelMembers => Set<Domain.Interviews.InterviewPanelMember>();
    public DbSet<Domain.Interviews.InterviewCriterion> InterviewCriteria => Set<Domain.Interviews.InterviewCriterion>();
    public DbSet<Domain.Interviews.Scorecard> Scorecards => Set<Domain.Interviews.Scorecard>();
    public DbSet<Domain.Interviews.ScorecardRating> ScorecardRatings => Set<Domain.Interviews.ScorecardRating>();

    // Phase 8: background checks, hiring recommendations and offers
    public DbSet<Domain.Hiring.BackgroundCheck> BackgroundChecks => Set<Domain.Hiring.BackgroundCheck>();
    public DbSet<Domain.Hiring.HiringRecommendation> HiringRecommendations => Set<Domain.Hiring.HiringRecommendation>();
    public DbSet<Domain.Hiring.Offer> Offers => Set<Domain.Hiring.Offer>();

    // Phase 9: appointment letters and onboarding
    public DbSet<Domain.Onboarding.OnboardingRecord> Onboardings => Set<Domain.Onboarding.OnboardingRecord>();
    public DbSet<Domain.Onboarding.OnboardingTask> OnboardingTasks => Set<Domain.Onboarding.OnboardingTask>();
    public DbSet<Domain.Onboarding.OnboardingActionEntry> OnboardingActions => Set<Domain.Onboarding.OnboardingActionEntry>();
    public DbSet<Domain.Onboarding.EmployeeDocument> EmployeeDocuments => Set<Domain.Onboarding.EmployeeDocument>();
    public DbSet<Domain.Onboarding.EmployeePersonalRecord> EmployeePersonalRecords => Set<Domain.Onboarding.EmployeePersonalRecord>();
    public DbSet<Domain.Onboarding.EmployeeDeclaration> EmployeeDeclarations => Set<Domain.Onboarding.EmployeeDeclaration>();
    public DbSet<Domain.Onboarding.InductionAreaSetting> InductionAreaSettings => Set<Domain.Onboarding.InductionAreaSetting>();
    public DbSet<Domain.Onboarding.PersonnelFileCategory> PersonnelFileCategories => Set<Domain.Onboarding.PersonnelFileCategory>();
    public DbSet<Domain.Onboarding.PersonnelFile> PersonnelFiles => Set<Domain.Onboarding.PersonnelFile>();
    public DbSet<Domain.Onboarding.PersonnelFileDocument> PersonnelFileDocuments => Set<Domain.Onboarding.PersonnelFileDocument>();

    // Phase 10: notifications, reminders and surveys
    public DbSet<Domain.Notifications.Notification> Notifications => Set<Domain.Notifications.Notification>();
    public DbSet<Domain.Notifications.SentReminder> SentReminders => Set<Domain.Notifications.SentReminder>();
    public DbSet<Domain.Surveys.SurveyInvitation> SurveyInvitations => Set<Domain.Surveys.SurveyInvitation>();
    public DbSet<Domain.Surveys.SurveyAnswer> SurveyAnswers => Set<Domain.Surveys.SurveyAnswer>();

    // Phase 14: permissions whose default grants have been applied (the grants themselves are in RoleClaims)
    public DbSet<Domain.Security.KnownPermission> KnownPermissions => Set<Domain.Security.KnownPermission>();

    // Phase 15: ATS
    public DbSet<Domain.Applications.AtsSettings> AtsSettings => Set<Domain.Applications.AtsSettings>();
    public DbSet<Domain.Applications.KnockoutAnswer> KnockoutAnswers => Set<Domain.Applications.KnockoutAnswer>();
    public DbSet<VacancyKnockoutQuestion> VacancyKnockoutQuestions => Set<VacancyKnockoutQuestion>();

    // Phase 16: system settings and email templates
    public DbSet<Domain.Settings.SystemSetting> SystemSettings => Set<Domain.Settings.SystemSetting>();
    public DbSet<Domain.Settings.EmailTemplate> EmailTemplates => Set<Domain.Settings.EmailTemplate>();

    /// <summary>
    /// Dates without a time (DateOnly: closing dates, start dates, ...) and times without a date (TimeOnly) are read and
    /// written through DateTime / TimeSpan. The Oracle MySQL provider hands back DATE and TIME columns as DateTime /
    /// TimeSpan and cannot turn them into DateOnly / TimeOnly itself ("Unable to cast DateTime to DateOnly", found on
    /// 4 November 2026). The column types stay DATE and TIME, so the database does not change.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        configurationBuilder.Properties<DateOnly?>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        configurationBuilder.Properties<TimeOnly>().HaveConversion<TimeOnlyConverter>().HaveColumnType("time");
        configurationBuilder.Properties<TimeOnly?>().HaveConversion<TimeOnlyConverter>().HaveColumnType("time");
    }

    private sealed class DateOnlyConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, DateTime>(
        d => d.ToDateTime(TimeOnly.MinValue), t => DateOnly.FromDateTime(t));

    private sealed class TimeOnlyConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TimeOnly, TimeSpan>(
        t => t.ToTimeSpan(), s => TimeOnly.FromTimeSpan(s));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity must configure its tables first; our configuration then renames/extends them.
        base.OnModelCreating(builder);

        // Load every IEntityTypeConfiguration class in this project (Persistence/Configurations/*.cs).
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // SOFT DELETE: for every entity that inherits BaseEntity, add the filter "WHERE IsDeleted = 0"
        // to every query automatically. Deleted rows are therefore invisible everywhere unless code
        // explicitly asks for them with .IgnoreQueryFilters().
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            var param = Expression.Parameter(entityType.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(param, nameof(BaseEntity.IsDeleted)),
                    Expression.Constant(false)),
                param);
            builder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    /// <summary>
    /// The unit-of-work save used by services. If another user changed the same record in the meantime
    /// (a concurrency token such as RecruitmentRequest.Version no longer matches), EF's own exception is
    /// translated into <see cref="ConcurrencyConflictException"/> so the Application layer can handle it
    /// without knowing about Entity Framework.
    /// </summary>
    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Forget the failed changes so the same request can continue cleanly.
            ChangeTracker.Clear();
            throw new ConcurrencyConflictException(ex);
        }
    }
}
