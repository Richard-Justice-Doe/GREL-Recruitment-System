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
        services.AddScoped<Applications.ApplicationService>();

        // Phase 6: CV screening and shortlisting
        services.AddScoped<Shortlisting.ShortlistingService>();

        // Phase 7: interviews, assessments and scorecards
        services.AddScoped<Interviews.InterviewService>();

        // Phase 8: background checks, hiring recommendation and offers
        services.AddScoped<Hiring.HiringService>();

        // Phase 9: appointment letters and onboarding
        services.AddScoped<Onboarding.OnboardingService>();

        // Home-page dashboard
        services.AddScoped<Dashboard.DashboardService>();

        return services;
    }
}
