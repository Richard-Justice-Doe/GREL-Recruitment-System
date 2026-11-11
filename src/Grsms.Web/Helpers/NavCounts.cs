using Microsoft.Extensions.Caching.Memory;
using Grsms.Application.Recruitment;
using Grsms.Application.Vacancies;
using Grsms.Domain.Security;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;

namespace Grsms.Web.Helpers;

/// <summary>
/// The small numbers in the sidebar and the notification bell. Worked out at most once per page request
/// (the sidebar and the top bar both ask), and only for users who can open the list behind each number.
///
/// docs/32 (11 November 2026): each user's numbers are kept for "Navigation:CountsCacheSeconds" (default 30; 0 = never
/// kept), which saves about 9 queries on every page. Any form a user posts clears their own numbers (Program.cs), so
/// their own actions show at once; other people's actions show within that time.
/// </summary>
public class NavCounts(
    IHttpContextAccessor http,
    IAuthorizationService auth,
    RecruitmentRequestService requests,
    VacancyService vacancies,
    Application.Interviews.InterviewService interviews,
    Application.Onboarding.OnboardingService onboarding,
    Application.Notifications.INotificationCenter notifications,
    Application.Common.Interfaces.ICurrentUserService me,
    Application.Applications.AtsService ats,
    IMemoryCache cache,
    IConfiguration config)
{
    private Task<Counts>? counts;

    public Task<Counts> GetAsync() => counts ??= CachedAsync();

    public static string CacheKey(string userId) => "grsms:nav-counts:" + userId;

    /// <summary>Clears a user's cached numbers (called for every form they post).</summary>
    public static void Forget(IMemoryCache cache, string? userId)
    {
        if (!string.IsNullOrEmpty(userId)) cache.Remove(CacheKey(userId));
    }

    /// <summary>
    /// The bell (unread notifications) is always counted fresh: opening a notification is a link, not a form, and the bell
    /// must drop at once. Only the other numbers are kept.
    /// </summary>
    private async Task<Counts> CachedAsync()
    {
        var seconds = config.GetValue("Navigation:CountsCacheSeconds", 30);
        if (seconds <= 0 || me.UserId is not { } uid || http.HttpContext?.User.Identity?.IsAuthenticated != true) return await LoadAsync();
        if (cache.TryGetValue(CacheKey(uid), out Counts? kept) && kept is not null)
            return kept with { UnreadNotifications = await notifications.CountUnreadAsync(uid, http.HttpContext!.RequestAborted) };
        var fresh = await LoadAsync();
        cache.Set(CacheKey(uid), fresh, TimeSpan.FromSeconds(seconds));
        return fresh;
    }

    private async Task<Counts> LoadAsync()
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true || me.UserId is null) return new Counts(null, null, null, null, null, 0, null);

        var ct = http.HttpContext!.RequestAborted;
        // Phase 10: the bell shows unread notifications, for everyone (candidates too).
        var unread = await notifications.CountUnreadAsync(me.UserId, ct);
        if (user.IsInRole(Roles.Candidate)) return new Counts(null, null, null, null, null, unread, null);

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
        // Score validation (9 Nov 2026): the same badge also counts sessions waiting for me to validate their scores.
        var scorecards = await interviews.CountMyPendingScorecardsAsync(ct) + await interviews.CountAwaitingMyValidationAsync(ct);
        // Phase 9: onboarding checklist items can be given to any staff member (e.g. IT).
        var onboardingTasks = await onboarding.CountMyTasksAsync(ct);
        // Phase 15: ATS rejections waiting for a reviewer to confirm or override.
        int? atsAwaiting = (await auth.AuthorizeAsync(user, Policies.Applications)).Succeeded ? await ats.AwaitingHrCountAsync(ct) : null;
        return new Counts(awaitingMe, open, toApprove, scorecards, onboardingTasks, unread, atsAwaiting);
    }

    /// <param name="RequestsAwaitingMe">Requests waiting for my decision (null: cannot see requests).</param>
    /// <param name="OpenVacancies">Published vacancies (null: cannot see vacancies).</param>
    /// <param name="VacanciesToApprove">Vacancies waiting for my approval (HR Manager only).</param>
    /// <param name="ScorecardsToDo">Interviews that have started and still need my scorecard, plus those waiting for me to validate.</param>
    /// <param name="OnboardingTasksToDo">Open onboarding checklist items assigned to me.</param>
    /// <param name="UnreadNotifications">Unread notifications (the bell).</param>
    /// <param name="AtsAwaitingHr">Phase 15: held ATS rejections I could decide (null: cannot see applications).</param>
    public record Counts(int? RequestsAwaitingMe, int? OpenVacancies, int? VacanciesToApprove, int? ScorecardsToDo, int? OnboardingTasksToDo,
        int UnreadNotifications, int? AtsAwaitingHr);
}
