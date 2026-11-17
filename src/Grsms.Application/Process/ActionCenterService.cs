using System.ComponentModel.DataAnnotations;
using Grsms.Application.Dashboard;
using Grsms.Application.Settings;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Applications;

namespace Grsms.Application.Process;

public enum ActionPriority
{
    [Display(Name = "High")] High,
    [Display(Name = "Medium")] Medium,
    [Display(Name = "Low")] Low
}

/// <summary>G2 (15 Nov 2026): when an action is due, for the Action Center's sections (red, amber, neutral).</summary>
public enum ActionTiming
{
    [Display(Name = "Overdue")] Overdue,
    [Display(Name = "Due today")] DueToday,
    [Display(Name = "Coming up")] Upcoming
}

/// <summary>One pending action with its priority, health against the target and the date it is expected by.</summary>
public sealed record ActionItem(DashboardTaskDto Task, ActionPriority Priority, ProcessHealth Health, int? TargetWorkingDays, DateOnly? DueDate, string Why)
{
    /// <summary>Overdue: past its target or expected date; due today: expected today; otherwise coming up.</summary>
    public ActionTiming TimingOn(DateOnly today) =>
        Health == ProcessHealth.Overdue || DueDate < today ? ActionTiming.Overdue : DueDate == today ? ActionTiming.DueToday : ActionTiming.Upcoming;
}

/// <summary>G2: something the person did recently (from the workflow histories), shown under "Completed".</summary>
public sealed record CompletedItem(string What, string Kind, string Controller, int Id, DateTime At);

public sealed record ActionCenterDto(IReadOnlyList<ActionItem> Items, IReadOnlyList<CompletedItem>? Completed = null)
{
    public IReadOnlyList<CompletedItem> Done => Completed ?? [];

    public int Count(ActionPriority p) => Items.Count(i => i.Priority == p);
}

/// <summary>
/// 13 November 2026 (docs/37 CR5, step C3): the Action Center: everything waiting for the signed-in staff member, in one list with a
/// priority. It is built from the same queries as "My tasks" on Home (DashboardService, without its per-kind limit), so both always
/// agree. Priority:
///   * High: overdue against its workflow target (System settings), or due today or earlier;
///   * Medium: past 75 % of its target, or due within 2 working days;
///   * Low: the rest.
/// A step without a target becomes Medium after 3 working days and High after 7, so nothing waits unseen.
/// </summary>
public class ActionCenterService(
    DashboardService dashboard,
    ISettingsStore settings,
    ICurrentUserService me,
    IRepository<RecruitmentRequestAction> requestActions,
    IRepository<VacancyActionEntry> vacancyActions,
    IRepository<AdvertActionEntry> advertActions,
    IRepository<ApplicationActionEntry> applicationActions,
    IRepository<OnboardingActionEntry> onboardingActions,
    IRepository<EmployeeDocumentEvent> documentEvents)
{
    public const int CompletedDays = 14, CompletedMax = 30;

    public const int NoTargetMedium = 3, NoTargetHigh = 7;

    /// <summary>The working-day target that applies to a task, from its kind and status (null when there is none).</summary>
    public string? TargetKeyFor(DashboardTaskDto t) => t.Target switch
    {
        DashboardTaskTarget.Request => t.Status switch
        {
            RecruitmentRequestStatus.DepartmentHeadReview => SettingCatalogue.TargetHodReview,
            RecruitmentRequestStatus.HrApproval or RecruitmentRequestStatus.RecruitmentReview => SettingCatalogue.TargetHrApproval,
            RecruitmentRequestStatus.MdApproval => SettingCatalogue.TargetMdApproval,
            _ => null
        },
        DashboardTaskTarget.Vacancy when t.Status is VacancyStatus.PendingApproval => SettingCatalogue.TargetVacancyApproval,
        DashboardTaskTarget.Advert when t.Status is AdvertStatus.AwaitingHrReview or AdvertStatus.AwaitingItApproval => SettingCatalogue.TargetAdvertReview,
        DashboardTaskTarget.OnboardingDocuments => SettingCatalogue.TargetDocumentVerification,
        _ => null
    };

    public ActionItem Rate(DashboardTaskDto t, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        // 17 Nov 2026: a request the MD has approved is the Recruitment Officer's next job, always high priority.
        if (t.Target == DashboardTaskTarget.NewVacancyForRequest)
            return new ActionItem(t, ActionPriority.High, ProcessHealth.None, null, null, "Approved by the Managing Director: create the vacancy");
        var key = TargetKeyFor(t);
        int? target = key is null ? null : settings.GetInt(key);
        var waited = OnboardingWorkflow.WorkingDaysBetween(DateOnly.FromDateTime(t.Since), today);
        if (target is { } days && days > 0)
        {
            var health = ProcessGuideService.HealthOf(t.Since, days, now);
            var due = OnboardingWorkflow.AddWorkingDays(DateOnly.FromDateTime(t.Since), days);
            var priority = health == ProcessHealth.Overdue || due <= today ? ActionPriority.High
                : health == ProcessHealth.Approaching || OnboardingWorkflow.WorkingDaysBetween(today, due) <= 2 ? ActionPriority.Medium
                : ActionPriority.Low;
            var why = health == ProcessHealth.Overdue ? $"Overdue: target {days} working day{(days == 1 ? "" : "s")}, waiting {waited}"
                : $"Target {days} working day{(days == 1 ? "" : "s")}, waiting {waited}";
            return new ActionItem(t, priority, health, days, due, why);
        }
        var p = waited >= NoTargetHigh ? ActionPriority.High : waited >= NoTargetMedium ? ActionPriority.Medium : ActionPriority.Low;
        return new ActionItem(t, p, ProcessHealth.None, null, null,
            waited == 0 ? "Since today" : $"Waiting {waited} working day{(waited == 1 ? "" : "s")}");
    }

    public async Task<ActionCenterDto> GetAsync(DashboardAccess access, CancellationToken ct = default)
    {
        var d = await dashboard.GetAsync(access, null, null, ct, taskLimit: 500);
        var now = DateTime.UtcNow;
        var items = d.Tasks.Select(t => Rate(t, now))
            .OrderBy(i => i.Priority).ThenBy(i => i.DueDate ?? DateOnly.MaxValue).ThenBy(i => i.Task.Since).ToList();
        return new ActionCenterDto(items, await CompletedAsync(now, ct));
    }

    /// <summary>
    /// G2: what the signed-in person did in the last <see cref="CompletedDays"/> days, from the workflow histories (requests,
    /// vacancies, adverts, applications, onboarding, new-hire documents), newest first. Six small queries, on this page only.
    /// </summary>
    public async Task<IReadOnlyList<CompletedItem>> CompletedAsync(DateTime now, CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return [];
        var from = now.AddDays(-CompletedDays);
        var list = new List<CompletedItem>();
        list.AddRange((await requestActions.ListAsync(a => a.ActorUserId == uid && a.ActionAt >= from, ct))
            .Select(a => new CompletedItem(EnumText.Label(a.Action), "Recruitment request", "RecruitmentRequests", a.RecruitmentRequestId, a.ActionAt)));
        list.AddRange((await vacancyActions.ListAsync(a => a.ActorUserId == uid && a.ActionAt >= from, ct))
            .Select(a => new CompletedItem(EnumText.Label(a.Action), "Vacancy", "Vacancies", a.VacancyId, a.ActionAt)));
        list.AddRange((await advertActions.ListAsync(a => a.ActorUserId == uid && a.ActionAt >= from, ct))
            .Select(a => new CompletedItem(EnumText.Label(a.Action), "Job advert", "Adverts", a.JobAdvertId, a.ActionAt)));
        list.AddRange((await applicationActions.ListAsync(a => a.ActorUserId == uid && a.ActionAt >= from, ct))
            .Select(a => new CompletedItem(EnumText.Label(a.Action), "Application", "Applications", a.JobApplicationId, a.ActionAt)));
        list.AddRange((await onboardingActions.ListAsync(a => a.ActorUserId == uid && a.ActionAt >= from, ct))
            .Select(a => new CompletedItem(EnumText.Label(a.Action), "Onboarding", "Onboarding", a.OnboardingRecordId, a.ActionAt)));
        list.AddRange((await documentEvents.ListAsync(a => a.ActorUserId == uid && a.ActionAt >= from, ct))
            .Select(a => new CompletedItem($"New-hire document: {EnumText.Label(a.Action)}", "Onboarding", "Onboarding", a.OnboardingRecordId, a.ActionAt)));
        return list.OrderByDescending(c => c.At).Take(CompletedMax).ToList();
    }
}
