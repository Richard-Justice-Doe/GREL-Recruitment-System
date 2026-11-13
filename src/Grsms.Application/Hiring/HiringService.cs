using System.Globalization;
using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Interviews;
using Grsms.Application.Users;
using Grsms.Application.Vacancies;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Interviews;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using static Grsms.Application.Common.Text;

namespace Grsms.Application.Hiring;

/// <summary>
/// Background checks, the hiring recommendation with its approvals, and offers (Module 8). See <see cref="HiringWorkflow"/>.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Record checks, propose / withdraw a recommendation, send / withdraw an offer : Recruitment Officer or HR Manager.
///   * Endorse or not                                                            : the department's Head of Department.
///   * Approve / reject (HR stage)                                               : HR Manager (with a reason if the HOD did not endorse).
///   * Approve / reject (final)                                                  : Managing Director.
///   * Accept / decline an offer                                                 : the applicant, in their portal.
/// Nobody acts on their own application, approves a recommendation they proposed, or approves at two stages.
/// </summary>
public class HiringService(
    IApplicationRepository applications,
    IVacancyRepository vacancies,
    IInterviewRepository interviews,
    IRepository<BackgroundCheck> checks,
    IRepository<HiringRecommendation> recommendations,
    IRepository<Offer> offers,
    IRepository<Division> divisions,
    IRepository<Department> departments,
    IRepository<Site> sites,
    IUserDirectory users,
    INumberGenerator numbers,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links,
    Settings.ISettingsStore settings)
{
    private const string ConflictMessage = "Someone else updated this record at the same moment. Please reload the page and try again.";
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool IsRecruiter => HiringWorkflow.RecruiterRoles.Any(me.IsInRole);

    // ===================================================================== The hiring case

    /// <summary>Everything about hiring one applicant, or null if the user may not see it.</summary>
    public async Task<HiringCaseDto?> GetCaseAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await applications.GetByIdAsync(applicationId, ct);
        if (app is null) return null;
        var isHod = await IsHodOfAsync(app.DepartmentId, ct);
        if (!me.HasPermission(Permissions.SeeAllDepartments) && !isHod) return null;

        var v = (await vacancies.GetByIdAsync(app.VacancyId, ct))!;
        var appChecks = await checks.ListAsync(c => c.JobApplicationId == app.Id, ct);
        var recs = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id, ct)).OrderByDescending(r => r.Id).ToList();
        var appOffers = (await offers.ListAsync(o => o.JobApplicationId == app.Id, ct)).OrderByDescending(o => o.Id).ToList();
        var sessions = await interviews.ListForApplicationsAsync([app.Id], ct);
        var cards = sessions.SelectMany(InterviewWorkflow.ValidatedScorecards).ToList(); // validated scores only (9 Nov 2026)
        var taken = await PositionsTakenAsync(v.Id, ct);

        var current = recs.FirstOrDefault(r => r.Status != HiringStatus.Withdrawn && r.Status != HiringStatus.Rejected);
        var offer = appOffers.FirstOrDefault(o => o.Status is OfferStatus.Sent or OfferStatus.Accepted);
        var draft = offer is null ? appOffers.FirstOrDefault(o => o.Status == OfferStatus.Draft) : null;
        var names = await users.GetDisplayNamesAsync(
            recs.SelectMany(r => new[] { r.CreatedByUserId, r.HodUserId, r.HrUserId, r.MdUserId }).Concat(appChecks.Select(c => c.RecordedByUserId))
                .OfType<string>().Distinct(), ct);
        string? Name(string? id) => id is null ? null : names.GetValueOrDefault(id);
        RecommendationDto ToDto(HiringRecommendation r) => new(r.Id, r.Number, r.Status, r.ProposedSalary, r.ProposedStartDate, r.Justification,
            Name(r.CreatedByUserId) ?? "?", r.CreatedAt, r.HodDecision, Name(r.HodUserId), r.HodComment, r.HodDecidedAt,
            r.HrDecision, Name(r.HrUserId), r.HrComment, r.HrDecidedAt, r.MdDecision, Name(r.MdUserId), r.MdComment, r.MdDecidedAt);
        OfferDto ToOffer(Offer o) => new(o.Id, o.Number, o.Status, o.JobTitle, o.MonthlySalary, o.StartDate, o.ExpiresOn, o.Terms, o.SentAt, o.RespondedAt, o.Reason);

        var own = app.ApplicantUserId == me.UserId;
        var recommendBlocked = RecommendProblem(app, current, sessions);
        var offerBlocked = current is null ? null : OfferProblem(app, current, appChecks, offer, taken, v.NumberOfPositions);

        return new HiringCaseDto
        {
            ApplicationId = app.Id,
            ApplicationNumber = app.ApplicationNumber,
            ApplicantName = app.FullName,
            ApplicationStatus = app.Status,
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            JobTitle = v.JobTitle,
            DivisionName = (await divisions.GetByIdAsync(app.DivisionId, ct))?.Name ?? "",
            DepartmentName = (await departments.GetByIdAsync(app.DepartmentId, ct))?.Name ?? "",
            Positions = v.NumberOfPositions,
            PositionsTaken = taken,
            ScreeningScore = app.ScreeningScore,
            InterviewAverage = cards.Count == 0 ? null : Math.Round(cards.Average(c => c.WeightedScore), 1),
            CompletedInterviews = sessions.Count(s => s.Status == InterviewStatus.Completed),
            InterviewRounds = await InterviewRoundsAsync(sessions, v.RecruiterUserId, ct),
            VacancySalaryMin = v.SalaryMin,
            VacancySalaryMax = v.SalaryMax,
            Checks = appChecks.OrderBy(c => c.Id)
                .Select(c => new CheckDto(c.Id, c.Type, c.Subject, c.Status, c.Notes, c.CompletedAt, Name(c.RecordedByUserId) ?? "?")).ToList(),
            Recommendation = current is null ? null : ToDto(current),
            PastRecommendations = recs.Where(r => r != current).Select(ToDto).ToList(),
            Offer = offer is null ? null : ToOffer(offer),
            PastOffers = appOffers.Where(o => o != offer && o.Status != OfferStatus.Draft).Select(ToOffer).ToList(),
            OfferDraft = draft is null ? null : ToOffer(draft),
            CanEditOffer = IsRecruiter && !own && current?.Status == HiringStatus.Approved && offer is null && app.Status == ApplicationStatus.Interview,
            FormSalary = draft?.MonthlySalary ?? current?.ProposedSalary ?? 0,
            FormStartDate = draft?.StartDate ?? current?.ProposedStartDate ?? Today,
            FormExpiresOn = draft?.ExpiresOn ?? DefaultReplyBy(current?.ProposedStartDate ?? Today),
            FormTerms = draft is null ? DefaultTerms : draft.Terms,
            CanRecordChecks = IsRecruiter && !own && HiringWorkflow.CanCheck(app.Status),
            CanRecommend = IsRecruiter && !own && recommendBlocked is null,
            RecommendBlockedReason = IsRecruiter && !own && current is null ? recommendBlocked : null,
            CanHodDecide = current?.Status == HiringStatus.AwaitingHod && isHod && !own,
            CanHrDecide = current?.Status == HiringStatus.AwaitingHr && me.IsInRole(Roles.HrManager) && !own && current.CreatedByUserId != me.UserId,
            HrNeedsOverride = current?.HodDecision == HiringDecision.Reject,
            CanMdDecide = current?.Status == HiringStatus.AwaitingMd && me.IsInRole(Roles.ManagingDirector) && !own &&
                          current.CreatedByUserId != me.UserId && current.HrUserId != me.UserId,
            CanWithdrawRecommendation = IsRecruiter && !own && current is not null && HiringWorkflow.IsOpen(current.Status),
            CanSubmitDraft = me.IsInRole(Roles.RecruitmentOfficer) && !own && current?.Status == HiringStatus.Draft && RecommendProblem(app, null, sessions) is null,
            CanOffer = IsRecruiter && !own && current?.Status == HiringStatus.Approved && offerBlocked is null,
            OfferBlockedReason = IsRecruiter && current?.Status == HiringStatus.Approved ? offerBlocked : null,
            CanWithdrawOffer = IsRecruiter && !own && offer?.Status == OfferStatus.Sent
        };
    }

    /// <summary>
    /// The interview rounds of a hiring case with their scores (9 Nov 2026). A validated round shows every panel member's score,
    /// recommendation and comments; any other round says what it is waiting for, without scores.
    /// </summary>
    private async Task<IReadOnlyList<InterviewRoundScoresDto>> InterviewRoundsAsync(IReadOnlyList<Interview> sessions, string? recruiterUserId,
        CancellationToken ct)
    {
        var names = await users.GetDisplayNamesAsync(
            sessions.SelectMany(s => s.Panel.Select(p => p.UserId)).Concat(sessions.Select(s => s.ValidatedByUserId))
                .Append(recruiterUserId).OfType<string>().Where(id => id.Length > 0).Distinct(), ct);
        string N(string? id) => id is null ? "?" : names.GetValueOrDefault(id) ?? "?";
        // The vacancy's owner is named only when they are a Recruitment Officer (who validates); otherwise any Recruitment Officer.
        var ownerValidates = !string.IsNullOrEmpty(recruiterUserId) && await users.IsActiveUserInRoleAsync(recruiterUserId, Roles.RecruitmentOfficer, ct);
        var validator = ownerValidates ? N(recruiterUserId) : "a Recruitment Officer";

        // docs/37 CR2: the reason given for each score, from validated sessions only (they carry the criteria and ratings).
        var comments = new Dictionary<(int Session, string Panel), IReadOnlyList<string>>();
        foreach (var done in sessions.Where(s => s.Status == InterviewStatus.Completed))
            if (await interviews.GetWithDetailsAsync(done.Id, ct) is { } full)
                foreach (var card in InterviewWorkflow.ValidatedScorecards(full))
                    comments[(full.Id, card.PanelUserId)] = full.Criteria.OrderBy(c => c.SortOrder)
                        .Select(c => (c, r: card.Ratings.FirstOrDefault(r => r.CriterionId == c.Id)))
                        .Where(x => !string.IsNullOrWhiteSpace(x.r?.Comment))
                        .Select(x => $"{x.c.Name} ({x.r!.Score}/5): {x.r.Comment}").ToList();

        return sessions.OrderBy(s => s.Round).Select(s =>
        {
            var validCards = s.Scorecards.Where(c => c.IsValid).ToList();
            var validated = InterviewWorkflow.ValidatedScorecards(s).ToList();
            var waitingFor = s.Status switch
            {
                InterviewStatus.Completed => null,
                InterviewStatus.AwaitingValidation => $"All scorecards are in. Waiting for {validator} to validate the scores.",
                InterviewStatus.Scheduled when DateTime.UtcNow < s.StartsAt => "The session has not taken place yet.",
                InterviewStatus.Scheduled => $"Scoring in progress: {validCards.Count} of {InterviewWorkflow.ExpectedScorers(s).Count()} scorecards in. " +
                                             $"Then {validator} validates the scores.",
                _ => $"No scores: {EnumText.Label(s.Status).ToLowerInvariant()}."
            };
            var scores = validated
                .Select(c => new PanelScoreDto(N(c.PanelUserId), s.Panel.Any(p => p.UserId == c.PanelUserId && p.IsChair), c.WeightedScore,
                    c.Recommendation, c.Strengths, c.Concerns, comments.GetValueOrDefault((s.Id, c.PanelUserId))))
                .OrderByDescending(x => x.IsChair).ThenByDescending(x => x.Score).ToList();
            return new InterviewRoundScoresDto(s.Id, s.Round, s.Type, s.StartsAt, s.Status, validCards.Count, s.Panel.Count,
                validated.Count == 0 ? null : Math.Round(validated.Average(c => c.WeightedScore), 1),
                s.ValidatedByUserId is null ? null : N(s.ValidatedByUserId), s.ValidatedAt, waitingFor, scores);
        }).ToList();
    }

    // ===================================================================== Background checks

    public async Task<Result> AddCheckAsync(int applicationId, CheckType type, string? subject, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure(error!);
        if (!HiringWorkflow.CanCheck(app.Status)) return Result.Failure("Background checks start once the applicant has been interviewed.");
        subject = Clean(subject);
        if (subject is null) return Result.Failure("Say who or what is being checked (e.g. the referee's name and company).");
        if (subject.Length > 300) return Result.Failure("Keep the description under 300 characters.");

        checks.Add(new BackgroundCheck { JobApplicationId = app.Id, Type = type, Subject = subject, RecordedByUserId = MyId });
        app.RecordStep(ApplicationAction.CheckRecorded, app.Status, MyId, scope.UserName, $"{EnumText.Label(type)}: {subject} (pending)", DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    public async Task<Result> CompleteCheckAsync(int checkId, CheckStatus status, string? notes, CancellationToken ct = default)
    {
        var check = await checks.GetByIdAsync(checkId, ct);
        if (check is null) return Result.Failure("Check not found.");
        var (app, error) = await LoadForRecruiterAsync(check.JobApplicationId, ct);
        if (app is null) return Result.Failure(error!);
        if (status == CheckStatus.Pending) return Result.Failure("Choose the outcome of the check.");
        notes = Clean(notes);
        if (status is CheckStatus.Concerns or CheckStatus.Failed && notes is null) return Result.Failure("Describe the concern or the reason it failed.");
        if (notes?.Length > 4000) return Result.Failure("Notes must be 4,000 characters or fewer.");

        check.Status = status;
        check.Notes = notes;
        check.CompletedAt = DateTime.UtcNow;
        app.RecordStep(ApplicationAction.CheckRecorded, app.Status, MyId, scope.UserName, $"{EnumText.Label(check.Type)}: {EnumText.Label(status)}", DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    // ===================================================================== Hiring recommendation

    /// <summary>
    /// Proceed with candidates (9 Nov 2026): the Recruitment Officer chooses which candidates with validated interview scores to
    /// take forward, and the hiring process starts for each one automatically: a DRAFT recommendation (advertised minimum salary,
    /// start date in 30 days, justification from the validated scores) and the standard background checks as Pending.
    /// The recruiter then reviews the draft and submits it to the HOD. Returns how many were started.
    /// </summary>
    public async Task<Result<int>> ProceedAsync(IReadOnlyCollection<int> applicationIds, CancellationToken ct = default)
    {
        if (!IsRecruiter) return Result.Failure<int>("Only a Recruitment Officer or HR Manager can start the hiring process.");
        var ids = applicationIds.Distinct().ToList();
        if (ids.Count == 0) return Result.Failure<int>("Tick the candidate(s) you want to proceed with.");

        var errors = new List<string>();
        var started = 0;
        foreach (var id in ids)
        {
            var (app, error) = await LoadForRecruiterAsync(id, ct);
            if (app is null) { errors.Add(error!); continue; }
            var current = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id, ct))
                .FirstOrDefault(r => r.Status is not (HiringStatus.Withdrawn or HiringStatus.Rejected));
            var sessions = await interviews.ListForApplicationsAsync([app.Id], ct);
            if (RecommendProblem(app, current, sessions) is { } problem) { errors.Add($"{app.FullName}: {problem}"); continue; }
            if (await SelectionProblemAsync(app, 1, ct) is { } full) { errors.Add($"{app.FullName}: {full}"); continue; }

            var v = await vacancies.GetByIdAsync(app.VacancyId, ct);
            var n = await numbers.NextAsync($"HIR-{DateTime.UtcNow.Year}", ct);
            var rec = new HiringRecommendation
            {
                Number = $"HIR-{DateTime.UtcNow.Year}-{n:0000}",
                JobApplicationId = app.Id,
                VacancyId = app.VacancyId,
                DivisionId = app.DivisionId,
                DepartmentId = app.DepartmentId,
                RequestingHodUserId = app.RequestingHodUserId,
                ProposedSalary = v?.SalaryMin ?? 0,
                ProposedStartDate = Today.AddDays(HiringWorkflow.DraftStartInDays),
                Justification = ScoreJustification(await WithRatingsAsync(sessions, ct)),
                CreatedByUserId = MyId,
                Status = HiringStatus.Draft
            };
            recommendations.Add(rec);

            var existing = (await checks.ListAsync(c => c.JobApplicationId == app.Id, ct)).Select(c => c.Type).ToHashSet();
            var opened = HiringWorkflow.StandardChecks.Where(c => !existing.Contains(c.Type)).ToList();
            foreach (var (type, subject) in opened)
                checks.Add(new BackgroundCheck { JobApplicationId = app.Id, Type = type, Subject = subject, RecordedByUserId = MyId });

            app.RecordStep(ApplicationAction.HiringRecommended, app.Status, MyId, scope.UserName,
                $"{rec.Number}: hiring process started (draft recommendation, {opened.Count} background check{(opened.Count == 1 ? "" : "s")} opened)", DateTime.UtcNow);
            var saved = await SaveAsync(ct);
            if (saved.Succeeded) started++;
            else errors.AddRange(saved.Errors);
        }
        return started > 0 || errors.Count == 0 ? Result.Success(started) : Result.Failure<int>([.. errors]);
    }

    /// <summary>Discards a draft recommendation (the candidate goes back to "Ready to recommend"). Background checks are kept.</summary>
    public async Task<Result> DiscardDraftAsync(int applicationId, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure(error!);
        var draft = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id && r.Status == HiringStatus.Draft, ct)).FirstOrDefault();
        if (draft is null) return Result.Failure("There is no draft recommendation to discard.");
        draft.Status = HiringStatus.Withdrawn;
        draft.Version = Guid.NewGuid();
        app.RecordStep(ApplicationAction.HiringDecided, app.Status, MyId, scope.UserName, $"{draft.Number}: draft discarded", DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    /// <summary>The start of a draft's justification: the validated interview scores, round by round.</summary>
    /// <summary>The validated sessions again with their criteria and ratings (for the score reasons, docs/37 CR2).</summary>
    private async Task<IReadOnlyList<Interview>> WithRatingsAsync(IReadOnlyList<Interview> sessions, CancellationToken ct)
    {
        var list = new List<Interview>();
        foreach (var s in sessions)
            list.Add(s.Status == InterviewStatus.Completed && await interviews.GetWithDetailsAsync(s.Id, ct) is { } full ? full : s);
        return list;
    }

    private static string ScoreJustification(IReadOnlyList<Interview> sessions)
    {
        var lines = sessions.Where(s => s.Status == InterviewStatus.Completed).OrderBy(s => s.Round).Select(s =>
        {
            var cards = InterviewWorkflow.ValidatedScorecards(s).ToList();
            var votes = string.Join(", ", cards.GroupBy(c => c.Recommendation).OrderBy(g => g.Key)
                .Select(g => $"{g.Count()} {EnumText.Label(g.Key).ToLowerInvariant()}"));
            // docs/37 CR2: the panel's reasons for their scores, criterion by criterion.
            var reasons = s.Criteria.OrderBy(c => c.SortOrder).SelectMany(c => cards
                .Select(card => card.Ratings.FirstOrDefault(r => r.CriterionId == c.Id))
                .Where(r => !string.IsNullOrWhiteSpace(r?.Comment))
                .Select(r => $"  • {c.Name} ({r!.Score}/5): {r.Comment!.Trim()}"));
            return $"Round {s.Round} ({EnumText.Label(s.Type).ToLowerInvariant()}): " +
                   $"{(cards.Count == 0 ? 0 : Math.Round(cards.Average(c => c.WeightedScore), 1)).ToString("0.0", Gb)}/100 " +
                   $"from {cards.Count} validated scorecard{(cards.Count == 1 ? "" : "s")}; {votes}." +
                   string.Concat(reasons.Select(r => "\n" + r));
        });
        var text = $"Interview results:\n{string.Join("\n", lines)}\n\nReferences and fit: ";
        // The justification is limited to 4,000 characters; keep room for the recruiter's own words.
        return text.Length > 3500 ? text[..3480] + "…\n\nReferences and fit: " : text;
    }

    /// <summary>
    /// The recruiter proposes hiring the applicant. The department's HOD is asked to endorse it. When a draft exists (the hiring
    /// process was started with "Proceed"), the draft is updated with these values and submitted.
    /// </summary>
    public async Task<Result<int>> RecommendAsync(int applicationId, decimal? salary, DateOnly? startDate, string? justification, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure<int>(error!);
        var current = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id, ct))
            .FirstOrDefault(r => r.Status is not (HiringStatus.Withdrawn or HiringStatus.Rejected));
        var draft = current?.Status == HiringStatus.Draft ? current : null;
        var sessions = await interviews.ListForApplicationsAsync([app.Id], ct);
        if (RecommendProblem(app, draft is null ? current : null, sessions) is { } problem) return Result.Failure<int>(problem);

        justification = Clean(justification);
        if (salary is not { } pay || pay <= 0 || pay > 10_000_000) return Result.Failure<int>("Enter the proposed monthly salary in GHS.");
        if (startDate is not { } start || start < Today) return Result.Failure<int>("Choose a start date from today onwards.");
        if (justification is null || justification.Length < 20) return Result.Failure<int>("Explain the recommendation in at least 20 characters (interview results, references, ...).");
        if (justification.Length > 4000) return Result.Failure<int>("The justification must be 4,000 characters or fewer.");

        // 9 Nov 2026 (user's choice): the hire needs the Recruitment Officer's approval only. The HOD, HR Manager and MD
        // are not asked; the approved recommendation goes straight on to the offer. Who approved and when are CreatedBy/CreatedAt.
        if (!me.IsInRole(Roles.RecruitmentOfficer)) return Result.Failure<int>("Only a Recruitment Officer can approve a hire.");
        if (await SelectionProblemAsync(app, 1, ct) is { } full) return Result.Failure<int>(full);
        var now = DateTime.UtcNow;

        if (draft is not null)
        {
            // Approving the draft started by "Proceed": same record and number.
            draft.ProposedSalary = pay;
            draft.ProposedStartDate = start;
            draft.Justification = justification;
            draft.Status = HiringStatus.Approved;
            draft.CreatedByUserId = MyId;
            draft.CreatedAt = now;
            draft.Version = Guid.NewGuid();
            app.RecordStep(ApplicationAction.HiringDecided, app.Status, MyId, scope.UserName,
                $"{draft.Number}: hire approved by the Recruitment Officer, GHS {pay:N2} per month from {start:dd MMM yyyy}", now);
            var approved = await SaveAsync(ct);
            if (!approved.Succeeded) return Result.Failure<int>([.. approved.Errors]);
            await DraftOfferAndSaveAsync(app, draft, ct);
            return Result.Success(draft.Id);
        }

        var n = await numbers.NextAsync($"HIR-{DateTime.UtcNow.Year}", ct);
        var rec = new HiringRecommendation
        {
            Number = $"HIR-{DateTime.UtcNow.Year}-{n:0000}",
            JobApplicationId = app.Id,
            VacancyId = app.VacancyId,
            DivisionId = app.DivisionId,
            DepartmentId = app.DepartmentId,
            RequestingHodUserId = app.RequestingHodUserId,
            ProposedSalary = pay,
            ProposedStartDate = start,
            Justification = justification,
            CreatedByUserId = MyId,
            Status = HiringStatus.Approved
        };
        recommendations.Add(rec);
        app.RecordStep(ApplicationAction.HiringDecided, app.Status, MyId, scope.UserName,
            $"{rec.Number}: hire approved by the Recruitment Officer, GHS {pay:N2} per month from {start:dd MMM yyyy}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);
        await DraftOfferAndSaveAsync(app, rec, ct);
        return Result.Success(rec.Id);
    }

    /// <summary>After the approval is saved: the offer draft (13 Nov 2026). If it can't be saved, the RO creates it from the case page.</summary>
    private async Task DraftOfferAndSaveAsync(JobApplication app, HiringRecommendation rec, CancellationToken ct)
    {
        await DraftOfferAsync(app, rec, ct);
        await SaveAsync(ct);
    }

    /// <summary>
    /// The vacancy's positions limit the selection (9 Nov 2026): candidates with a draft, an approved hire, an offer or a hire
    /// count; adding <paramref name="adding"/> more must not exceed the number of positions. Null when it fits.
    /// </summary>
    private async Task<string?> SelectionProblemAsync(JobApplication app, int adding, CancellationToken ct)
    {
        var v = await vacancies.GetByIdAsync(app.VacancyId, ct);
        if (v is null) return "Vacancy not found.";
        var active = (await recommendations.ListAsync(r => r.VacancyId == v.Id && r.JobApplicationId != app.Id &&
            (r.Status == HiringStatus.Draft || r.Status == HiringStatus.Approved || r.Status == HiringStatus.AwaitingHod ||
             r.Status == HiringStatus.AwaitingHr || r.Status == HiringStatus.AwaitingMd), ct)).Select(r => r.JobApplicationId).Distinct().ToList();
        var stillIn = (await applications.ListAsync(a => active.Contains(a.Id) &&
            (a.Status == ApplicationStatus.Interview || a.Status == ApplicationStatus.Offer || a.Status == ApplicationStatus.Hired), ct)).Count;
        return stillIn + adding > v.NumberOfPositions
            ? $"{v.VacancyNumber} has {v.NumberOfPositions} position{(v.NumberOfPositions == 1 ? "" : "s")} and {stillIn} candidate{(stillIn == 1 ? " is" : "s are")} already selected. " +
              "Discard a draft or withdraw a hire first, or increase the number of positions."
            : null;
    }

    /// <summary>A decision at the current stage: HOD (endorse), HR Manager or Managing Director (approve / reject).</summary>
    public async Task<Result> DecideAsync(int applicationId, HiringDecision decision, string? comment, CancellationToken ct = default)
    {
        var app = await applications.GetWithHistoryAsync(applicationId, ct);
        if (app is null) return Result.Failure("Application not found.");
        var rec = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id, ct)).FirstOrDefault(r => HiringWorkflow.IsOpen(r.Status));
        if (rec is null) return Result.Failure("There is no hiring recommendation waiting for a decision.");
        if (app.ApplicantUserId == MyId) return Result.Failure("This is your own application.");
        if (decision == HiringDecision.None) return Result.Failure("Choose approve or reject.");
        comment = Clean(comment);
        if (comment?.Length > 4000) return Result.Failure("The comment must be 4,000 characters or fewer.");
        var now = DateTime.UtcNow;
        string stage;

        switch (rec.Status)
        {
            case HiringStatus.AwaitingHod:
                if (!await IsHodOfAsync(app.DepartmentId, ct)) return Result.Failure("Only the Head of this department can endorse this recommendation.");
                if (decision == HiringDecision.Reject && (comment is null || comment.Length < 5)) return Result.Failure("Say why you do not endorse this hire.");
                (rec.HodDecision, rec.HodComment, rec.HodUserId, rec.HodDecidedAt) = (decision, comment, MyId, now);
                rec.Status = HiringStatus.AwaitingHr; // HR always decides; the HOD's view travels with the recommendation
                stage = "Head of Department";
                break;

            case HiringStatus.AwaitingHr:
                if (!me.IsInRole(Roles.HrManager)) return Result.Failure("Only an HR Manager can decide at this stage.");
                if (rec.CreatedByUserId == MyId) return Result.Failure("You proposed this hire, so another HR Manager must decide.");
                if (decision == HiringDecision.Approve && rec.HodDecision == HiringDecision.Reject && (comment is null || comment.Length < 5))
                    return Result.Failure("The Head of Department did not endorse this hire, so record why you approve it anyway.");
                if (decision == HiringDecision.Reject && (comment is null || comment.Length < 5)) return Result.Failure("Say why you reject this hire.");
                (rec.HrDecision, rec.HrComment, rec.HrUserId, rec.HrDecidedAt) = (decision, comment, MyId, now);
                rec.Status = decision == HiringDecision.Approve ? HiringStatus.AwaitingMd : HiringStatus.Rejected;
                stage = "HR Manager";
                break;

            case HiringStatus.AwaitingMd:
                if (!me.IsInRole(Roles.ManagingDirector)) return Result.Failure("Only the Managing Director can give the final approval.");
                if (rec.CreatedByUserId == MyId || rec.HrUserId == MyId) return Result.Failure("You already acted on this recommendation, so someone else must approve it.");
                if (decision == HiringDecision.Reject && (comment is null || comment.Length < 5)) return Result.Failure("Say why you reject this hire.");
                (rec.MdDecision, rec.MdComment, rec.MdUserId, rec.MdDecidedAt) = (decision, comment, MyId, now);
                rec.Status = decision == HiringDecision.Approve ? HiringStatus.Approved : HiringStatus.Rejected;
                stage = "Managing Director";
                break;

            default:
                return Result.Failure("There is no hiring recommendation waiting for a decision.");
        }

        rec.Version = Guid.NewGuid();
        app.RecordStep(ApplicationAction.HiringDecided, app.Status, MyId, scope.UserName,
            $"{stage}: {EnumText.Label(decision)}{(comment is null ? "" : $" ({comment})")}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;
        if (rec.Status == HiringStatus.Approved) await DraftOfferAndSaveAsync(app, rec, ct); // older chain: final approval drafts the offer too

        // Tell whoever acts next, and the recruiter about the outcome.
        var link = links.HiringCase(app.Id);
        if (rec.Status == HiringStatus.AwaitingHr)
            await mailer.ToRoleAsync(Roles.HrManager, [rec.CreatedByUserId], $"{rec.Number} needs HR approval",
                $"The hiring recommendation for <b>{WorkflowMailer.Enc(app.FullName)}</b> is waiting for an HR Manager.", link, "Open the hiring case", ct);
        else if (rec.Status == HiringStatus.AwaitingMd)
            await mailer.ToRoleAsync(Roles.ManagingDirector, [], $"{rec.Number} needs your final approval",
                $"The hiring recommendation for <b>{WorkflowMailer.Enc(app.FullName)}</b> has been approved by HR and needs your final approval.",
                link, "Open the hiring case", ct);
        else
            await mailer.ToUserAsync(rec.CreatedByUserId, $"{rec.Number} {(rec.Status == HiringStatus.Approved ? "approved" : "rejected")}",
                $"The hiring recommendation for <b>{WorkflowMailer.Enc(app.FullName)}</b> was {(rec.Status == HiringStatus.Approved ? "<b>approved</b>. You can now send the offer" : "<b>rejected</b>")}.",
                link, "Open the hiring case", ct);
        return saved;
    }

    public async Task<Result> WithdrawRecommendationAsync(int applicationId, string? reason, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure(error!);
        var rec = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id, ct)).FirstOrDefault(r => HiringWorkflow.IsOpen(r.Status));
        if (rec is null) return Result.Failure("There is no open recommendation to withdraw.");
        reason = Clean(reason);
        if (reason is null || reason.Length < 5) return Result.Failure("Give the reason for withdrawing.");

        rec.Status = HiringStatus.Withdrawn;
        rec.Version = Guid.NewGuid();
        app.RecordStep(ApplicationAction.HiringDecided, app.Status, MyId, scope.UserName, $"{rec.Number} withdrawn: {reason}", DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    // ===================================================================== Offers

    // ---- 13 November 2026 (docs/26 Phase 24): offer drafts. When the hire is approved, the offer is drafted with the approved
    // salary and start date, the default reply-by date and terms (System settings) and its reference number. The RO checks it,
    // previews the letter, and sends it with one click. The candidate never sees a draft.

    private string? DefaultTerms => Clean(settings.Get(Domain.Settings.SettingCatalogue.OfferDefaultTerms)?.Replace("\\n", "\n"));

    private DateOnly DefaultReplyBy(DateOnly start)
    {
        var replyBy = Today.AddDays(Math.Max(1, settings.GetInt(Domain.Settings.SettingCatalogue.OfferDefaultReplyDays)));
        return replyBy > start ? (start < Today ? Today : start) : replyBy;
    }

    /// <summary>Adds a draft offer for an approved hire (nothing if the application already has a draft, sent or accepted offer).</summary>
    private async Task DraftOfferAsync(JobApplication app, HiringRecommendation rec, CancellationToken ct)
    {
        if ((await offers.ListAsync(o => o.JobApplicationId == app.Id, ct)).Any(o => o.Status is OfferStatus.Draft or OfferStatus.Sent or OfferStatus.Accepted))
            return;
        var v = (await vacancies.GetByIdAsync(app.VacancyId, ct))!;
        offers.Add(await NewOfferAsync(app, rec, v, rec.ProposedSalary, rec.ProposedStartDate, DefaultReplyBy(rec.ProposedStartDate), DefaultTerms, OfferStatus.Draft, ct));
    }

    private async Task<Offer> NewOfferAsync(JobApplication app, HiringRecommendation rec, Domain.Vacancies.Vacancy v, decimal pay, DateOnly start, DateOnly expiry,
        string? terms, OfferStatus status, CancellationToken ct)
    {
        var n = await numbers.NextAsync($"OFF-{DateTime.UtcNow.Year}", ct);
        var now = DateTime.UtcNow;
        return new Offer
        {
            Number = $"OFF-{now.Year}-{n:0000}",
            JobApplicationId = app.Id,
            HiringRecommendationId = rec.Id,
            VacancyId = v.Id,
            DivisionId = app.DivisionId,
            DepartmentId = app.DepartmentId,
            RequestingHodUserId = app.RequestingHodUserId,
            JobTitle = v.JobTitle,
            MonthlySalary = pay,
            StartDate = start,
            ExpiresOn = expiry,
            Terms = terms,
            Status = status,
            SentAt = now,
            SentByUserId = MyId
        };
    }

    /// <summary>The checks shared by saving a draft and sending: salary within the approved amount, dates, terms length.</summary>
    private string? OfferTermsProblem(HiringRecommendation rec, decimal pay, DateOnly start, DateOnly expiry, string? terms)
    {
        if (pay <= 0 || pay > rec.ProposedSalary) return $"The salary must be more than 0 and no more than the approved GHS {rec.ProposedSalary:N2}.";
        if (start < Today) return "The start date cannot be in the past.";
        var maxDays = settings.GetInt(Domain.Settings.SettingCatalogue.OfferReplyMaxDays); // Phase 16: System settings
        if (expiry < Today || expiry > Today.AddDays(maxDays) || expiry > start)
            return $"The reply-by date must be within the next {maxDays} days and no later than the start date.";
        if (terms?.Length > 8000) return "The terms must be 8,000 characters or fewer.";
        return null;
    }

    /// <summary>Saves the offer draft (creating it for a hire approved before drafts existed). Sending is a separate click.</summary>
    public async Task<Result<int>> SaveOfferDraftAsync(int applicationId, decimal? salary, DateOnly? startDate, DateOnly? expiresOn, string? terms, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure<int>(error!);
        var rec = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id && r.Status == HiringStatus.Approved, ct)).OrderByDescending(r => r.Id).FirstOrDefault();
        if (rec is null) return Result.Failure<int>("The hire must be approved by the Recruitment Officer first.");
        var appOffers = await offers.ListAsync(o => o.JobApplicationId == app.Id, ct);
        if (appOffers.Any(o => o.Status is OfferStatus.Sent or OfferStatus.Accepted)) return Result.Failure<int>("The offer has already been sent.");
        if (app.Status != ApplicationStatus.Interview) return Result.Failure<int>("Offers can only be made to applicants at the interview stage.");

        var draft = appOffers.FirstOrDefault(o => o.Status == OfferStatus.Draft);
        var pay = salary ?? draft?.MonthlySalary ?? rec.ProposedSalary;
        var start = startDate ?? draft?.StartDate ?? rec.ProposedStartDate;
        var expiry = expiresOn ?? draft?.ExpiresOn ?? DefaultReplyBy(start);
        terms = Clean(terms);
        if (OfferTermsProblem(rec, pay, start, expiry, terms) is { } problem) return Result.Failure<int>(problem);

        if (draft is null)
        {
            var v = (await vacancies.GetByIdAsync(app.VacancyId, ct))!;
            draft = await NewOfferAsync(app, rec, v, pay, start, expiry, terms, OfferStatus.Draft, ct);
            offers.Add(draft);
        }
        else
        {
            (draft.MonthlySalary, draft.StartDate, draft.ExpiresOn, draft.Terms) = (pay, start, expiry, terms);
            (draft.SentAt, draft.SentByUserId) = (DateTime.UtcNow, MyId);
            draft.Version = Guid.NewGuid();
        }
        var saved = await SaveAsync(ct);
        return saved.Succeeded ? Result.Success(draft.Id) : Result.Failure<int>([.. saved.Errors]);
    }

    /// <summary>The offer letter for staff: the draft (preview) or the offer sent. Visible to whoever may open the hiring case.</summary>
    public async Task<MyOfferDto?> GetOfferLetterAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await applications.GetByIdAsync(applicationId, ct);
        if (app is null || app.ApplicantUserId == me.UserId) return null;
        if (!me.HasPermission(Permissions.SeeAllDepartments) && !await IsHodOfAsync(app.DepartmentId, ct)) return null;
        var offer = (await offers.ListAsync(o => o.JobApplicationId == app.Id, ct))
            .Where(o => o.Status is OfferStatus.Draft or OfferStatus.Sent or OfferStatus.Accepted).OrderByDescending(o => o.Id).FirstOrDefault();
        return offer is null ? null : await LetterAsync(app, offer, ct);
    }

    public async Task<Result<int>> SendOfferAsync(int applicationId, decimal? salary, DateOnly? startDate, DateOnly? expiresOn, string? terms, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure<int>(error!);
        var rec = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id && r.Status == HiringStatus.Approved, ct)).OrderByDescending(r => r.Id).FirstOrDefault();
        if (rec is null) return Result.Failure<int>("The hire must be approved by the Recruitment Officer first.");
        var v = (await vacancies.GetByIdAsync(app.VacancyId, ct))!;
        var appChecks = await checks.ListAsync(c => c.JobApplicationId == app.Id, ct);
        var appOffers = await offers.ListAsync(o => o.JobApplicationId == app.Id, ct);
        var open = appOffers.FirstOrDefault(o => o.Status is OfferStatus.Sent or OfferStatus.Accepted);
        if (OfferProblem(app, rec, appChecks, open, await PositionsTakenAsync(v.Id, ct), v.NumberOfPositions) is { } problem)
            return Result.Failure<int>(problem);

        var draft = appOffers.FirstOrDefault(o => o.Status == OfferStatus.Draft);
        var pay = salary ?? draft?.MonthlySalary ?? rec.ProposedSalary;
        var start = startDate ?? draft?.StartDate ?? rec.ProposedStartDate;
        var expiry = expiresOn ?? draft?.ExpiresOn ?? DefaultReplyBy(start);
        terms = terms is null && draft is not null ? draft.Terms : Clean(terms); // not posted = keep the draft's
        if (OfferTermsProblem(rec, pay, start, expiry, terms) is { } termsProblem) return Result.Failure<int>(termsProblem);

        var now = DateTime.UtcNow;
        Offer offer;
        if (draft is not null)
        {
            // Sending the draft: same record and reference number.
            offer = draft;
            (offer.MonthlySalary, offer.StartDate, offer.ExpiresOn, offer.Terms) = (pay, start, expiry, terms);
            (offer.Status, offer.SentAt, offer.SentByUserId) = (OfferStatus.Sent, now, MyId);
            offer.Version = Guid.NewGuid();
        }
        else
        {
            offer = await NewOfferAsync(app, rec, v, pay, start, expiry, terms, OfferStatus.Sent, ct);
            offers.Add(offer);
        }
        app.RecordStep(ApplicationAction.OfferSent, ApplicationStatus.Offer, MyId, scope.UserName, $"{offer.Number}, reply by {expiry:dd MMM yyyy}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

        await mailer.TemplateToUserAsync(app.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.OfferSent,
            new Dictionary<string, string?>
            {
                ["JobTitle"] = v.JobTitle, ["StartDate"] = start.ToString("d MMMM yyyy", Gb), ["ReplyBy"] = expiry.ToString("d MMMM yyyy", Gb)
            },
            links.MyApplication(app.Id), ct: ct);
        await mailer.TextAsync(app.Phone, $"GREL: congratulations! We have sent you an offer for {v.JobTitle}. Please sign in to GRSMS to reply by " +
            $"{expiry.ToString("d MMM", Gb)}.", ct);
        return Result.Success(offer.Id);
    }

    public async Task<Result> WithdrawOfferAsync(int applicationId, string? reason, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure(error!);
        var offer = (await offers.ListAsync(o => o.JobApplicationId == app.Id && o.Status == OfferStatus.Sent, ct)).FirstOrDefault();
        if (offer is null) return Result.Failure("There is no open offer to withdraw.");
        reason = Clean(reason);
        if (reason is null || reason.Length < 5) return Result.Failure("Give the reason for withdrawing the offer.");

        offer.Status = OfferStatus.Withdrawn;
        offer.Reason = reason;
        offer.Version = Guid.NewGuid();
        app.RecordStep(ApplicationAction.OfferWithdrawn, HiringWorkflow.ApplicationStatusFor(OfferStatus.Withdrawn), MyId, scope.UserName, reason, DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
            await mailer.TemplateToUserAsync(app.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.OfferWithdrawn,
                new Dictionary<string, string?> { ["JobTitle"] = offer.JobTitle }, links.MyApplication(app.Id), ct: ct);
        return saved;
    }

    // ===================================================================== The candidate's side

    /// <summary>The offer on one of my applications (the latest one), or null.</summary>
    public async Task<MyOfferDto?> GetMyOfferAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await applications.GetByIdAsync(applicationId, ct);
        if (app is null || app.ApplicantUserId != me.UserId) return null;
        var offer = (await offers.ListAsync(o => o.JobApplicationId == app.Id, ct))
            .Where(o => o.Status is not (OfferStatus.Withdrawn or OfferStatus.Draft)).OrderByDescending(o => o.Id).FirstOrDefault();
        return offer is null ? null : await LetterAsync(app, offer, ct);
    }

    private async Task<MyOfferDto> LetterAsync(JobApplication app, Offer offer, CancellationToken ct)
    {
        var v = (await vacancies.GetByIdAsync(offer.VacancyId, ct))!;
        var manager = v.ReportingManagerUserId is null ? null : (await users.GetDisplayNamesAsync([v.ReportingManagerUserId], ct)).GetValueOrDefault(v.ReportingManagerUserId);
        return new MyOfferDto(offer.Id, offer.Number, app.FullName, offer.JobTitle,
            (await departments.GetByIdAsync(offer.DepartmentId, ct))?.Name ?? "", (await sites.GetByIdAsync(v.SiteId, ct))?.Name ?? "",
            manager, offer.MonthlySalary, offer.StartDate, offer.ExpiresOn, offer.Terms, offer.Status, offer.SentAt, offer.RespondedAt,
            offer.Status == OfferStatus.Sent && offer.ExpiresOn >= Today);
    }

    /// <summary>The candidate accepts (application becomes Hired) or declines their offer.</summary>
    public async Task<Result> RespondToOfferAsync(int applicationId, bool accept, string? reason, CancellationToken ct = default)
    {
        var app = await applications.GetWithHistoryAsync(applicationId, ct);
        if (app is null || app.ApplicantUserId != me.UserId) return Result.Failure("Offer not found.");
        var offer = (await offers.ListAsync(o => o.JobApplicationId == app.Id && o.Status == OfferStatus.Sent, ct)).FirstOrDefault();
        if (offer is null) return Result.Failure("There is no offer waiting for your reply.");
        if (offer.ExpiresOn < Today) return Result.Failure("This offer has expired. Please contact the recruitment team.");
        reason = Clean(reason);
        if (reason?.Length > 2000) return Result.Failure("Keep your message under 2,000 characters.");

        var now = DateTime.UtcNow;
        offer.Status = accept ? OfferStatus.Accepted : OfferStatus.Declined;
        offer.RespondedAt = now;
        offer.Reason = accept ? null : reason;
        offer.Version = Guid.NewGuid();
        app.RecordStep(accept ? ApplicationAction.OfferAccepted : ApplicationAction.OfferDeclined,
            HiringWorkflow.ApplicationStatusFor(offer.Status), MyId, app.FullName, accept ? null : reason, now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        var text = accept
            ? $"<b>{WorkflowMailer.Enc(app.FullName)}</b> has <b>accepted</b> the offer for {WorkflowMailer.Enc(offer.JobTitle)} (starts {offer.StartDate.ToString("d MMMM yyyy", Gb)})."
            : $"<b>{WorkflowMailer.Enc(app.FullName)}</b> has <b>declined</b> the offer for {WorkflowMailer.Enc(offer.JobTitle)}.{(reason is null ? "" : $" Reason: {WorkflowMailer.Enc(reason)}")}";
        var subject = $"{offer.Number} {(accept ? "accepted" : "declined")}";
        var link = links.HiringCase(app.Id);
        await mailer.ToUserAsync(offer.SentByUserId, subject, text, link, "Open the hiring case", ct);
        foreach (var hod in await HodsAsync(app, ct))
            await mailer.ToUserAsync(hod, subject, text, link, "Open the hiring case", ct);
        if (accept)
            await mailer.TemplateToUserAsync(app.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.OfferAccepted,
                new Dictionary<string, string?> { ["JobTitle"] = offer.JobTitle, ["StartDate"] = offer.StartDate.ToString("d MMMM yyyy", Gb) },
                links.MyApplication(app.Id), includeMe: true, ct);
        return saved;
    }

    /// <summary>Marks offers not answered by their expiry date as expired (run by the background job). Returns how many.</summary>
    public async Task<int> ExpireOffersAsync(DateOnly today, CancellationToken ct = default)
    {
        var due = await offers.ListAsync(o => o.Status == OfferStatus.Sent && o.ExpiresOn < today, ct);
        foreach (var o in due)
        {
            o.Status = OfferStatus.Expired;
            o.Version = Guid.NewGuid();
            var app = await applications.GetWithHistoryAsync(o.JobApplicationId, ct);
            app?.RecordStep(ApplicationAction.OfferExpired, HiringWorkflow.ApplicationStatusFor(OfferStatus.Expired), null, "System", o.Number, DateTime.UtcNow);
        }
        if (due.Count == 0) return 0;
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return 0; }
        foreach (var o in due)
            await mailer.ToUserAsync(o.SentByUserId, $"{o.Number} expired", $"The offer for {WorkflowMailer.Enc(o.JobTitle)} was not answered in time.",
                links.HiringCase(o.JobApplicationId), "Open the hiring case", ct);
        return due.Count;
    }

    // ===================================================================== Lists and tasks

    public async Task<IReadOnlyList<HiringListItemDto>> SearchAsync(HiringFilter filter, CancellationToken ct = default)
    {
        var visibility = await scope.VisibilityAsync(ct);
        var hodDepts = (await scope.HodDepartmentIdsAsync(ct)).ToList();
        var all = await recommendations.ListAsync(r => visibility.SeeAll || hodDepts.Contains(r.DepartmentId), ct);
        IEnumerable<HiringRecommendation> q = all;

        if (filter.Tab == "awaiting") q = await AwaitingMeAsync(all, ct);
        else if (filter.Tab == "approved") q = q.Where(r => r.Status == HiringStatus.Approved);
        if (filter.Status is { } s) q = q.Where(r => r.Status == s);
        if (filter.DivisionId is { } div) q = q.Where(r => r.DivisionId == div);
        if (filter.DepartmentId is { } dep) q = q.Where(r => r.DepartmentId == dep);
        if (filter.From is { } from) q = q.Where(r => DateOnly.FromDateTime(r.CreatedAt) >= from);
        if (filter.To is { } to) q = q.Where(r => DateOnly.FromDateTime(r.CreatedAt) <= to);

        var list = q.ToList();
        var appIds = list.Select(r => r.JobApplicationId).ToList();
        var apps = (await applications.ListAsync(a => appIds.Contains(a.Id), ct)).ToDictionary(a => a.Id);
        var appOffers = (await offers.ListAsync(o => appIds.Contains(o.JobApplicationId), ct)).GroupBy(o => o.HiringRecommendationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.Id).First().Status);
        if (filter.Tab == "offers") list = list.Where(r => appOffers.ContainsKey(r.Id)).ToList();
        var vacancyIds = list.Select(r => r.VacancyId).Distinct().ToList();
        var vacs = (await vacancies.ListAsync(v => vacancyIds.Contains(v.Id), ct)).ToDictionary(v => v.Id);
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        var divNames = (await divisions.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);

        // The validated interview average of each candidate, shown next to the salary on the list (9 Nov 2026).
        var averages = (await interviews.ListForApplicationsAsync(list.Select(r => r.JobApplicationId).Distinct().ToList(), ct))
            .GroupBy(s => s.JobApplicationId)
            .Select(g => (g.Key, Cards: g.SelectMany(InterviewWorkflow.ValidatedScorecards).ToList()))
            .Where(x => x.Cards.Count > 0)
            .ToDictionary(x => x.Key, x => Math.Round(x.Cards.Average(c => c.WeightedScore), 1));

        return list.OrderByDescending(r => r.Id).Select(r => new HiringListItemDto(r.Id, r.Number, r.JobApplicationId,
                apps.GetValueOrDefault(r.JobApplicationId)?.FullName ?? "?", vacs.GetValueOrDefault(r.VacancyId)?.VacancyNumber ?? "?",
                vacs.GetValueOrDefault(r.VacancyId)?.JobTitle ?? "?", divNames.GetValueOrDefault(r.DivisionId, ""), deptNames.GetValueOrDefault(r.DepartmentId, ""),
                r.Status, r.ProposedSalary, r.ProposedStartDate, r.CreatedAt,
                appOffers.TryGetValue(r.Id, out var os) ? os : null,
                averages.TryGetValue(r.JobApplicationId, out var avg) ? avg : null))
            .ToList();
    }

    /// <summary>
    /// Candidates ready for a hiring recommendation (9 Nov 2026): at the interview stage, with at least one interview whose scores
    /// the Recruitment Officer has validated, and no recommendation in progress or approved. Best validated average first per vacancy.
    /// <paramref name="onlyMine"/>: only those the current Recruitment Officer should act on (vacancies they own, or whose owner is
    /// not a Recruitment Officer), for Home's to-dos.
    /// </summary>
    public async Task<IReadOnlyList<ReadyToRecommendDto>> ListReadyToRecommendAsync(bool onlyMine = false, CancellationToken ct = default)
    {
        if (onlyMine && !IsRecruiter) return [];
        var visibility = await scope.VisibilityAsync(ct);
        var hodDepts = (await scope.HodDepartmentIdsAsync(ct)).ToList();
        if (!visibility.SeeAll && hodDepts.Count == 0) return [];
        var apps = await applications.ListAsync(a => a.Status == ApplicationStatus.Interview
            && (visibility.SeeAll || hodDepts.Contains(a.DepartmentId)), ct);
        if (apps.Count == 0) return [];
        var appIds = apps.Select(a => a.Id).ToList();
        var sessions = (await interviews.ListForApplicationsAsync(appIds, ct)).ToLookup(s => s.JobApplicationId);
        var busy = (await recommendations.ListAsync(r => appIds.Contains(r.JobApplicationId), ct))
            .Where(r => r.Status is not (HiringStatus.Withdrawn or HiringStatus.Rejected))
            .Select(r => r.JobApplicationId).ToHashSet();
        var vacs = (await vacancies.ListAsync(v => apps.Select(a => a.VacancyId).Contains(v.Id), ct)).ToDictionary(v => v.Id);
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        var ownerIsRecruiter = new Dictionary<string, bool>();

        var ready = new List<ReadyToRecommendDto>();
        foreach (var a in apps.Where(a => !busy.Contains(a.Id) && a.ApplicantUserId != me.UserId))
        {
            var validated = sessions[a.Id].Where(s => s.Status == InterviewStatus.Completed).ToList();
            if (validated.Count == 0) continue;
            var v = vacs.GetValueOrDefault(a.VacancyId);
            if (onlyMine)
            {
                var owner = v?.RecruiterUserId ?? "";
                if (!ownerIsRecruiter.TryGetValue(owner, out var isRo))
                    ownerIsRecruiter[owner] = isRo = owner.Length > 0 && await users.IsActiveUserInRoleAsync(owner, Roles.RecruitmentOfficer, ct);
                if (isRo && owner != me.UserId) continue;
            }
            var cards = validated.SelectMany(InterviewWorkflow.ValidatedScorecards).ToList();
            ready.Add(new ReadyToRecommendDto(a.Id, a.ApplicationNumber, a.FullName, v?.Id ?? 0, v?.VacancyNumber ?? "?", v?.JobTitle ?? "?",
                deptNames.GetValueOrDefault(a.DepartmentId, ""),
                cards.Count == 0 ? null : Math.Round(cards.Average(c => c.WeightedScore), 1),
                validated.Count, validated.Max(s => s.ValidatedAt ?? s.StartsAt)));
        }
        return ready.OrderBy(r => r.VacancyNumber).ThenByDescending(r => r.InterviewAverage ?? -1).ToList();
    }

    /// <summary>Recommendations waiting for the current user's decision (for the list tab and "My tasks").</summary>
    public async Task<IReadOnlyList<HiringListItemDto>> AwaitingMyDecisionAsync(CancellationToken ct = default) =>
        await SearchAsync(new HiringFilter { Tab = "awaiting" }, ct);

    private async Task<IEnumerable<HiringRecommendation>> AwaitingMeAsync(IReadOnlyList<HiringRecommendation> visible, CancellationToken ct)
    {
        var hodDepts = me.IsInRole(Roles.HeadOfDepartment) ? await scope.HodDepartmentIdsAsync(ct) : [];
        var applicantOf = (await applications.ListAsync(a => a.ApplicantUserId == MyId, ct)).Select(a => a.Id).ToHashSet();
        return visible.Where(r => !applicantOf.Contains(r.JobApplicationId) && (
            r.Status == HiringStatus.Draft && r.CreatedByUserId == MyId ||   // 9 Nov 2026: my draft, to review and submit
            r.Status == HiringStatus.AwaitingHod && hodDepts.Contains(r.DepartmentId) ||
            r.Status == HiringStatus.AwaitingHr && me.IsInRole(Roles.HrManager) && r.CreatedByUserId != MyId ||
            r.Status == HiringStatus.AwaitingMd && me.IsInRole(Roles.ManagingDirector) && r.CreatedByUserId != MyId && r.HrUserId != MyId));
    }

    // ===================================================================== Helpers

    private async Task<(JobApplication? Application, string? Error)> LoadForRecruiterAsync(int applicationId, CancellationToken ct)
    {
        if (!IsRecruiter) return (null, "Only a Recruitment Officer or HR Manager can do this.");
        var app = await applications.GetWithHistoryAsync(applicationId, ct);
        if (app is null) return (null, "Application not found.");
        return app.ApplicantUserId == MyId ? (null, "This is your own application.") : (app, null);
    }

    private static string? RecommendProblem(JobApplication app, HiringRecommendation? current, IReadOnlyList<Interview> sessions)
    {
        if (current is not null) return current.Status == HiringStatus.Approved ? "A recommendation has already been approved." : "A recommendation is already in progress.";
        if (!HiringWorkflow.CanRecommend(app.Status)) return "Only applicants at the interview stage can be recommended for hire.";
        if (!sessions.Any(s => s.Status == InterviewStatus.Completed)) return "The scores of at least one interview or assessment must be validated by the Recruitment Officer first.";
        return null;
    }

    private static string? OfferProblem(JobApplication app, HiringRecommendation rec, IReadOnlyCollection<BackgroundCheck> appChecks, Offer? open, int taken, int positions)
    {
        if (rec.Status != HiringStatus.Approved) return "The hire must be approved by the Recruitment Officer first.";
        if (open is not null) return open.Status == OfferStatus.Accepted ? "The candidate has accepted an offer." : "An offer is already waiting for the candidate.";
        if (app.Status != ApplicationStatus.Interview) return "Offers can only be made to applicants at the interview stage.";
        if (!HiringWorkflow.ChecksAllowOffer(appChecks))
            return appChecks.Count == 0 ? "Record at least one background check (e.g. references) first."
                : appChecks.Any(c => c.Status == CheckStatus.Failed) ? "A background check has failed." : "Some background checks are still pending.";
        if (taken >= positions) return $"All {positions} position(s) for this vacancy are already offered or filled.";
        return null;
    }

    /// <summary>Offers sent or accepted for the vacancy (positions taken).</summary>
    private async Task<int> PositionsTakenAsync(int vacancyId, CancellationToken ct) =>
        await offers.CountAsync(o => o.VacancyId == vacancyId && (o.Status == OfferStatus.Sent || o.Status == OfferStatus.Accepted), ct);

    private async Task<IReadOnlyList<string>> HodsAsync(JobApplication app, CancellationToken ct) =>
        new[] { app.RequestingHodUserId, (await departments.GetByIdAsync(app.DepartmentId, ct))?.HeadOfDepartmentUserId }
            .OfType<string>().Distinct().Where(id => id != app.ApplicantUserId).ToList();

    private async Task<bool> IsHodOfAsync(int departmentId, CancellationToken ct) =>
        me.IsInRole(Roles.HeadOfDepartment) && (await scope.HodDepartmentIdsAsync(ct)).Contains(departmentId);


    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);
}
