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
    IAppLinks links)
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
        var cards = sessions.SelectMany(s => s.Scorecards).ToList();
        var taken = await PositionsTakenAsync(v.Id, ct);

        var current = recs.FirstOrDefault(r => r.Status != HiringStatus.Withdrawn && r.Status != HiringStatus.Rejected);
        var offer = appOffers.FirstOrDefault(o => o.Status is OfferStatus.Sent or OfferStatus.Accepted);
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
            VacancySalaryMin = v.SalaryMin,
            VacancySalaryMax = v.SalaryMax,
            Checks = appChecks.OrderBy(c => c.Id)
                .Select(c => new CheckDto(c.Id, c.Type, c.Subject, c.Status, c.Notes, c.CompletedAt, Name(c.RecordedByUserId) ?? "?")).ToList(),
            Recommendation = current is null ? null : ToDto(current),
            PastRecommendations = recs.Where(r => r != current).Select(ToDto).ToList(),
            Offer = offer is null ? null : ToOffer(offer),
            PastOffers = appOffers.Where(o => o != offer).Select(ToOffer).ToList(),
            CanRecordChecks = IsRecruiter && !own && HiringWorkflow.CanCheck(app.Status),
            CanRecommend = IsRecruiter && !own && recommendBlocked is null,
            RecommendBlockedReason = IsRecruiter && !own && current is null ? recommendBlocked : null,
            CanHodDecide = current?.Status == HiringStatus.AwaitingHod && isHod && !own,
            CanHrDecide = current?.Status == HiringStatus.AwaitingHr && me.IsInRole(Roles.HrManager) && !own && current.CreatedByUserId != me.UserId,
            HrNeedsOverride = current?.HodDecision == HiringDecision.Reject,
            CanMdDecide = current?.Status == HiringStatus.AwaitingMd && me.IsInRole(Roles.ManagingDirector) && !own &&
                          current.CreatedByUserId != me.UserId && current.HrUserId != me.UserId,
            CanWithdrawRecommendation = IsRecruiter && !own && current is not null && HiringWorkflow.IsOpen(current.Status),
            CanOffer = IsRecruiter && !own && current?.Status == HiringStatus.Approved && offerBlocked is null,
            OfferBlockedReason = IsRecruiter && current?.Status == HiringStatus.Approved ? offerBlocked : null,
            CanWithdrawOffer = IsRecruiter && !own && offer?.Status == OfferStatus.Sent
        };
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

    /// <summary>The recruiter proposes hiring the applicant. The department's HOD is asked to endorse it.</summary>
    public async Task<Result<int>> RecommendAsync(int applicationId, decimal? salary, DateOnly? startDate, string? justification, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure<int>(error!);
        var current = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id, ct))
            .FirstOrDefault(r => r.Status is not (HiringStatus.Withdrawn or HiringStatus.Rejected));
        var sessions = await interviews.ListForApplicationsAsync([app.Id], ct);
        if (RecommendProblem(app, current, sessions) is { } problem) return Result.Failure<int>(problem);

        justification = Clean(justification);
        if (salary is not { } pay || pay <= 0 || pay > 10_000_000) return Result.Failure<int>("Enter the proposed monthly salary in GHS.");
        if (startDate is not { } start || start < Today) return Result.Failure<int>("Choose a start date from today onwards.");
        if (justification is null || justification.Length < 20) return Result.Failure<int>("Explain the recommendation in at least 20 characters (interview results, references, ...).");
        if (justification.Length > 4000) return Result.Failure<int>("The justification must be 4,000 characters or fewer.");

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
            CreatedByUserId = MyId
        };
        recommendations.Add(rec);
        app.RecordStep(ApplicationAction.HiringRecommended, app.Status, MyId, scope.UserName,
            $"{rec.Number}: GHS {pay:N2} per month from {start:dd MMM yyyy}", DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

        foreach (var hod in await HodsAsync(app, ct))
            await mailer.ToUserAsync(hod, $"Hiring recommendation {rec.Number} needs your endorsement",
                $"{WorkflowMailer.Enc(scope.UserName)} recommends hiring <b>{WorkflowMailer.Enc(app.FullName)}</b>. " +
                "As Head of Department, please endorse it or say why not.", links.HiringCase(app.Id), "Open the hiring case", ct);
        return Result.Success(rec.Id);
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

    public async Task<Result<int>> SendOfferAsync(int applicationId, decimal? salary, DateOnly? startDate, DateOnly? expiresOn, string? terms, CancellationToken ct = default)
    {
        var (app, error) = await LoadForRecruiterAsync(applicationId, ct);
        if (app is null) return Result.Failure<int>(error!);
        var rec = (await recommendations.ListAsync(r => r.JobApplicationId == app.Id && r.Status == HiringStatus.Approved, ct)).OrderByDescending(r => r.Id).FirstOrDefault();
        if (rec is null) return Result.Failure<int>("The hiring recommendation must be approved by the Managing Director first.");
        var v = (await vacancies.GetByIdAsync(app.VacancyId, ct))!;
        var appChecks = await checks.ListAsync(c => c.JobApplicationId == app.Id, ct);
        var open = (await offers.ListAsync(o => o.JobApplicationId == app.Id, ct)).FirstOrDefault(o => o.Status is OfferStatus.Sent or OfferStatus.Accepted);
        if (OfferProblem(app, rec, appChecks, open, await PositionsTakenAsync(v.Id, ct), v.NumberOfPositions) is { } problem)
            return Result.Failure<int>(problem);

        var pay = salary ?? rec.ProposedSalary;
        var start = startDate ?? rec.ProposedStartDate;
        var expiry = expiresOn ?? Today.AddDays(7);
        terms = Clean(terms);
        if (pay <= 0 || pay > rec.ProposedSalary) return Result.Failure<int>($"The salary must be more than 0 and no more than the approved GHS {rec.ProposedSalary:N2}.");
        if (start < Today) return Result.Failure<int>("The start date cannot be in the past.");
        if (expiry < Today || expiry > Today.AddDays(30) || expiry > start)
            return Result.Failure<int>("The reply-by date must be within the next 30 days and no later than the start date.");
        if (terms?.Length > 8000) return Result.Failure<int>("The terms must be 8,000 characters or fewer.");

        var n = await numbers.NextAsync($"OFF-{DateTime.UtcNow.Year}", ct);
        var now = DateTime.UtcNow;
        var offer = new Offer
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
            SentAt = now,
            SentByUserId = MyId
        };
        offers.Add(offer);
        app.RecordStep(ApplicationAction.OfferSent, ApplicationStatus.Offer, MyId, scope.UserName, $"{offer.Number}, reply by {expiry:dd MMM yyyy}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

        await mailer.ToUserAsync(app.ApplicantUserId, $"Offer of employment: {v.JobTitle}",
            $"Congratulations! GREL is pleased to offer you the position of <b>{WorkflowMailer.Enc(v.JobTitle)}</b>, starting " +
            $"{start.ToString("d MMMM yyyy", Gb)}. Please read your offer letter and accept or decline it by <b>{expiry.ToString("d MMMM yyyy", Gb)}</b>.",
            links.MyApplication(app.Id), "Read my offer", ct);
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
            await mailer.ToUserAsync(app.ApplicantUserId, $"Your offer for {offer.JobTitle}",
                "We are sorry to tell you that the offer of employment has been withdrawn. The recruitment team will contact you.",
                links.MyApplication(app.Id), "View my application", ct);
        return saved;
    }

    // ===================================================================== The candidate's side

    /// <summary>The offer on one of my applications (the latest one), or null.</summary>
    public async Task<MyOfferDto?> GetMyOfferAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await applications.GetByIdAsync(applicationId, ct);
        if (app is null || app.ApplicantUserId != me.UserId) return null;
        var offer = (await offers.ListAsync(o => o.JobApplicationId == app.Id, ct))
            .Where(o => o.Status != OfferStatus.Withdrawn).OrderByDescending(o => o.Id).FirstOrDefault();
        if (offer is null) return null;
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
            await mailer.ToUserIncludingMeAsync(app.ApplicantUserId, $"Welcome to GREL: {offer.JobTitle}",
                $"Thank you for accepting our offer. We look forward to welcoming you on {offer.StartDate.ToString("d MMMM yyyy", Gb)}. " +
                "HR will contact you about your appointment letter and first day.", links.MyApplication(app.Id), "View my application", ct);
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

        return list.OrderByDescending(r => r.Id).Select(r => new HiringListItemDto(r.Id, r.Number, r.JobApplicationId,
                apps.GetValueOrDefault(r.JobApplicationId)?.FullName ?? "?", vacs.GetValueOrDefault(r.VacancyId)?.VacancyNumber ?? "?",
                vacs.GetValueOrDefault(r.VacancyId)?.JobTitle ?? "?", divNames.GetValueOrDefault(r.DivisionId, ""), deptNames.GetValueOrDefault(r.DepartmentId, ""),
                r.Status, r.ProposedSalary, r.ProposedStartDate, r.CreatedAt,
                appOffers.TryGetValue(r.Id, out var os) ? os : null))
            .ToList();
    }

    /// <summary>Recommendations waiting for the current user's decision (for the list tab and "My tasks").</summary>
    public async Task<IReadOnlyList<HiringListItemDto>> AwaitingMyDecisionAsync(CancellationToken ct = default) =>
        await SearchAsync(new HiringFilter { Tab = "awaiting" }, ct);

    private async Task<IEnumerable<HiringRecommendation>> AwaitingMeAsync(IReadOnlyList<HiringRecommendation> visible, CancellationToken ct)
    {
        var hodDepts = me.IsInRole(Roles.HeadOfDepartment) ? await scope.HodDepartmentIdsAsync(ct) : [];
        var applicantOf = (await applications.ListAsync(a => a.ApplicantUserId == MyId, ct)).Select(a => a.Id).ToHashSet();
        return visible.Where(r => !applicantOf.Contains(r.JobApplicationId) && (
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
        if (!sessions.Any(s => s.Status == InterviewStatus.Completed)) return "Complete at least one interview or assessment first.";
        return null;
    }

    private static string? OfferProblem(JobApplication app, HiringRecommendation rec, IReadOnlyCollection<BackgroundCheck> appChecks, Offer? open, int taken, int positions)
    {
        if (rec.Status != HiringStatus.Approved) return "The hiring recommendation must be approved by the Managing Director first.";
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

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<Result> SaveAsync(CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure(ConflictMessage);
        }
    }
}
