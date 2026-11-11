using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Recruitment;
using Grsms.Application.Users;
using Grsms.Application.Vacancies;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Dashboard;

/// <summary>
/// Builds the staff home page (dashboard): KPI cards with 7-day charts, "My tasks", the vacancy closing
/// calendar, requests in progress and the newest candidates.
///
/// Every number follows the same visibility rules as the lists (see <see cref="AccessScope"/>), so a Head of
/// Department only ever counts their own departments' records.
/// </summary>
public class DashboardService(
    IRecruitmentRequestRepository requests,
    IVacancyRepository vacancies,
    Applications.IApplicationRepository applications,
    IRepository<Department> departments,
    IUserDirectory users,
    RecruitmentRequestService requestService,
    VacancyService vacancyService,
    Shortlisting.ShortlistingService shortlisting,
    Interviews.InterviewService interviews,
    Hiring.HiringService hiring,
    Onboarding.OnboardingService onboarding,
    Applications.AtsService ats,
    Probation.ProbationService probation,
    AccessScope scope)
{
    private const int TaskRows = 6, ProgressRows = 5, CandidateRows = 5;

    private static readonly RecruitmentRequestStatus[] InApproval =
        [.. RecruitmentWorkflow.DecisionStages];

    public async Task<DashboardDto> GetAsync(DashboardAccess access, DateOnly? month, DashboardFilter? filter = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var firstDay = today.AddDays(-6);                  // the 7 days shown as bars, today included
        var since = firstDay.AddDays(-7).ToDateTime(TimeOnly.MinValue); // ... plus the 7 before, for the trend

        var v = await scope.VisibilityAsync(ct);
        var all = v.SeeAll;
        var uid = v.UserId;
        var hod = v.HodDepartmentIds.ToList();

        // Optional Division / Department filter. It narrows the KPI cards, the in-progress list and the calendar;
        // "My tasks" always shows everything waiting for the user.
        var divId = filter?.DivisionId;
        var deptId = filter?.DepartmentId;

        var kpis = new List<KpiCardDto>();
        var tasks = new List<DashboardTaskDto>();
        var taskTotal = 0;

        // ------------------------------------------------------------ Recruitment requests
        List<RequestProgressDto>? inProgress = null;
        var inProgressTotal = 0;
        if (access.Requests)
        {
            var submittedTotal = await requests.CountAsync(r =>
                (all || r.RequestedByUserId == uid || hod.Contains(r.DepartmentId)) &&
                (divId == null || r.DivisionId == divId) && (deptId == null || r.DepartmentId == deptId) && r.DateRequested != null, ct);
            var approvedTotal = await requests.CountAsync(r =>
                (all || r.RequestedByUserId == uid || hod.Contains(r.DepartmentId)) &&
                (divId == null || r.DivisionId == divId) && (deptId == null || r.DepartmentId == deptId) && r.Status == RecruitmentRequestStatus.Approved, ct);
            var recent = await requests.ListAsync(r =>
                (all || r.RequestedByUserId == uid || hod.Contains(r.DepartmentId)) &&
                (divId == null || r.DivisionId == divId) && (deptId == null || r.DepartmentId == deptId) &&
                ((r.DateRequested != null && r.DateRequested >= since) || (r.ApprovedAt != null && r.ApprovedAt >= since)), ct);

            kpis.Add(Card(KpiKind.Requests, "Recruitment requests", "fa-file-signature", submittedTotal,
                recent.Where(r => r.DateRequested is not null).Select(r => r.DateRequested!.Value), firstDay));
            kpis.Add(Card(KpiKind.Approved, "Approved requests", "fa-circle-check", approvedTotal,
                recent.Where(r => r.ApprovedAt is not null).Select(r => r.ApprovedAt!.Value), firstDay));

            // Tasks: requests waiting for my decision ...
            var awaiting = await requestService.SearchAsync(new RecruitmentRequestFilter { AwaitingMe = true, PageSize = TaskRows }, ct);
            taskTotal += awaiting.TotalCount;
            tasks.AddRange(awaiting.Items.Select(r => new DashboardTaskDto(DashboardTaskTarget.Request, r.Id, r.PositionTitle,
                r.RequestNumber, "Review and decide", r.RequestedByName, r.Status, r.StatusChangedAt)));

            // ... and my own requests that are still drafts or were returned for changes.
            var mine = await requests.ListAsync(r => r.RequestedByUserId == uid &&
                (r.Status == RecruitmentRequestStatus.Draft || r.Status == RecruitmentRequestStatus.Returned), ct);
            taskTotal += mine.Count;
            tasks.AddRange(mine.Select(r => new DashboardTaskDto(DashboardTaskTarget.Request, r.Id, r.PositionTitle, r.RequestNumber,
                r.Status == RecruitmentRequestStatus.Returned ? "Make the changes and resubmit" : "Finish and submit",
                "You", r.Status, r.StatusChangedAt)));

            // Requests in progress (newest movement first), with a progress bar through the approval stages.
            var moving = (await requests.ListAsync(r =>
                    (all || r.RequestedByUserId == uid || hod.Contains(r.DepartmentId)) &&
                (divId == null || r.DivisionId == divId) && (deptId == null || r.DepartmentId == deptId) && InApproval.Contains(r.Status), ct))
                .OrderByDescending(r => r.StatusChangedAt).ToList();
            inProgressTotal = moving.Count;
            var top = moving.Take(ProgressRows).ToList();
            var names = await users.GetDisplayNamesAsync(top.Select(r => r.RequestedByUserId), ct);
            inProgress = top.Select(r => new RequestProgressDto(r.Id, r.RequestNumber, r.PositionTitle,
                names.GetValueOrDefault(r.RequestedByUserId, "Unknown"), r.Status, ProgressPercent(r.Status))).ToList();
        }

        // ------------------------------------------------------------ Vacancies
        List<ClosingDayDto>? closingDays = null;
        var shownMonth = month is { } m ? new DateOnly(m.Year, m.Month, 1) : new DateOnly(today.Year, today.Month, 1);
        if (access.Vacancies)
        {
            var openTotal = await vacancies.CountAsync(x =>
                (all || x.RecruiterUserId == uid || hod.Contains(x.DepartmentId)) &&
                (divId == null || x.DivisionId == divId) && (deptId == null || x.DepartmentId == deptId) && x.Status == VacancyStatus.Published, ct);
            var published = await vacancies.ListAsync(x =>
                (all || x.RecruiterUserId == uid || hod.Contains(x.DepartmentId)) &&
                (divId == null || x.DivisionId == divId) && (deptId == null || x.DepartmentId == deptId) && x.PublishedAt != null && x.PublishedAt >= since, ct);
            kpis.Add(Card(KpiKind.OpenVacancies, "Open vacancies", "fa-briefcase", openTotal,
                published.Select(x => x.PublishedAt!.Value), firstDay));

            // Tasks: vacancies to approve (HR Manager), approved requests needing a vacancy and my draft vacancies (recruiters).
            if (scope.IsInRole(Roles.HrManager))
            {
                var pending = await vacancyService.SearchAsync(new VacancyFilter { Tab = "pending", PageSize = TaskRows }, ct);
                taskTotal += pending.TotalCount;
                tasks.AddRange(pending.Items.Select(x => new DashboardTaskDto(DashboardTaskTarget.Vacancy, x.Id, x.JobTitle,
                    x.VacancyNumber, "Approve or return", x.RecruiterName, x.Status, x.StatusChangedAt)));
            }
            if (scope.IsInRole(Roles.RecruitmentOfficer))
            {
                var needVacancy = await vacancyService.GetApprovedRequestsWithoutVacancyAsync(ct);
                taskTotal += needVacancy.Count;
                tasks.AddRange(needVacancy.Select(r => new DashboardTaskDto(DashboardTaskTarget.NewVacancyForRequest, r.Id,
                    r.PositionTitle, r.RequestNumber, "Convert into a vacancy", r.AssignedRecruiterName ?? "Any Recruitment Officer",
                    RecruitmentRequestStatus.Approved, r.ApprovedAt ?? now)));

                var drafts = await vacancies.ListAsync(x => x.RecruiterUserId == uid && x.Status == VacancyStatus.Draft, ct);
                taskTotal += drafts.Count;
                tasks.AddRange(drafts.Select(x => new DashboardTaskDto(DashboardTaskTarget.Vacancy, x.Id, x.JobTitle,
                    x.VacancyNumber, "Finish and submit for approval", "You", x.Status, x.StatusChangedAt)));
            }

            // Closing-date calendar for the chosen month: open vacancies, plus ones that already closed that month.
            var start = shownMonth;
            var end = shownMonth.AddMonths(1);
            var closing = await vacancies.ListAsync(x =>
                (all || x.RecruiterUserId == uid || hod.Contains(x.DepartmentId)) &&
                (divId == null || x.DivisionId == divId) && (deptId == null || x.DepartmentId == deptId) &&
                x.ClosingDate >= start && x.ClosingDate < end &&
                (x.Status == VacancyStatus.Published || x.Status == VacancyStatus.Closed), ct);
            var deptIds = closing.Select(x => x.DepartmentId).Distinct().ToList();
            var deptNames = (await departments.ListAsync(d => deptIds.Contains(d.Id), ct)).ToDictionary(d => d.Id, d => d.Name);
            closingDays = closing
                .GroupBy(x => x.ClosingDate)
                .OrderBy(g => g.Key)
                .Select(g => new ClosingDayDto(g.Key, g.OrderBy(x => x.JobTitle)
                    .Select(x => new ClosingVacancyDto(x.Id, x.VacancyNumber, x.JobTitle, deptNames.GetValueOrDefault(x.DepartmentId, ""),
                        x.NumberOfPositions, x.Status == VacancyStatus.Closed))
                    .ToList()))
                .ToList();
        }

        // ------------------------------------------------------------ Shortlisting (Phase 6): HOD's view needed
        foreach (var t in await shortlisting.HodTasksAsync(ct))
        {
            taskTotal++;
            tasks.Add(new DashboardTaskDto(DashboardTaskTarget.Shortlist, t.VacancyId, t.JobTitle, t.VacancyNumber,
                $"Give your view on {t.Waiting} shortlist recommendation{(t.Waiting == 1 ? "" : "s")}", "Recruitment",
                Domain.Applications.ApplicationStatus.UnderReview, t.Since));
        }

        // ------------------------------------------------------------ Interviews (Phase 7): my scorecards to submit
        var toScore = await interviews.SearchAsync(new Interviews.InterviewFilter { Tab = "scorecards", PageSize = TaskRows }, ct);
        taskTotal += toScore.TotalCount;
        tasks.AddRange(toScore.Items.Select(i => new DashboardTaskDto(DashboardTaskTarget.Interview, i.Id, $"{EnumText.Label(i.Type)}: {i.ApplicantName}",
            i.VacancyNumber, "Submit your scorecard", "Recruitment", i.Status, i.StartsAt)));

        // Score validation (9 Nov 2026): every scorecard is in and the vacancy's Recruitment Officer checks them.
        var toValidate = await interviews.ListAwaitingMyValidationAsync(ct);
        taskTotal += toValidate.Count;
        tasks.AddRange(toValidate.Take(TaskRows).Select(i => new DashboardTaskDto(DashboardTaskTarget.InterviewValidation, i.Id,
            $"Validate the scores: {i.ApplicantName}", i.VacancyNumber, $"{EnumText.Label(i.Type)}, round {i.Round}: all {i.ScorecardsIn} scorecards are in",
            "Recruitment", i.Status, i.StartsAt)));

        // ------------------------------------------------------------ Hiring (Phase 8): recommendations waiting for my decision
        if (access.Vacancies)
        {
            var decisions = await hiring.AwaitingMyDecisionAsync(ct);
            taskTotal += decisions.Count;
            tasks.AddRange(decisions.Take(TaskRows).Select(h => new DashboardTaskDto(DashboardTaskTarget.Hiring, h.ApplicationId,
                $"Hire {h.ApplicantName}", h.Number, h.Status switch
                {
                    Domain.Hiring.HiringStatus.Draft => "Review the draft recommendation and submit it to the HOD",
                    Domain.Hiring.HiringStatus.AwaitingHod => "Endorse or not",
                    _ => "Approve or reject"
                },
                "Recruitment", h.Status, h.CreatedAt)));

            // 9 Nov 2026: interview scores validated, so the next step is the hiring recommendation (vacancy's recruiter).
            var toRecommend = await hiring.ListReadyToRecommendAsync(onlyMine: true, ct);
            taskTotal += toRecommend.Count;
            tasks.AddRange(toRecommend.Take(TaskRows).Select(r => new DashboardTaskDto(DashboardTaskTarget.HiringRecommendation, r.ApplicationId,
                $"Proceed with {r.ApplicantName}?", r.VacancyNumber,
                $"Interview scores validated{(r.InterviewAverage is decimal a ? $" ({a:0.0}/100)" : "")}: decide whether to start the hiring process",
                "Recruitment", Domain.Applications.ApplicationStatus.Interview, r.LastValidatedAt)));
        }

        // ------------------------------------------------------------ Onboarding (Phase 9): accepted offers to start (HR), my checklist items (anyone)
        var toStart = await onboarding.ToStartAsync(ct: ct);
        taskTotal += toStart.Count;
        tasks.AddRange(toStart.Take(TaskRows).Select(s => new DashboardTaskDto(DashboardTaskTarget.NewOnboarding, s.ApplicationId,
            $"Onboard {s.EmployeeName}", s.OfferNumber, $"Start onboarding (starts {s.StartDate:dd MMM})", "Recruitment",
            Domain.Applications.ApplicationStatus.Hired, s.AcceptedAt)));

        var myOnboarding = await onboarding.MyTasksAsync(ct);
        taskTotal += myOnboarding.Count;
        tasks.AddRange(myOnboarding.Take(TaskRows).Select(t => new DashboardTaskDto(DashboardTaskTarget.Onboarding, t.OnboardingId,
            $"{t.Title}: {t.EmployeeName}", t.Number, t.DueDate is null ? "Onboarding task" : $"Due {t.DueDate:dd MMM}", "Recruitment",
            Domain.Onboarding.OnboardingTaskStatus.Open, t.Since)));

        // ------------------------------------------------------------ Usability U1: the rest of the work waiting for someone
        var atsWaiting = await ats.AwaitingHrByVacancyAsync(ct);
        taskTotal += atsWaiting.Count;
        tasks.AddRange(atsWaiting.Take(TaskRows).Select(x => new DashboardTaskDto(DashboardTaskTarget.AtsConfirm, x.VacancyId, x.JobTitle,
            x.VacancyNumber, $"{x.Count} automatic rejection(s) to confirm", "Recruitment", Domain.Applications.ApplicationStatus.AutoRejected, x.Since)));

        var onboardingWork = await onboarding.MyWorkAsync(ct);
        taskTotal += onboardingWork.Count;
        tasks.AddRange(onboardingWork.Take(TaskRows).Select(w => new DashboardTaskDto(w.Kind switch
            {
                Onboarding.OnboardingWorkKind.IssueLetter => DashboardTaskTarget.OnboardingLetter,
                Onboarding.OnboardingWorkKind.VerifyDocuments => DashboardTaskTarget.OnboardingDocuments,
                Onboarding.OnboardingWorkKind.Activate => DashboardTaskTarget.OnboardingActivate,
                _ => DashboardTaskTarget.OnboardingInduction
            }, w.OnboardingId, w.EmployeeName, w.Number, w.Kind switch
            {
                Onboarding.OnboardingWorkKind.IssueLetter => "Issue the appointment letter",
                Onboarding.OnboardingWorkKind.VerifyDocuments => $"{w.Count} document(s) to verify",
                Onboarding.OnboardingWorkKind.Activate => "Ready for activation",
                _ => "Induction done: close the onboarding"
            }, "Recruitment", Domain.Onboarding.OnboardingTaskStatus.Open, w.Since)));

        var probationWork = await probation.MyWorkAsync(ct);
        taskTotal += probationWork.Count;
        tasks.AddRange(probationWork.Take(TaskRows).Select(w => new DashboardTaskDto(
            w.IsDecision ? DashboardTaskTarget.ProbationDecision : DashboardTaskTarget.ProbationStep, w.ProbationId, w.EmployeeName, w.Number,
            w.Due is { } due ? $"{w.Step}, due {due:dd MMM}" : w.Step, "Recruitment",
            w.IsDecision ? Domain.Probation.ProbationStatus.AwaitingHrDecision : Domain.Probation.ProbationStatus.InProgress, w.Since)));

        // ------------------------------------------------------------ Applications (Phase 5)
        if (access.Vacancies)
        {
            var appTotal = await applications.CountAsync(x =>
                (all || hod.Contains(x.DepartmentId)) &&
                (divId == null || x.DivisionId == divId) && (deptId == null || x.DepartmentId == deptId), ct);
            var recentApps = await applications.ListAsync(x =>
                (all || hod.Contains(x.DepartmentId)) &&
                (divId == null || x.DivisionId == divId) && (deptId == null || x.DepartmentId == deptId) && x.SubmittedAt >= since, ct);
            kpis.Add(Card(KpiKind.Applications, "Applications", "fa-inbox", appTotal, recentApps.Select(x => x.SubmittedAt), firstDay));
        }

        // ------------------------------------------------------------ Candidates
        IReadOnlyList<CandidateSummary>? latestCandidates = null;
        var candidateTotal = await users.CountCandidatesAsync(ct);
        var registered = await users.GetCandidateRegistrationDatesAsync(since, ct);
        kpis.Add(Card(KpiKind.Candidates, "Registered candidates", "fa-user-graduate", candidateTotal, registered, firstDay));
        if (access.Candidates)
            latestCandidates = await users.GetLatestCandidatesAsync(CandidateRows, ct);

        return new DashboardDto
        {
            Kpis = kpis,
            // Oldest first: the longer something has waited, the higher it sits.
            Tasks = tasks.OrderBy(t => t.Since).Take(TaskRows).ToList(),
            TaskTotal = taskTotal,
            ClosingDays = closingDays,
            Month = shownMonth,
            InProgress = inProgress,
            InProgressTotal = inProgressTotal,
            LatestCandidates = latestCandidates
        };
    }

    /// <summary>Counts events per day for the 7 days from <paramref name="firstDay"/>, and in the 7 days before.</summary>
    private static KpiCardDto Card(KpiKind kind, string title, string icon, int total, IEnumerable<DateTime> events, DateOnly firstDay)
    {
        var days = events.Select(DateOnly.FromDateTime).ToList();
        var last7 = Enumerable.Range(0, 7)
            .Select(i => firstDay.AddDays(i))
            .Select(d => new DayCount(d, days.Count(x => x == d)))
            .ToList();
        var previous = days.Count(d => d >= firstDay.AddDays(-7) && d < firstDay);
        return new KpiCardDto(kind, title, icon, total, last7, previous);
    }

    /// <summary>How far a request has come: submitted (Department Head review) 25%, HR 50%, MD 75%, approved 100%.</summary>
    public static int ProgressPercent(RecruitmentRequestStatus status) => status switch
    {
        RecruitmentRequestStatus.DepartmentHeadReview or RecruitmentRequestStatus.RecruitmentReview => 25,
        RecruitmentRequestStatus.HrApproval => 50,
        RecruitmentRequestStatus.MdApproval => 75,
        RecruitmentRequestStatus.Approved => 100,
        _ => 0
    };
}
