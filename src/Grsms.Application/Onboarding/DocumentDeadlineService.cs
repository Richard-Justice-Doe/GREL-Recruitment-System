using System.Globalization;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Notifications;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Onboarding;

/// <summary>
/// Phase 27 stage C (12 November 2026): the onboarding document deadline, run by the daily job on working days.
///   * While something is outstanding, the new employee is reminded every 3 working days after the onboarding started.
///   * On the due date: a last reminder, copied to the Recruitment Officer.
///   * The day after: the onboarding is overdue: the employee and the RO are told once (it is flagged on the pages).
///   * Each RO gets one daily digest of the documents waiting for their verification.
/// Each message has a key, so it goes out once however often the job runs. The employee may still start work with
/// documents outstanding; nothing is blocked.
/// </summary>
public class DocumentDeadlineService(
    IRepository<OnboardingRecord> onboardings,
    IRepository<Vacancy> vacancies,
    OnboardingProgressService progress,
    INotificationCenter notifications,
    WorkflowMailer mailer,
    IAppLinks links)
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    public record RunResult(int Reminders, int Overdue, int Digests);

    public async Task<RunResult> RunAsync(DateOnly today, CancellationToken ct = default)
    {
        if (today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return new(0, 0, 0);
        var open = (await onboardings.ListAsync(o => o.Status != OnboardingStatus.Cancelled && o.Status != OnboardingStatus.Completed, ct))
            .Where(o => OnboardingWorkflow.BeforeActivation(o.Status)).ToList();
        if (open.Count == 0) return new(0, 0, 0);
        var vacancyIds = open.Select(o => o.VacancyId).Distinct().ToList();
        var recruiters = (await vacancies.ListAsync(v => vacancyIds.Contains(v.Id), ct)).ToDictionary(v => v.Id, v => v.RecruiterUserId);
        string RecruitmentOfficer(OnboardingRecord o) =>
            recruiters.GetValueOrDefault(o.VacancyId) is { } r && r != o.EmployeeUserId ? r : o.CreatedByUserId;

        int reminders = 0, overdue = 0;
        var toVerify = new List<(OnboardingRecord Onboarding, int Count)>();
        foreach (var o in open)
        {
            var readiness = (await progress.SnapshotAsync(o, ct)).Readiness;
            if (readiness.AwaitingVerification > 0) toVerify.Add((o, readiness.AwaitingVerification));
            if (o.DocumentsDueDate is not { } due || readiness.ForEmployee.Count == 0) continue;

            var items = string.Concat(readiness.ForEmployee.Select(i => $"<li>{WorkflowMailer.Enc(i)}</li>"));
            var link = links.MyApplication(o.JobApplicationId);
            var ro = RecruitmentOfficer(o);
            if (today > due)
            {
                if (!await notifications.ClaimReminderAsync($"onb-docs-overdue:{o.Id}:{due:yyyyMMdd}", ct)) continue;
                await mailer.ToUserAsync(o.EmployeeUserId, "Your onboarding documents are overdue",
                    $"Your onboarding documents for <b>{WorkflowMailer.Enc(o.JobTitle)}</b> were due on {due.ToString("dddd d MMMM", Gb)}. Still needed:<ul>{items}</ul>" +
                    "Please upload them as soon as you can, or contact HR if you need more time.", link, "Upload my documents", ct);
                await mailer.ToUserAsync(ro, $"Onboarding {o.Number}: documents overdue",
                    $"<b>{WorkflowMailer.Enc(o.EmployeeName)}</b>'s documents were due on {due.ToString("d MMMM", Gb)}. Still outstanding:<ul>{items}</ul>" +
                    "You can extend the deadline once (up to 14 days) or follow up with them.", links.Onboarding(o.Id), "Open the onboarding", ct);
                overdue++;
            }
            else if (today == due)
            {
                if (!await notifications.ClaimReminderAsync($"onb-docs-last:{o.Id}:{due:yyyyMMdd}", ct)) continue;
                await mailer.ToUserAsync(o.EmployeeUserId, "Last day for your onboarding documents",
                    $"Today is the last day to provide your onboarding documents for <b>{WorkflowMailer.Enc(o.JobTitle)}</b>. Still needed:<ul>{items}</ul>",
                    link, "Upload my documents", ct);
                await mailer.ToUserAsync(ro, $"Onboarding {o.Number}: documents due today",
                    $"Copy of today's reminder to <b>{WorkflowMailer.Enc(o.EmployeeName)}</b>. Still outstanding:<ul>{items}</ul>", links.Onboarding(o.Id), "Open the onboarding", ct);
                reminders++;
            }
            else
            {
                var started = DateOnly.FromDateTime(o.CreatedAt);
                var days = OnboardingWorkflow.WorkingDaysBetween(started, today);
                if (days == 0 || days % OnboardingWorkflow.DocumentReminderEveryWorkingDays != 0) continue;
                if (!await notifications.ClaimReminderAsync($"onb-docs:{o.Id}:{today:yyyyMMdd}", ct)) continue;
                await mailer.ToUserAsync(o.EmployeeUserId, "Reminder: your onboarding documents",
                    $"Please provide your onboarding documents for <b>{WorkflowMailer.Enc(o.JobTitle)}</b> by <b>{due.ToString("dddd d MMMM", Gb)}</b>. " +
                    $"Still needed:<ul>{items}</ul>You can take photos with your phone.", link, "Upload my documents", ct);
                reminders++;
            }
        }

        // One digest per Recruitment Officer with everything waiting for their verification.
        var digests = 0;
        foreach (var group in toVerify.GroupBy(x => RecruitmentOfficer(x.Onboarding)))
        {
            if (!await notifications.ClaimReminderAsync($"onb-digest:{group.Key}:{today:yyyyMMdd}", ct)) continue;
            var list = string.Concat(group.OrderBy(x => x.Onboarding.StartDate).Select(x =>
                $"<li><b>{WorkflowMailer.Enc(x.Onboarding.EmployeeName)}</b> ({WorkflowMailer.Enc(x.Onboarding.Number)}, starts {x.Onboarding.StartDate.ToString("d MMM", Gb)}): " +
                $"{x.Count} document{(x.Count == 1 ? "" : "s")}</li>"));
            var total = group.Sum(x => x.Count);
            await mailer.ToUserAsync(group.Key, $"{total} onboarding document{(total == 1 ? "" : "s")} to verify",
                $"Documents waiting for your verification:<ul>{list}</ul>", links.Home(), "Open your to-dos", ct);
            digests++;
        }
        return new(reminders, overdue, digests);
    }
}
