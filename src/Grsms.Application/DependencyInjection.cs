using Grsms.Application.Common;
using Grsms.Application.Documents;
using Grsms.Application.Vacancies;
using Grsms.Application.Organization;
using Grsms.Application.Recruitment;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Application;

/// <summary>
/// Registers the Application layer's services with the dependency-injection container.
/// Called once from Program.cs: <c>builder.Services.AddApplication();</c>
///
/// "Scoped" means one instance per web request, which matches the database context's lifetime.
/// When a new module adds a service, register it here.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Master data (Phase 1)
        services.AddScoped<IDivisionService, DivisionService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<ISiteService, SiteService>();

        // Documents (Phase 2) - shared by every module that stores files
        services.AddScoped<AttachmentService>();

        // Shared helpers used by the workflow modules
        services.AddScoped<AccessScope>();
        services.AddScoped<WorkflowMailer>();

        // Recruitment requests (Phase 2)
        services.AddScoped<RecruitmentRequestService>();

        // Vacancies (Phase 3)
        services.AddScoped<VacancyService>();

        // Phase 4: job adverts
        services.AddScoped<Adverts.AdvertService>();

        // Phase 5: candidate profiles and applications
        services.AddScoped<Applications.CandidateProfileService>();
        services.AddScoped<Applications.MyNextStepService>(); // usability U2
        services.AddScoped<Applications.ApplicationService>();
        services.AddScoped<Applications.TalentPoolService>(); // docs/34: talent pool
        services.AddScoped<Applications.JobAlertService>(); // docs/34: opt-in job alerts

        // Phase 6: CV screening and shortlisting
        services.AddScoped<Shortlisting.ShortlistingService>();

        // Phase 15: the ATS screening engine (CV reading, scores, automatic rejection held for HR)
        services.AddScoped<Applications.AtsService>();

        // Phase 16: System settings and email templates
        services.AddScoped<Settings.SystemSettingsService>();
        services.AddScoped<Settings.LetterSettingsService>(); // letterhead and signature (12 Nov 2026)
        services.AddScoped<Ai.AiGateway>(); // docs/36: every AI call goes through it
        services.AddScoped<Ai.AiAdminService>();

        // Phase 7: interviews, assessments and scorecards
        services.AddScoped<Interviews.InterviewService>();

        // Phase 8: background checks, hiring recommendation and offers
        services.AddScoped<Hiring.HiringService>();

        // Phase 9: appointment letters and onboarding
        services.AddScoped<Onboarding.OnboardingService>();
        services.AddScoped<Onboarding.DocumentVerificationService>(); // Phase 18
        services.AddScoped<Onboarding.OnboardingProgressService>(); // Phase 27
        services.AddScoped<Onboarding.DocumentDeadlineService>(); // Phase 27 stage C: document deadline reminders and digest
        services.AddScoped<Onboarding.OnboardingDocumentTypeService>(); // Phase 27
        services.AddScoped<Onboarding.PersonalRecordService>(); // Phase 19
        services.AddScoped<Onboarding.InductionAreaService>(); // Phase 20
        services.AddScoped<Onboarding.PersonnelFileService>(); // Phase 20
        services.AddScoped<Probation.ProbationService>(); // Phase 21
        services.AddScoped<Workforce.WorkforceDashboardService>(); // Phase 21
        services.AddScoped<Workforce.WorkforceReportService>(); // Phase 21

        // Phase 28 (docs/30): the employee master record, job history and grades
        services.AddScoped<Employees.EmployeeLifecycle>();
        services.AddScoped<Employees.EmployeeService>();
        services.AddScoped<Employees.GradeService>();
        services.AddScoped<Employees.ContractAlertService>(); // docs/34: contract expiry alerts (daily job)

        // Phase 10: reminders and surveys (notifications themselves are stored by Infrastructure)
        services.AddScoped<Notifications.ReminderService>();
        services.AddScoped<Surveys.SurveyService>();

        // Phase 11: reports and the executive overview
        services.AddScoped<Reports.ReportService>();
        services.AddScoped<Reports.WorkflowTargetsReportService>(); // docs/35 R1

        // Home-page dashboard
        services.AddScoped<Dashboard.DashboardService>();

        return services;
    }
}
