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
/// Builds the staff home page (dashboard): action cards (docs/32), "My tasks", the vacancy closing
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
    Adverts.AdvertService advertService,
    AccessScope scope,
    IRepository<Domain.Hiring.Offer> offers)
{
    private const int TaskRows = 6, ProgressRows = 5, CandidateRows = 5;

    /// <summary>Roles that decide recruitment requests at some stage: they always see the "awaiting your approval" card.</summary>
    private static readonly string[] RequestApprovers = [Roles.HeadOfDepartment, Roles.RecruitmentOfficer, Roles.HrManager, Roles.ManagingDirector];

    /// <summary>Roles that run recruitment day to day (the same list as the menu's, Web/Helpers/MenuRules.cs).</summary>
    private static readonly string[] RecruitmentRoles =
        [Roles.RecruitmentOfficer, Roles.HrManager, Roles.HrAdministrator, Roles.Admin, Roles.SuperAdmin, Roles.ManagingDirector];

    private static readonly RecruitmentRequestStatus[] InApproval =
        [.. RecruitmentWorkflow.DecisionStages];

    /// <param name="taskLimit">How many "My tasks" rows of each kind (Home shows a few; the Action Center, docs/37 CR5, asks for all).</param>
    public async Task<DashboardDto> GetAsync(DashboardAccess access, DateOnly? month, DashboardFilter? filter = null, CancellationToken ct = default,
        int taskLimit = TaskRows)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var firstDay = today.AddDays(-6);                  // "this week": the last 7 days, today included
        var v = await scope.VisibilityAsync(ct);
        // A Head of Department with no recruitment role (they don't follow offers; as in the menu).
        var hodOnlyView = scope.IsInRole(Roles.HeadOfDepartment) && !RecruitmentRoles.Any(scope.IsInRole);
        var all = v.SeeAll;
        var uid = v.UserId;
        var hod = v.HodDepartmentIds.ToList();

        // Optional Division / Department filter. It narrows the KPI cards, the in-progress list and the calendar;
        // "My tasks" always shows everything waiting for the user.
        var divId = filter?.DivisionId;
        var deptId = filter?.DepartmentId;

        // docs/32: action cards (they replace the KPI row with 7-day charts). Cards about work waiting for the user are not
        // filtered (like "My tasks"); the others follow the Division / Department filter.
        var cards = new List<ActionCardDto>();
        var tasks = new List<DashboardTaskDto>();
        var taskTotal = 0;
        Dictionary<string, string> Route(string? tab = null, bool filtered = false)
        {
            var d = new Dictionary<string, string>();
            if (tab is not null) d["tab"] = tab;
            if (filtered && divId is { } dv) d["DivisionId"] = dv.ToString();
            if (filtered && deptId is { } dp) d["DepartmentId"] = dp.ToString();
            return d;
        }

        // ------------------------------------------------------------ Recruitment requests
        List<RequestProgressDto>? inProgress = null;
        var inProgressTotal = 0;
        if (access.Requests)
        {
            // Tasks: requests waiting for my decision ...
            var awaiting = await requestService.SearchAsync(new RecruitmentRequestFilter { AwaitingMe = true, PageSize = taskLimit }, ct);
            taskTotal += awaiting.TotalCount;
            if (awaiting.TotalCount > 0 || RequestApprovers.Any(scope.IsInRole))
                cards.Add(new ActionCardDto("Requests awaiting your approval", "fa-file-signature", awaiting.TotalCount,
                    awaiting.TotalCount == 0 ? "Nothing waiting for you" : "Waiting for your decision", "tone-hire", "RecruitmentRequests", "Index",
                    Route("awaiting"), true));
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
            // Tasks: vacancies to approve (HR Manager), approved requests needing a vacancy and my draft vacancies (recruiters).
            if (scope.IsInRole(Roles.HrManager))
            {
                var pending = await vacancyService.SearchAsync(new VacancyFilter { Tab = "pending", PageSize = taskLimit }, ct);
                taskTotal += pending.TotalCount;
                cards.Add(new ActionCardDto("Vacancies awaiting publication", "fa-briefcase", pending.TotalCount,
                    pending.TotalCount == 0 ? "Nothing waiting for you" : "Approve or return", "tone-probation", "Vacancies", "Index", Route("pending"), true));
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
        var toScore = await interviews.SearchAsync(new Interviews.InterviewFilter { Tab = "scorecards", PageSize = taskLimit }, ct);
        taskTotal += toScore.TotalCount;
        tasks.AddRange(toScore.Items.Select(i => new DashboardTaskDto(DashboardTaskTarget.Interview, i.Id, $"{EnumText.Label(i.Type)}: {i.ApplicantName}",
            i.VacancyNumber, "Submit your scorecard", "Recruitment", i.Status, i.StartsAt)));

        // docs/35: panel invitations waiting for my reply.
        var replies = await interviews.ListAwaitingMyReplyAsync(ct);
        taskTotal += replies.Count;
        tasks.AddRange(replies.Take(taskLimit).Select(i => new DashboardTaskDto(DashboardTaskTarget.Interview, i.Id,
            $"Panel invitation: {EnumText.Label(i.Type)}", i.StartsAt.ToString("dd MMM HH:mm"), "Accept or decline", "Recruitment", i.Status, i.CreatedAt)));

        // Score validation (9 Nov 2026): every scorecard is in and the vacancy's Recruitment Officer checks them.
        var toValidate = await interviews.ListAwaitingMyValidationAsync(ct);
        taskTotal += toValidate.Count;
        tasks.AddRange(toValidate.Take(taskLimit).Select(i => new DashboardTaskDto(DashboardTaskTarget.InterviewValidation, i.Id,
            $"Validate the scores: {i.ApplicantName}", i.VacancyNumber, $"{EnumText.Label(i.Type)}, round {i.Round}: all {i.ScorecardsIn} scorecards are in",
            "Recruitment", i.Status, i.StartsAt)));

        // ------------------------------------------------------------ Hiring (Phase 8): recommendations waiting for my decision
        if (access.Vacancies)
        {
            var decisions = await hiring.AwaitingMyDecisionAsync(ct);
            taskTotal += decisions.Count;
            tasks.AddRange(decisions.Take(taskLimit).Select(h => new DashboardTaskDto(DashboardTaskTarget.Hiring, h.ApplicationId,
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
            tasks.AddRange(toRecommend.Take(taskLimit).Select(r => new DashboardTaskDto(DashboardTaskTarget.HiringRecommendation, r.ApplicationId,
                $"Proceed with {r.ApplicantName}?", r.VacancyNumber,
                $"Interview scores validated{(r.InterviewAverage is decimal a ? $" ({a:0.0}/100)" : "")}: decide whether to start the hiring process",
                "Recruitment", Domain.Applications.ApplicationStatus.Interview, r.LastValidatedAt)));
        }

        // ------------------------------------------------------------ Onboarding (Phase 9): accepted offers to start (HR), my checklist items (anyone)
        var toStart = await onboarding.ToStartAsync(ct: ct);
        taskTotal += toStart.Count;
        tasks.AddRange(toStart.Take(taskLimit).Select(s => new DashboardTaskDto(DashboardTaskTarget.NewOnboarding, s.ApplicationId,
            $"Onboard {s.EmployeeName}", s.OfferNumber, $"Start onboarding (starts {s.StartDate:dd MMM})", "Recruitment",
            Domain.Applications.ApplicationStatus.Hired, s.AcceptedAt)));

        var myOnboarding = await onboarding.MyTasksAsync(ct);
        taskTotal += myOnboarding.Count;
        tasks.AddRange(myOnboarding.Take(taskLimit).Select(t => new DashboardTaskDto(DashboardTaskTarget.Onboarding, t.OnboardingId,
            $"{t.Title}: {t.EmployeeName}", t.Number, t.DueDate is null ? "Onboarding task" : $"Due {t.DueDate:dd MMM}", "Recruitment",
            Domain.Onboarding.OnboardingTaskStatus.Open, t.Since)));

        // ------------------------------------------------------------ Usability U1: the rest of the work waiting for someone
        var atsWaiting = await ats.AwaitingHrByVacancyAsync(ct);
        taskTotal += atsWaiting.Count;
        tasks.AddRange(atsWaiting.Take(taskLimit).Select(x => new DashboardTaskDto(DashboardTaskTarget.AtsConfirm, x.VacancyId, x.JobTitle,
            x.VacancyNumber, $"{x.Count} automatic rejection(s) to confirm", "Recruitment", Domain.Applications.ApplicationStatus.AutoRejected, x.Since)));

        var onboardingWork = await onboarding.MyWorkAsync(ct);
        taskTotal += onboardingWork.Count;
        tasks.AddRange(onboardingWork.Take(taskLimit).Select(w => new DashboardTaskDto(w.Kind switch
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
        tasks.AddRange(probationWork.Take(taskLimit).Select(w => new DashboardTaskDto(
            w.IsDecision ? DashboardTaskTarget.ProbationDecision : DashboardTaskTarget.ProbationStep, w.ProbationId, w.EmployeeName, w.Number,
            w.Due is { } due ? $"{w.Step}, due {due:dd MMM}" : w.Step, "Recruitment",
            w.IsDecision ? Domain.Probation.ProbationStatus.AwaitingHrDecision : Domain.Probation.ProbationStatus.InProgress, w.Since)));


        // ------------------------------------------------------------ Job adverts (11 Nov 2026, docs/29 change 2): HR review, IT approval, corrections
        var advertWork = await advertService.MyWorkAsync(ct);
        taskTotal += advertWork.Count;
        tasks.AddRange(advertWork.Take(taskLimit).Select(w => new DashboardTaskDto(DashboardTaskTarget.Advert, w.Id, w.Title, w.AdvertNumber,
            w.Action, "Recruitment", w.Status, w.Since)));
        var advertCards = await advertService.ItSummaryAsync(ct);

        // ------------------------------------------------------------ docs/32: this week's activity (filtered like the lists)
        if (access.Vacancies)
        {
            var weekStart = firstDay.ToDateTime(TimeOnly.MinValue);
            var appsThisWeek = await applications.CountAsync(x =>
                (all || hod.Contains(x.DepartmentId)) &&
                (divId == null || x.DivisionId == divId) && (deptId == null || x.DepartmentId == deptId) && x.SubmittedAt >= weekStart, ct);
            cards.Add(new ActionCardDto("Applications received", "fa-inbox", appsThisWeek, "In the last 7 days", "tone-interview",
                "Applications", "Index", Route("all", filtered: true), false));
        }

        var upcoming = await interviews.SearchAsync(new Interviews.InterviewFilter
        {
            Tab = "upcoming", From = today, To = today.AddDays(6), DivisionId = divId, DepartmentId = deptId, PageSize = 1
        }, ct);
        if (access.Vacancies || upcoming.TotalCount > 0)
        {
            var week = Route("upcoming", filtered: true);
            (week["From"], week["To"]) = (today.ToString("yyyy-MM-dd"), today.AddDays(6).ToString("yyyy-MM-dd")); // the list shows the same interviews
            cards.Add(new ActionCardDto("Interviews this week", "fa-people-group", upcoming.TotalCount, "Today and the next 6 days", "tone-interview",
                "Interviews", "Index", week, false));
        }

        if (access.Vacancies && !hodOnlyView)
        {
            var offersOut = await offers.CountAsync(o => o.Status == Domain.Hiring.OfferStatus.Sent &&
                (all || hod.Contains(o.DepartmentId)) && (divId == null || o.DivisionId == divId) && (deptId == null || o.DepartmentId == deptId), ct);
            cards.Add(new ActionCardDto("Offers awaiting the candidate", "fa-envelope-open-text", offersOut,
                offersOut == 0 ? "No offer waiting for an answer" : "Sent, not yet answered", "tone-offer", "Hiring", "Index", Route("offers", filtered: true), false));
        }

        // ------------------------------------------------------------ Candidates
        IReadOnlyList<CandidateSummary>? latestCandidates = null;
        if (access.Candidates)
            latestCandidates = await users.GetLatestCandidatesAsync(CandidateRows, ct);

        return new DashboardDto
        {
            Cards = cards,
            // Oldest first: the longer something has waited, the higher it sits.
            Tasks = tasks.OrderBy(t => t.Since).Take(taskLimit).ToList(),
            TaskTotal = taskTotal,
            ClosingDays = closingDays,
            Month = shownMonth,
            InProgress = inProgress,
            InProgressTotal = inProgressTotal,
            LatestCandidates = latestCandidates,
            AdvertCards = advertCards
        };
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
