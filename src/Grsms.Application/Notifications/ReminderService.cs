using System.Globalization;
using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Interviews;
using Grsms.Domain.Hiring;
using Grsms.Domain.Interviews;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Notifications;

/// <summary>
/// Automatic reminders (Module 10), sent by the background job by email and the bell (and SMS to applicants):
///
///   * Offer reply-by date is tomorrow               → the candidate (email + SMS).
///   * Interview or assessment tomorrow              → the applicant (email + SMS) and the panel.
///   * First day tomorrow                            → the new employee (email + SMS) and the person meeting them.
///   * Onboarding items due tomorrow or overdue      → each owner, one summary per day.
///   * Approvals waiting more than N days            → whoever must decide (requests, vacancies, hiring), then weekly.
///
/// Each reminder has a key (e.g. "offer-expiring:12") that is recorded when it is sent, so it never goes out twice,
/// however often the job runs.
/// </summary>
public class ReminderService(
    IRepository<Offer> offers,
    IRepository<Interview> interviews,
    IInterviewRepository interviewDetails,
    IApplicationRepository applications,
    IRepository<OnboardingRecord> onboardings,
    IRepository<OnboardingTask> onboardingTasks,
    IRepository<RecruitmentRequest> requests,
    IRepository<Vacancy> vacancies,
    IRepository<HiringRecommendation> recommendations,
    IRepository<Department> departments,
    INotificationCenter notifications,
    WorkflowMailer mailer,
    IAppLinks links)
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>Sends every reminder that is due. Returns how many reminders went out.</summary>
    /// <param name="nowUtc">The current time (GMT, Ghana's time).</param>
    /// <param name="approvalWaitingDays">An approval is "waiting too long" after this many days.</param>
    public async Task<int> RunAsync(DateTime nowUtc, int approvalWaitingDays = 3, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        var tomorrow = today.AddDays(1);
        var sent = 0;

        // ---------------------------------------------------------------- Offers: reply-by date tomorrow
        foreach (var o in await offers.ListAsync(o => o.Status == OfferStatus.Sent && o.ExpiresOn == tomorrow, ct))
        {
            if (!await notifications.ClaimReminderAsync($"offer-expiring:{o.Id}", ct)) continue;
            var app = await applications.GetByIdAsync(o.JobApplicationId, ct);
            if (app is null) continue;
            await mailer.ToUserAsync(app.ApplicantUserId, $"Reminder: please reply to your offer by {o.ExpiresOn.ToString("d MMMM", Gb)}",
                $"Your offer for the position of <b>{WorkflowMailer.Enc(o.JobTitle)}</b> is waiting for your reply. " +
                $"Please accept or decline it by <b>{o.ExpiresOn.ToString("dddd d MMMM yyyy", Gb)}</b>.",
                links.MyApplication(app.Id), "Read my offer", ct);
            await mailer.TextAsync(app.Phone,
                $"GREL: please reply to your offer for {o.JobTitle} by {o.ExpiresOn.ToString("d MMM", Gb)}. Sign in to GRSMS to accept or decline.", ct);
            sent++;
        }

        // ---------------------------------------------------------------- Interviews tomorrow
        var from = tomorrow.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);
        var due = await interviews.ListAsync(i => i.Status == InterviewStatus.Scheduled && i.StartsAt >= from && i.StartsAt < to, ct);
        if (due.Count > 0)
        {
            var panels = (await interviewDetails.ListForApplicationsAsync(due.Select(i => i.JobApplicationId).Distinct().ToList(), ct))
                .ToDictionary(i => i.Id, i => i.Panel.Select(p => p.UserId).ToList());
            foreach (var i in due)
            {
                if (!await notifications.ClaimReminderAsync($"interview:{i.Id}:{i.StartsAt:yyyyMMddHHmm}", ct)) continue;
                var app = await applications.GetByIdAsync(i.JobApplicationId, ct);
                if (app is null) continue;
                var when = $"{i.StartsAt.ToString("dddd d MMMM 'at' HH:mm", Gb)} GMT";
                var kind = EnumText.Label(i.Type).ToLowerInvariant();
                await mailer.ToUserAsync(app.ApplicantUserId, $"Reminder: your {kind} is tomorrow",
                    $"This is a reminder of your {kind} on <b>{when}</b>, {WorkflowMailer.Enc(i.Location)}.",
                    links.MyApplication(app.Id), "See the details", ct);
                await mailer.TextAsync(app.Phone, $"GREL: reminder of your {kind} on {i.StartsAt.ToString("ddd d MMM HH:mm", Gb)} GMT, {i.Location}.", ct);
                foreach (var member in panels.GetValueOrDefault(i.Id) ?? [])
                    await mailer.ToUserAsync(member, $"Reminder: {kind} with {app.FullName} tomorrow",
                        $"You are on the panel for <b>{WorkflowMailer.Enc(app.FullName)}</b> on <b>{when}</b>, {WorkflowMailer.Enc(i.Location)}.",
                        links.Interview(i.Id), "Open the session", ct);
                sent++;
            }
        }

        // ---------------------------------------------------------------- First day tomorrow
        foreach (var o in await onboardings.ListAsync(o => o.Status == OnboardingStatus.InProgress && o.StartDate == tomorrow, ct))
        {
            if (!await notifications.ClaimReminderAsync($"first-day:{o.Id}:{o.StartDate:yyyyMMdd}", ct)) continue;
            var app = await applications.GetByIdAsync(o.JobApplicationId, ct);
            await mailer.ToUserAsync(o.EmployeeUserId, "Welcome to GREL tomorrow!",
                $"We look forward to seeing you tomorrow, <b>{o.StartDate.ToString("dddd d MMMM", Gb)}</b>. Please report at <b>{o.ReportTime:HH:mm}</b> to " +
                $"{WorkflowMailer.Enc(o.ReportLocation)}, and remember the documents listed in your portal.",
                links.MyApplication(o.JobApplicationId), "See my first-day information", ct);
            await mailer.TextAsync(app?.Phone, $"GREL: welcome! Please report tomorrow at {o.ReportTime:HH:mm} to {o.ReportLocation}. Bring your Ghana Card and documents.", ct);
            await mailer.ToUserAsync(o.ContactPersonUserId, $"{o.EmployeeName} starts tomorrow",
                $"<b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}) starts tomorrow at {o.ReportTime:HH:mm}, " +
                $"{WorkflowMailer.Enc(o.ReportLocation)}. You are the person meeting them.", links.Onboarding(o.Id), "Open the onboarding", ct);
            sent++;
        }

        // ---------------------------------------------------------------- Onboarding items due tomorrow or overdue (one summary per owner per day)
        var active = (await onboardings.ListAsync(o => o.Status == OnboardingStatus.InProgress, ct)).ToDictionary(o => o.Id);
        if (active.Count > 0)
        {
            var ids = active.Keys.ToList();
            var open = await onboardingTasks.ListAsync(t => ids.Contains(t.OnboardingRecordId) && t.Status == OnboardingTaskStatus.Open &&
                                                            t.AssignedToUserId != null && t.DueDate != null && t.DueDate <= tomorrow, ct);
            foreach (var group in open.GroupBy(t => t.AssignedToUserId!))
            {
                if (!await notifications.ClaimReminderAsync($"onboarding-tasks:{group.Key}:{today:yyyyMMdd}", ct)) continue;
                var items = group.OrderBy(t => t.DueDate).ToList();
                await mailer.ToUserAsync(group.Key, $"Onboarding: {items.Count} task(s) due",
                    "These onboarding tasks are due tomorrow or overdue:<ul>" + string.Concat(items.Select(t =>
                        $"<li>{WorkflowMailer.Enc(t.Title)}: {WorkflowMailer.Enc(active[t.OnboardingRecordId].EmployeeName)}, " +
                        $"{(t.DueDate < today ? "<b>overdue</b> since" : "due")} {t.DueDate!.Value.ToString("d MMM", Gb)}</li>")) + "</ul>",
                    links.Onboarding(items[0].OnboardingRecordId), "Open the onboarding", ct);
                sent++;
            }
        }

        // ---------------------------------------------------------------- Approvals waiting too long (first after N days, then weekly)
        string? Block(DateTime since)
        {
            var age = (int)(nowUtc - since).TotalDays;
            return age < approvalWaitingDays ? null : ((age - approvalWaitingDays) / 7).ToString(CultureInfo.InvariantCulture);
        }

        foreach (var r in await requests.ListAsync(r => r.Status == RecruitmentRequestStatus.RecruitmentReview ||
                                                        r.Status == RecruitmentRequestStatus.HrApproval || r.Status == RecruitmentRequestStatus.MdApproval, ct))
        {
            if (Block(r.StatusChangedAt) is not { } block || !await notifications.ClaimReminderAsync($"waiting:request:{r.Id}:{r.Status}:{block}", ct)) continue;
            var role = r.Status switch
            {
                RecruitmentRequestStatus.RecruitmentReview => Roles.RecruitmentOfficer,
                RecruitmentRequestStatus.HrApproval => Roles.HrManager,
                _ => Roles.ManagingDirector
            };
            await mailer.ToRoleAsync(role, [r.RequestedByUserId], $"Waiting {(int)(nowUtc - r.StatusChangedAt).TotalDays} days: {r.RequestNumber}",
                $"Recruitment request <b>{WorkflowMailer.Enc(r.RequestNumber)}</b> ({WorkflowMailer.Enc(r.PositionTitle)}) has been waiting for " +
                $"<b>{EnumText.Label(r.Status)}</b> since {r.StatusChangedAt.ToString("d MMMM", Gb)}.", links.RecruitmentRequest(r.Id), "Open the request", ct);
            sent++;
        }

        foreach (var v in await vacancies.ListAsync(v => v.Status == VacancyStatus.PendingApproval, ct))
        {
            if (Block(v.StatusChangedAt) is not { } block || !await notifications.ClaimReminderAsync($"waiting:vacancy:{v.Id}:{block}", ct)) continue;
            await mailer.ToRoleAsync(Roles.HrManager, [v.RecruiterUserId], $"Waiting {(int)(nowUtc - v.StatusChangedAt).TotalDays} days: {v.VacancyNumber}",
                $"Vacancy <b>{WorkflowMailer.Enc(v.VacancyNumber)}</b> ({WorkflowMailer.Enc(v.JobTitle)}) has been waiting for HR approval since " +
                $"{v.StatusChangedAt.ToString("d MMMM", Gb)}.", links.Vacancy(v.Id), "Open the vacancy", ct);
            sent++;
        }

        foreach (var h in await recommendations.ListAsync(h => h.Status == HiringStatus.AwaitingHod || h.Status == HiringStatus.AwaitingHr ||
                                                              h.Status == HiringStatus.AwaitingMd, ct))
        {
            var since = h.Status switch
            {
                HiringStatus.AwaitingHod => h.CreatedAt,
                HiringStatus.AwaitingHr => h.HodDecidedAt ?? h.CreatedAt,
                _ => h.HrDecidedAt ?? h.CreatedAt
            };
            if (Block(since) is not { } block || !await notifications.ClaimReminderAsync($"waiting:hiring:{h.Id}:{h.Status}:{block}", ct)) continue;
            var subject = $"Waiting {(int)(nowUtc - since).TotalDays} days: {h.Number}";
            var text = $"Hiring recommendation <b>{WorkflowMailer.Enc(h.Number)}</b>: <b>{EnumText.Label(h.Status)}</b> since {since.ToString("d MMMM", Gb)}.";
            var link = links.HiringCase(h.JobApplicationId);
            if (h.Status == HiringStatus.AwaitingHod)
            {
                var hods = new[] { h.RequestingHodUserId, (await departments.GetByIdAsync(h.DepartmentId, ct))?.HeadOfDepartmentUserId }.OfType<string>().Distinct();
                foreach (var hod in hods) await mailer.ToUserAsync(hod, subject, text, link, "Open the hiring case", ct);
            }
            else
                await mailer.ToRoleAsync(h.Status == HiringStatus.AwaitingHr ? Roles.HrManager : Roles.ManagingDirector,
                    [h.CreatedByUserId, h.HrUserId], subject, text, link, "Open the hiring case", ct);
            sent++;
        }

        return sent;
    }
}
