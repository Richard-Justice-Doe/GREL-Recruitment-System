using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Interviews;
using Grsms.Application.Settings;
using Grsms.Application.Users;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Interviews;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Process;

public enum StageState { Done, Current, Upcoming, Stopped }

/// <summary>Where the current step stands against its target (docs/35 workflow targets).</summary>
public enum ProcessHealth
{
    [Display(Name = "No target")] None,
    [Display(Name = "On track")] OnTrack,
    [Display(Name = "Approaching the target")] Approaching,
    [Display(Name = "Overdue")] Overdue
}

public sealed record ProcessStage(string Label, StageState State, DateTime? At = null);

public sealed record ProcessLastAction(string What, string Who, DateTime At);

/// <summary>
/// The answer to "where am I, what's next, who owns it, since when, is it late" for one record (docs/37 CR5).
/// <see cref="Owner"/> is the role or person who must act; <see cref="OwnerNames"/> the people, when known.
/// </summary>
public sealed record ProcessGuideDto(
    string Title,
    IReadOnlyList<ProcessStage> Stages,
    string NextAction,
    string Owner,
    IReadOnlyList<string> OwnerNames,
    string? NextApprover,
    DateTime? Since,
    int? TargetWorkingDays,
    ProcessHealth Health,
    DateOnly? DueDate,
    ProcessLastAction? LastAction,
    bool Finished = false,
    bool Stopped = false)
{
    /// <summary>"5 days 9 hours" since the step started.</summary>
    public string? PendingFor(DateTime now)
    {
        if (Since is not { } s || Finished || Stopped) return null;
        var span = now - s;
        if (span < TimeSpan.Zero) return null;
        var days = (int)span.TotalDays;
        return days > 0 ? $"{days} day{(days == 1 ? "" : "s")} {span.Hours} hour{(span.Hours == 1 ? "" : "s")}" : $"{span.Hours} hour{(span.Hours == 1 ? "" : "s")} {span.Minutes} min";
    }
}

/// <summary>
/// 13 November 2026 (docs/37 CR5, step C2): the process guide shown at the top of every record page. Two journeys:
///   * <b>Requisition</b> (request and vacancy pages): request submitted → HOD review → HR Manager review → MD approval →
///     vacancy prepared → vacancy approved → advertised → applications → selection → positions filled;
///   * <b>Candidate</b> (application, interview, hiring case and onboarding pages): applied → screening → interview →
///     hire approval → offer → onboarding → employee.
/// Everything is worked out from the records' existing statuses and histories: no workflow data of its own. For the current
/// step it names the next action, the owner (role and people), the next approver, how long it has been waiting, the target from
/// System settings → Workflow targets (green within 75 %, amber up to 100 %, red past it), the expected date and the last action.
/// Pages call it after their own visibility checks.
/// </summary>
public class ProcessGuideService(
    IRepository<RecruitmentRequest> requests,
    IRepository<RecruitmentRequestAction> requestActions,
    IRepository<Vacancy> vacancies,
    IRepository<VacancyActionEntry> vacancyActions,
    IRepository<JobAdvert> adverts,
    IRepository<JobApplication> applications,
    IRepository<ApplicationActionEntry> applicationActions,
    IInterviewRepository interviews,
    IRepository<HiringRecommendation> recommendations,
    IRepository<Offer> offers,
    IRepository<OnboardingRecord> onboardings,
    IRepository<Department> departments,
    IUserDirectory users,
    ISettingsStore settings)
{
    private const double Amber = 0.75;

    public static ProcessHealth HealthOf(DateTime? since, int? targetDays, DateTime now)
    {
        if (since is not { } s || targetDays is not { } t || t <= 0) return ProcessHealth.None;
        var used = OnboardingWorkflow.WorkingDaysBetween(DateOnly.FromDateTime(s), DateOnly.FromDateTime(now));
        return used > t ? ProcessHealth.Overdue : used >= t * Amber ? ProcessHealth.Approaching : ProcessHealth.OnTrack;
    }

    private static DateOnly? Due(DateTime? since, int? targetDays) =>
        since is { } s && targetDays is { } t && t > 0 ? OnboardingWorkflow.AddWorkingDays(DateOnly.FromDateTime(s), t) : null;

    private ProcessGuideDto Guide(string title, List<ProcessStage> stages, string next, string owner, IReadOnlyList<string> names, string? approver,
        DateTime? since, string? targetKey, ProcessLastAction? last, DateOnly? due = null, bool finished = false, bool stopped = false)
    {
        int? target = targetKey is null ? null : settings.GetInt(targetKey);
        return new ProcessGuideDto(title, stages, next, owner, names, approver, since, target, HealthOf(since, target, DateTime.UtcNow),
            due ?? Due(since, target), last, finished, stopped);
    }

    private async Task<IReadOnlyList<string>> NamesInRoleAsync(string role, CancellationToken ct) =>
        (await users.GetActiveUsersInRoleAsync(role, ct)).Select(u => u.Text).ToList();

    private async Task<IReadOnlyList<string>> NamesAsync(IEnumerable<string?> ids, CancellationToken ct)
    {
        var list = ids.OfType<string>().Where(i => i.Length > 0).Distinct().ToList();
        if (list.Count == 0) return [];
        var names = await users.GetDisplayNamesAsync(list, ct);
        return list.Select(i => names.GetValueOrDefault(i)).OfType<string>().ToList();
    }

    private static string Join(IReadOnlyList<string> names) => names.Count == 0 ? "" : string.Join(", ", names);

    // ===================================================================== Requisition journey

    public async Task<ProcessGuideDto?> ForRequestAsync(int requestId, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(requestId, ct);
        if (r is null) return null;
        var v = (await vacancies.ListAsync(x => x.RecruitmentRequestId == r.Id, ct)).OrderByDescending(x => x.Id).FirstOrDefault();
        return await RequisitionAsync(r, v, ct);
    }

    public async Task<ProcessGuideDto?> ForVacancyAsync(int vacancyId, CancellationToken ct = default)
    {
        var v = await vacancies.GetByIdAsync(vacancyId, ct);
        if (v is null) return null;
        var r = await requests.GetByIdAsync(v.RecruitmentRequestId, ct);
        return r is null ? null : await RequisitionAsync(r, v, ct);
    }

    private async Task<ProcessGuideDto> RequisitionAsync(RecruitmentRequest r, Vacancy? v, CancellationToken ct)
    {
        var rHistory = (await requestActions.ListAsync(a => a.RecruitmentRequestId == r.Id, ct)).OrderBy(a => a.ActionAt).ThenBy(a => a.Id).ToList();
        var vHistory = v is null ? [] : (await vacancyActions.ListAsync(a => a.VacancyId == v.Id, ct)).OrderBy(a => a.ActionAt).ThenBy(a => a.Id).ToList();
        var ads = v is null ? [] : (await adverts.ListAsync(a => a.VacancyId == v.Id, ct)).ToList();
        var filled = v is null ? 0 : await offers.CountAsync(o => o.VacancyId == v.Id && o.Status == OfferStatus.Accepted, ct);

        DateTime? Left(RecruitmentRequestStatus s) => rHistory.LastOrDefault(a => a.FromStatus == s && a.ToStatus != s)?.ActionAt;
        DateTime? VLeft(VacancyStatus s) => vHistory.LastOrDefault(a => a.FromStatus == s && a.ToStatus != s)?.ActionAt;

        int Order(RecruitmentRequestStatus s) => s switch
        {
            RecruitmentRequestStatus.Draft or RecruitmentRequestStatus.Returned => 0,
            RecruitmentRequestStatus.DepartmentHeadReview => 1,
            RecruitmentRequestStatus.RecruitmentReview or RecruitmentRequestStatus.HrApproval => 2,
            RecruitmentRequestStatus.MdApproval => 3,
            _ => 4
        };
        var stopped = r.Status is RecruitmentRequestStatus.Rejected or RecruitmentRequestStatus.Cancelled;
        var reqStep = stopped ? Order(rHistory.LastOrDefault()?.FromStatus ?? RecruitmentRequestStatus.Draft) : Order(r.Status);

        // Steps after the approved request: 5 vacancy prepared, 6 vacancy approved, 7 advertised, 8 applications, 9 selection, 10 filled.
        var liveAdvert = ads.Where(a => a.Status is AdvertStatus.Live or AdvertStatus.Closed).OrderBy(a => a.PublishedAt).FirstOrDefault();
        int step;
        if (reqStep < 4) step = reqStep;
        else if (v is null || v.Status == VacancyStatus.Draft) step = 4;
        else if (v.Status == VacancyStatus.PendingApproval) step = 5;
        else if (v.Status == VacancyStatus.Published && liveAdvert is null) step = 6;
        else if (v.Status == VacancyStatus.Published) step = 7;
        else if (filled < v.NumberOfPositions && v.Status != VacancyStatus.Archived) step = 8;
        else step = 9;
        var finished = v is not null && filled >= v.NumberOfPositions;

        string[] labels =
        [
            "Request submitted", "Head of Department review", "HR Manager review", "MD approval", "Vacancy prepared", "Vacancy approved",
            "Advertised", "Applications", "Selection and hiring", "Positions filled"
        ];
        DateTime?[] doneAt =
        [
            r.DateRequested, Left(RecruitmentRequestStatus.DepartmentHeadReview), Left(RecruitmentRequestStatus.HrApproval) ?? Left(RecruitmentRequestStatus.RecruitmentReview),
            r.ApprovedAt ?? Left(RecruitmentRequestStatus.MdApproval), VLeft(VacancyStatus.Draft), v?.PublishedAt, liveAdvert?.PublishedAt, v?.ClosedAt, null, null
        ];
        // Stage 0 is "request submitted": current while in draft or returned, done once submitted.
        var stages = labels.Select((label, i) => new ProcessStage(label,
            stopped && i == step ? StageState.Stopped
            : finished || i < step ? StageState.Done
            : i == step ? StageState.Current : StageState.Upcoming,
            finished || i < step ? doneAt[i] : null)).ToList();

        var last = rHistory.Select(a => new ProcessLastAction($"Request: {EnumText.Label(a.Action)}", a.ActorName, a.ActionAt))
            .Concat(vHistory.Select(a => new ProcessLastAction($"Vacancy: {EnumText.Label(a.Action)}", a.ActorName, a.ActionAt)))
            .MaxBy(a => a.At);
        var title = $"{r.RequestNumber}{(v is null ? "" : $" · {v.VacancyNumber}")} · {r.PositionTitle}";

        if (stopped)
            return Guide(title, stages, $"The request was {EnumText.Label(r.Status).ToLowerInvariant()}.", "No one", [], null, null, null, last, stopped: true);
        if (finished)
            return Guide(title, stages, "All positions are filled.", "No one", [], null, null, null, last, finished: true);

        var hods = await NamesAsync([(await departments.GetByIdAsync(r.DepartmentId, ct))?.HeadOfDepartmentUserId], ct);
        switch (step)
        {
            case 0:
                return Guide(title, stages, r.Status == RecruitmentRequestStatus.Returned ? "Correct the request and submit it again." : "Complete the request and submit it.",
                    "The requester", await NamesAsync([r.RequestedByUserId], ct), Join(hods) is { Length: > 0 } h ? $"Head of Department: {h}" : "Head of Department",
                    r.StatusChangedAt, null, last);
            case 1:
                return Guide(title, stages, "Waiting for the Head of Department's review.", "Head of Department", hods,
                    $"Head of Department{(hods.Count == 0 ? "" : $": {Join(hods)}")}", r.StatusChangedAt, SettingCatalogue.TargetHodReview, last);
            case 2:
            {
                var hrm = await NamesInRoleAsync(Roles.HrManager, ct);
                return Guide(title, stages, "Waiting for the HR Manager's approval.", "HR Manager", hrm, $"HR Manager{(hrm.Count == 0 ? "" : $": {Join(hrm)}")}",
                    r.StatusChangedAt, SettingCatalogue.TargetHrApproval, last);
            }
            case 3:
            {
                var md = await NamesInRoleAsync(Roles.ManagingDirector, ct);
                return Guide(title, stages, "Waiting for the Managing Director's approval.", "Managing Director", md,
                    $"Managing Director{(md.Count == 0 ? "" : $": {Join(md)}")}", r.StatusChangedAt, SettingCatalogue.TargetMdApproval, last);
            }
            case 4:
            {
                var ro = await NamesAsync([v?.RecruiterUserId ?? r.AssignedRecruiterUserId], ct);
                return Guide(title, stages, v is null ? "Convert the approved request into a vacancy." : "Complete the vacancy and submit it for approval.",
                    "Recruitment Officer", ro, "HR Manager (vacancy approval)", v?.StatusChangedAt ?? r.ApprovedAt ?? r.StatusChangedAt, null, last);
            }
            case 5:
            {
                var hrm = await NamesInRoleAsync(VacancyWorkflow.ApproverRole, ct);
                return Guide(title, stages, "Waiting for the HR Manager to approve the vacancy.", "HR Manager", hrm,
                    $"HR Manager{(hrm.Count == 0 ? "" : $": {Join(hrm)}")}", v!.StatusChangedAt, SettingCatalogue.TargetVacancyApproval, last);
            }
            case 6:
            {
                var inReview = ads.Where(a => a.Status is AdvertStatus.AwaitingHrReview or AdvertStatus.AwaitingItApproval).OrderByDescending(a => a.Id).FirstOrDefault();
                if (inReview?.Status == AdvertStatus.AwaitingHrReview)
                    return Guide(title, stages, $"Advert {inReview.AdvertNumber} is waiting for the HR Manager's review.", "HR Manager",
                        await NamesInRoleAsync(Roles.HrManager, ct), "HR Manager, then IT Manager", inReview.SubmittedAt, SettingCatalogue.TargetAdvertReview, last);
                if (inReview?.Status == AdvertStatus.AwaitingItApproval)
                    return Guide(title, stages, $"Advert {inReview.AdvertNumber} is waiting for the IT Manager's approval.", "IT Manager",
                        await NamesInRoleAsync(Roles.ItManager, ct), "IT Manager", inReview.HrReviewedAt ?? inReview.SubmittedAt, SettingCatalogue.TargetAdvertReview, last);
                return Guide(title, stages, ads.Any(a => a.Status == AdvertStatus.Scheduled) ? "An advert is scheduled; it goes live on its start date." : "Create the job advert and send it for review.",
                    "Recruitment Officer", await NamesAsync([v!.RecruiterUserId], ct), "HR Manager, then IT Manager", v.PublishedAt, null, last);
            }
            case 7:
                return Guide(title, stages, $"Receiving applications until {v!.ClosingDate:d MMMM yyyy}; screen them as they arrive.", "Recruitment Officer",
                    await NamesAsync([v.RecruiterUserId], ct), null, liveAdvert?.PublishedAt ?? v.PublishedAt, null, last, due: v.ClosingDate);
            default:
                return Guide(title, stages,
                    $"Selection: screening, interviews and hiring ({filled} of {v!.NumberOfPositions} position{(v.NumberOfPositions == 1 ? "" : "s")} filled).",
                    "Recruitment Officer", await NamesAsync([v.RecruiterUserId], ct), null, v.ClosedAt, null, last);
        }
    }

    // ===================================================================== Candidate journey

    public async Task<ProcessGuideDto?> ForApplicationAsync(int applicationId, CancellationToken ct = default)
    {
        var a = await applications.GetByIdAsync(applicationId, ct);
        if (a is null) return null;
        var v = await vacancies.GetByIdAsync(a.VacancyId, ct);
        var history = (await applicationActions.ListAsync(h => h.JobApplicationId == a.Id, ct)).OrderBy(h => h.ActionAt).ThenBy(h => h.Id).ToList();
        var sessions = (await interviews.ListForApplicationsAsync([a.Id], ct)).OrderBy(s => s.Round).ToList();
        var recs = (await recommendations.ListAsync(r => r.JobApplicationId == a.Id, ct)).OrderByDescending(r => r.Id).ToList();
        var rec = recs.FirstOrDefault(r => r.Status is not (HiringStatus.Withdrawn or HiringStatus.Rejected));
        var appOffers = (await offers.ListAsync(o => o.JobApplicationId == a.Id, ct)).OrderByDescending(o => o.Id).ToList();
        var offer = appOffers.FirstOrDefault(o => o.Status is not (OfferStatus.Withdrawn));
        var ob = (await onboardings.ListAsync(o => o.JobApplicationId == a.Id && o.Status != OnboardingStatus.Cancelled, ct)).OrderByDescending(o => o.Id).FirstOrDefault();
        var ro = await NamesAsync([v?.RecruiterUserId], ct);
        var title = $"{a.ApplicationNumber} · {a.FullName}{(v is null ? "" : $" · {v.JobTitle}")}";
        var last = history.Select(h => new ProcessLastAction(EnumText.Label(h.Action), h.ActorName, h.ActionAt)).LastOrDefault();
        DateTime? Reached(params ApplicationStatus[] s) => history.FirstOrDefault(h => s.Contains(h.ToStatus))?.ActionAt;

        var validated = sessions.Any(s => s.Status == InterviewStatus.Completed);
        int step = a.Status switch
        {
            ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.AutoRejected => 1,
            ApplicationStatus.Shortlisted => 2,
            ApplicationStatus.Interview when rec?.Status == HiringStatus.Approved => 4,
            ApplicationStatus.Interview when validated => 3,
            ApplicationStatus.Interview => 2,
            ApplicationStatus.Offer => 4,
            ApplicationStatus.Hired when ob?.Status == OnboardingStatus.Completed => 6,
            ApplicationStatus.Hired => 5,
            _ => -1
        };
        var stopped = step < 0;
        if (stopped)
            step = history.Select(h => h.FromStatus).LastOrDefault() switch
            {
                ApplicationStatus.Shortlisted or ApplicationStatus.Interview => 2,
                ApplicationStatus.Offer => 4,
                _ => 1
            };
        var finished = step == 6;

        string[] labels = ["Applied", "Screening", "Interview", "Hire approval", "Offer", "Onboarding", "Employee"];
        DateTime?[] doneAt =
        [
            a.SubmittedAt, Reached(ApplicationStatus.Shortlisted), sessions.Where(s => s.Status == InterviewStatus.Completed).Max(s => s.ValidatedAt),
            rec?.Status == HiringStatus.Approved ? rec.CreatedAt : null, offer?.Status == OfferStatus.Accepted ? offer.RespondedAt : null, ob?.CompletedAt, ob?.CompletedAt
        ];
        var stages = labels.Select((label, i) => new ProcessStage(label,
            stopped && i == step ? StageState.Stopped : i < step || finished ? StageState.Done : i == step ? StageState.Current : StageState.Upcoming,
            i < step || finished ? doneAt[i] : null)).ToList();

        if (stopped)
            return Guide(title, stages, $"The application ended: {EnumText.Label(a.Status).ToLowerInvariant()}.", "No one", [], null, null, null, last, stopped: true);
        if (finished)
            return Guide(title, stages, "Onboarding is complete: the candidate is now an employee.", "No one", [], null, null, null, last, finished: true);

        switch (step)
        {
            case 1:
                return a.AtsDecision == AtsDecision.AutoRejected
                    ? Guide(title, stages, "Confirm or override the automatic rejection.", "Recruitment Officer / HR", ro, null, a.StatusChangedAt, null, last)
                    : Guide(title, stages, "Screen the application and shortlist or decline it.", "Recruitment Officer", ro, null, a.SubmittedAt, null, last);
            case 2:
            {
                var next = sessions.LastOrDefault(s => s.Status is InterviewStatus.Scheduled or InterviewStatus.AwaitingValidation);
                if (next is null)
                    return Guide(title, stages, "Schedule the interview or assessment.", "Recruitment Officer", ro, null, a.StatusChangedAt, null, last);
                if (next.Status == InterviewStatus.AwaitingValidation)
                    return Guide(title, stages, "Validate the interview scores.", "Recruitment Officer", ro, "Recruitment Officer (score validation)",
                        next.Scorecards.Count == 0 ? next.StartsAt : next.Scorecards.Max(c => c.SubmittedAt), null, last);
                if (DateTime.UtcNow < next.StartsAt)
                    return Guide(title, stages, $"Interview on {next.StartsAt:dddd d MMMM yyyy, HH:mm} GMT at {next.Location}.", "Interview panel",
                        await NamesAsync(next.Panel.Select(p => p.UserId), ct), null, next.CreatedAt, null, last, due: DateOnly.FromDateTime(next.StartsAt));
                var missing = InterviewWorkflow.MissingScorers(next).Select(p => p.UserId);
                return Guide(title, stages, "Panel members to submit their scorecards.", "Interview panel", await NamesAsync(missing, ct),
                    "Recruitment Officer (score validation)", next.StartsAt, null, last, due: OnboardingWorkflow.AddWorkingDays(DateOnly.FromDateTime(next.StartsAt), 2));
            }
            case 3:
                return rec?.Status switch
                {
                    HiringStatus.AwaitingHod => Guide(title, stages, "Waiting for the Head of Department (earlier workflow).", "Head of Department",
                        await NamesAsync([(await departments.GetByIdAsync(a.DepartmentId, ct))?.HeadOfDepartmentUserId], ct), "Head of Department", rec.CreatedAt, null, last),
                    HiringStatus.AwaitingHr => Guide(title, stages, "Waiting for the HR Manager (earlier workflow).", "HR Manager", await NamesInRoleAsync(Roles.HrManager, ct),
                        "HR Manager", rec.HodDecidedAt ?? rec.CreatedAt, null, last),
                    HiringStatus.AwaitingMd => Guide(title, stages, "Waiting for the Managing Director (earlier workflow).", "Managing Director",
                        await NamesInRoleAsync(Roles.ManagingDirector, ct), "Managing Director", rec.HrDecidedAt ?? rec.CreatedAt, null, last),
                    HiringStatus.Draft => Guide(title, stages, "Check the background checks and approve the hire.", "Recruitment Officer", ro,
                        "Recruitment Officer (hire approval)", rec.CreatedAt, null, last),
                    _ => Guide(title, stages, "Decide whether to proceed with this candidate (hire approval).", "Recruitment Officer", ro,
                        "Recruitment Officer (hire approval)", sessions.Max(s => s.ValidatedAt), null, last)
                };
            case 4:
                if (offer is { Status: OfferStatus.Sent })
                    return Guide(title, stages, $"Waiting for the candidate to accept or decline the offer (reply by {offer.ExpiresOn:d MMMM yyyy}).", "Candidate",
                        [a.FullName], null, offer.SentAt, SettingCatalogue.TargetOfferAnswer, last, due: offer.ExpiresOn);
                return Guide(title, stages, offer is { Status: OfferStatus.Draft } ? "Check the drafted offer and send it." : "Prepare and send the offer.",
                    "Recruitment Officer", ro, null, rec?.CreatedAt, null, last);
            default:
                if (ob is null)
                    return Guide(title, stages, "Start the onboarding.", "Recruitment Officer", ro, null, offer?.RespondedAt, null, last);
                return ob.Status switch
                {
                    OnboardingStatus.Draft or OnboardingStatus.AwaitingEmployeeSubmission => Guide(title, stages,
                        "The new employee uploads the onboarding documents and signs the letter and personal record.", "New employee", [a.FullName],
                        "Recruitment Officer (document verification)", ob.CreatedAt, null, last, due: ob.DocumentsDueDate),
                    OnboardingStatus.AwaitingDocumentVerification => Guide(title, stages, "Verify the new employee's documents.", "Recruitment Officer", ro,
                        null, ob.StatusChangedAt, SettingCatalogue.TargetDocumentVerification, last),
                    OnboardingStatus.AwaitingActivation => Guide(title, stages, "Activate the new employee's staff account.", "Recruitment Officer / HR Manager", ro,
                        null, ob.StatusChangedAt, null, last),
                    _ => Guide(title, stages, "Complete the departmental induction.", "Induction owners", [], null, ob.StatusChangedAt, null, last)
                };
        }
    }
}
