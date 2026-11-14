using System.ComponentModel.DataAnnotations;
using Grsms.Application.Dashboard;
using Grsms.Application.Settings;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Process;

public enum ActionPriority
{
    [Display(Name = "High")] High,
    [Display(Name = "Medium")] Medium,
    [Display(Name = "Low")] Low
}

/// <summary>One pending action with its priority, health against the target and the date it is expected by.</summary>
public sealed record ActionItem(DashboardTaskDto Task, ActionPriority Priority, ProcessHealth Health, int? TargetWorkingDays, DateOnly? DueDate, string Why);

public sealed record ActionCenterDto(IReadOnlyList<ActionItem> Items)
{
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
public class ActionCenterService(DashboardService dashboard, ISettingsStore settings)
{
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
        return new ActionCenterDto(items);
    }
}
