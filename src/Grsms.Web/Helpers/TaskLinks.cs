using Grsms.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Helpers;

/// <summary>docs/37 CR5: the page a pending action opens, and its stage colour, label and button (as on Home's "My tasks").</summary>
public static class TaskLinks
{
    public static string? Url(IUrlHelper url, DashboardTaskDto t) => t.Target switch
    {
        DashboardTaskTarget.Request => url.Action("Details", "RecruitmentRequests", new { id = t.Id }),
        DashboardTaskTarget.Vacancy => url.Action("Details", "Vacancies", new { id = t.Id }),
        DashboardTaskTarget.Shortlist => url.Action("Vacancy", "Shortlisting", new { id = t.Id }),
        DashboardTaskTarget.Interview or DashboardTaskTarget.InterviewValidation => url.Action("Details", "Interviews", new { id = t.Id }),
        DashboardTaskTarget.Hiring or DashboardTaskTarget.HiringRecommendation => url.Action("Case", "Hiring", new { id = t.Id }),
        DashboardTaskTarget.Onboarding => url.Action("Details", "Onboarding", new { id = t.Id }),
        DashboardTaskTarget.NewOnboarding => url.Action("Start", "Onboarding", new { id = t.Id }),
        DashboardTaskTarget.AtsConfirm => url.Action("Vacancy", "Ats", new { id = t.Id }),
        DashboardTaskTarget.OnboardingLetter or DashboardTaskTarget.OnboardingDocuments or DashboardTaskTarget.OnboardingActivate
            or DashboardTaskTarget.OnboardingInduction => url.Action("Details", "Onboarding", new { id = t.Id }),
        DashboardTaskTarget.ProbationStep or DashboardTaskTarget.ProbationDecision => url.Action("Details", "Probation", new { id = t.Id }),
        DashboardTaskTarget.Advert => url.Action("Details", "Adverts", new { id = t.Id }),
        _ => url.Action("Create", "Vacancies", new { requestId = t.Id })
    };

    public static (string Tone, string Label, string Button) Look(DashboardTaskTarget target) => target switch
    {
        DashboardTaskTarget.Request => ("tone-hire", "Recruitment request", "Open request"),
        DashboardTaskTarget.Vacancy => ("tone-hire", "Vacancy", "Open vacancy"),
        DashboardTaskTarget.NewVacancyForRequest => ("tone-hire", "Vacancy", "Create the vacancy"),
        DashboardTaskTarget.Shortlist => ("tone-hire", "Shortlist", "Open shortlist"),
        DashboardTaskTarget.Interview => ("tone-interview", "Interview", "Open"),
        DashboardTaskTarget.InterviewValidation => ("tone-interview", "Score validation", "Check the scores"),
        DashboardTaskTarget.Hiring => ("tone-offer", "Hiring decision", "Open hiring case"),
        DashboardTaskTarget.HiringRecommendation => ("tone-offer", "Ready for hiring", "Decide"),
        DashboardTaskTarget.NewOnboarding => ("tone-onboard", "Onboarding", "Start onboarding"),
        DashboardTaskTarget.AtsConfirm => ("tone-hire", "Screening", "Confirm or overrule"),
        DashboardTaskTarget.OnboardingLetter => ("tone-onboard", "Appointment letter", "Issue the letter"),
        DashboardTaskTarget.OnboardingDocuments => ("tone-onboard", "Documents", "Verify the documents"),
        DashboardTaskTarget.OnboardingActivate => ("tone-onboard", "Ready for activation", "Activate"),
        DashboardTaskTarget.OnboardingInduction => ("tone-onboard", "Induction", "Close the onboarding"),
        DashboardTaskTarget.ProbationStep => ("tone-probation", "Probation", "Open the probation"),
        DashboardTaskTarget.ProbationDecision => ("tone-probation", "Probation decision", "Decide"),
        DashboardTaskTarget.Advert => ("tone-hire", "Job advert", "Open advert"),
        _ => ("tone-onboard", "Onboarding", "Open checklist")
    };
}
