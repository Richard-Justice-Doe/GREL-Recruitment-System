using Grsms.Application.Recruitment;
using Grsms.Application.Vacancies;
using Grsms.Domain.Security;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;

namespace Grsms.Web.Helpers;

/// <summary>
/// The small numbers in the sidebar and the notification bell. Worked out at most once per page request
/// (the sidebar and the top bar both ask), and only for users who can open the list behind each number.
/// </summary>
public class NavCounts(
    IHttpContextAccessor http,
    IAuthorizationService auth,
    RecruitmentRequestService requests,
    VacancyService vacancies,
    Application.Interviews.InterviewService interviews,
    Application.Onboarding.OnboardingService onboarding)
{
    private Task<Counts>? counts;

    public Task<Counts> GetAsync() => counts ??= LoadAsync();

    private async Task<Counts> LoadAsync()
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true || user.IsInRole(Roles.Candidate)) return new Counts(null, null, null, null, null);

        var ct = http.HttpContext!.RequestAborted;
        int? awaitingMe = null, open = null, toApprove = null;

        if ((await auth.AuthorizeAsync(user, Policies.RecruitmentRequests)).Succeeded)
            awaitingMe = await requests.CountAwaitingMeAsync(ct);

        if ((await auth.AuthorizeAsync(user, Policies.Vacancies)).Succeeded)
        {
            open = (await vacancies.SearchAsync(new VacancyFilter { Tab = "open", PageSize = 1 }, ct)).TotalCount;
            if (user.IsInRole(Roles.HrManager))
                toApprove = (await vacancies.SearchAsync(new VacancyFilter { Tab = "pending", PageSize = 1 }, ct)).TotalCount;
        }
        // Phase 7: any staff member can be on a panel.
        var scorecards = await interviews.CountMyPendingScorecardsAsync(ct);
        // Phase 9: onboarding checklist items can be given to any staff member (e.g. IT).
        var onboardingTasks = await onboarding.CountMyTasksAsync(ct);
        return new Counts(awaitingMe, open, toApprove, scorecards, onboardingTasks);
    }

    /// <param name="RequestsAwaitingMe">Requests waiting for my decision (null: cannot see requests).</param>
    /// <param name="OpenVacancies">Published vacancies (null: cannot see vacancies).</param>
    /// <param name="VacanciesToApprove">Vacancies waiting for my approval (HR Manager only).</param>
    /// <param name="ScorecardsToDo">Interviews that have started and still need my scorecard.</param>
    /// <param name="OnboardingTasksToDo">Open onboarding checklist items assigned to me.</param>
    public record Counts(int? RequestsAwaitingMe, int? OpenVacancies, int? VacanciesToApprove, int? ScorecardsToDo, int? OnboardingTasksToDo)
    {
        /// <summary>Total for the notification bell.</summary>
        public int Notifications => (RequestsAwaitingMe ?? 0) + (VacanciesToApprove ?? 0) + (ScorecardsToDo ?? 0) + (OnboardingTasksToDo ?? 0);
    }
}
