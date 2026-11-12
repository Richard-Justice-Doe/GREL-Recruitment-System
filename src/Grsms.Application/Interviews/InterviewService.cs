using System.Globalization;
using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Application.Vacancies;
using Grsms.Domain.Applications;
using Grsms.Domain.Interviews;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using static Grsms.Application.Common.Text;

namespace Grsms.Application.Interviews;

/// <summary>
/// Assessments, interviews and scorecards (Module 7). See <see cref="InterviewWorkflow"/> for the rules.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Schedule / reschedule / cancel / record no-show           : Recruitment Officer or HR Manager.
///   * Submit a scorecard                                        : members of that interview's panel.
///   * Validate the scores, return a scorecard, mark a member
///     absent or excused (9 Nov 2026)                            : any Recruitment Officer, also one on the panel (the
///                                                                 vacancy's own is emailed; if the owner is not a Recruitment
///                                                                 Officer, all are). Nobody returns their own scorecard.
///   * See a session                                             : its panel; reviewers; the department's HOD; MD; HR Admin.
///   * See all scorecards before validation                      : Recruitment Officers, and panel members who have submitted
///                                                                 their own.
///   * See all scorecards once validated                         : also reviewers, the department's HOD, MD and HR Admin.
/// The applicant sees the date, place and instructions only, never scores or comments.
/// </summary>
public class InterviewService(
    IInterviewRepository interviews,
    IApplicationRepository applications,
    IVacancyRepository vacancies,
    IRepository<Department> departments,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links)
{
    private const string ConflictMessage = "Someone else updated this interview at the same moment. Please reload the page and try again.";
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool IsScheduler => InterviewWorkflow.SchedulerRoles.Any(me.IsInRole);
    private bool SeesEverything => me.HasPermission(Permissions.SeeAllDepartments);

    // ===================================================================== Scheduling

    /// <summary>A new schedule form for a shortlisted application, with the department's HOD on the panel and default criteria.</summary>
    public async Task<Result<ScheduleFormDto>> GetScheduleFormAsync(int applicationId, CancellationToken ct = default)
    {
        var (app, error) = await LoadApplicationForSchedulingAsync(applicationId, ct);
        if (app is null) return Result.Failure<ScheduleFormDto>(error!);

        var form = new ScheduleFormDto
        {
            ApplicationId = applicationId,
            StartsAt = DateTime.UtcNow.Date.AddDays(3).AddHours(10),
            Criteria = string.Join("\n", InterviewWorkflow.DefaultCriteria.Select(c => $"{c.Name} | {c.Weight}"))
        };
        await FillScheduleFormAsync(form, ct);
        form.PanelUserIds = [.. form.RequiredPanelUserIds];
        form.ChairUserId = form.RequiredPanelUserIds.FirstOrDefault();
        return Result.Success(form);
    }

    /// <summary>Fills the read-only parts of the schedule form (also after a failed post).</summary>
    public async Task FillScheduleFormAsync(ScheduleFormDto form, CancellationToken ct = default)
    {
        var app = await applications.GetByIdAsync(form.ApplicationId, ct);
        if (app is null) return;
        var v = await vacancies.GetByIdAsync(app.VacancyId, ct);
        form.ApplicantName = app.FullName;
        form.ApplicationNumber = app.ApplicationNumber;
        form.JobTitle = v?.JobTitle;
        form.Round = (await interviews.ListAsync(i => i.JobApplicationId == app.Id, ct)).Count + 1;
        form.RequiredPanelUserIds = await RequiredHodsAsync(app, ct);
        form.StaffList = (await users.GetActiveStaffAsync(null, ct)).Where(u => u.Value != app.ApplicantUserId).ToList();
    }

    public async Task<Result<int>> ScheduleAsync(ScheduleFormDto form, CancellationToken ct = default)
    {
        var (app, error) = await LoadApplicationForSchedulingAsync(form.ApplicationId, ct);
        if (app is null) return Result.Failure<int>(error!);

        var errors = new List<string>();
        var now = DateTime.UtcNow;
        if (form.StartsAt is not { } startsAt || startsAt < now.AddDays(-1))
            errors.Add("Choose a date and time (no earlier than yesterday, to record a session that already took place).");

        var panel = form.PanelUserIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var required = await RequiredHodsAsync(app, ct);
        if (panel.Count is < InterviewWorkflow.MinPanelSize or > InterviewWorkflow.MaxPanelSize)
            errors.Add($"The panel needs between {InterviewWorkflow.MinPanelSize} and {InterviewWorkflow.MaxPanelSize} people.");
        if (required.Count > 0 && !required.Any(panel.Contains))
            errors.Add("The department's Head of Department must be on the panel.");
        if (panel.Contains(app.ApplicantUserId))
            errors.Add("The applicant cannot sit on their own panel.");
        foreach (var id in panel)
            if (!await users.IsActiveStaffAsync(id, ct)) { errors.Add("Every panel member must be an active GREL staff member."); break; }
        var chair = string.IsNullOrWhiteSpace(form.ChairUserId) ? panel.FirstOrDefault() : form.ChairUserId;
        if (chair is null || !panel.Contains(chair)) errors.Add("Choose a chair from the panel.");

        var criteria = ParseCriteria(form.Criteria, errors);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);
        if (!form.IgnoreClashes && await ClashesAsync(form.StartsAt!.Value, form.DurationMinutes, panel, app.ApplicantUserId, null, ct) is { Count: > 0 } clashes)
            return Result.Failure<int>([.. clashes]);

        var round = (await interviews.ListAsync(i => i.JobApplicationId == app.Id, ct)).Count + 1;
        var interview = new Interview
        {
            JobApplicationId = app.Id,
            VacancyId = app.VacancyId,
            DivisionId = app.DivisionId,
            DepartmentId = app.DepartmentId,
            RequestingHodUserId = app.RequestingHodUserId,
            Round = round,
            Type = form.Type!.Value,
            Mode = form.Mode!.Value,
            StartsAt = form.StartsAt!.Value,
            DurationMinutes = form.DurationMinutes,
            Location = form.Location.Trim(),
            CandidateInstructions = Clean(form.CandidateInstructions),
            ScheduledByUserId = MyId
        };
        foreach (var id in panel) interview.Panel.Add(new InterviewPanelMember { UserId = id, IsChair = id == chair });
        for (var i = 0; i < criteria.Count; i++)
            interview.Criteria.Add(new InterviewCriterion { Name = criteria[i].Name, Weight = criteria[i].Weight, SortOrder = i });
        interviews.Add(interview);

        var what = EnumText.Label(interview.Type);
        var when = interview.StartsAt.ToString("dddd d MMMM yyyy, HH:mm", Gb);
        if (app.Status == ApplicationStatus.Shortlisted)
            app.RecordStep(ApplicationAction.InterviewScheduled, ApplicationStatus.Interview, MyId, scope.UserName, $"Round {round}: {what}, {when}", now);
        else
            app.RecordStep(ApplicationAction.InterviewScheduled, app.Status, MyId, scope.UserName, $"Round {round}: {what}, {when}", now);

        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

        var v = await vacancies.GetByIdAsync(app.VacancyId, ct);
        await mailer.TemplateToUserAsync(app.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.InterviewInvitation,
            new Dictionary<string, string?>
            {
                ["Kind"] = what.ToLowerInvariant(), ["JobTitle"] = v?.JobTitle, ["When"] = when, ["Duration"] = interview.DurationMinutes.ToString(Gb),
                ["Mode"] = EnumText.Label(interview.Mode), ["Location"] = interview.Location, ["Instructions"] = interview.CandidateInstructions
            },
            links.MyApplication(app.Id), ct: ct);
        await mailer.TextAsync(app.Phone, $"GREL: you are invited to a {what.ToLowerInvariant()} for {v?.JobTitle} on {when} GMT, {interview.Location}. Details: sign in to GRSMS.", ct);
        foreach (var member in interview.Panel)
            await mailer.ToUserAsync(member.UserId, $"Panel: {what} with {app.FullName}, {interview.StartsAt:dd MMM HH:mm}",
                $"You are {(member.IsChair ? "the <b>chair</b> of" : "on")} the panel for a {WorkflowMailer.Enc(what.ToLowerInvariant())} with " +
                $"<b>{WorkflowMailer.Enc(app.FullName)}</b> for <b>{WorkflowMailer.Enc(v?.JobTitle)}</b> on {when} GMT at {WorkflowMailer.Enc(interview.Location)}." +
                "<p>Please submit your scorecard in GRSMS after the session.</p>",
                links.Interview(interview.Id), "Open the interview", ct);
        return Result.Success(interview.Id);
    }

    /// <summary>Moves a scheduled session to a new time or place. The applicant and panel are emailed.</summary>
    public async Task<Result> RescheduleAsync(int id, DateTime? startsAt, string? location, CancellationToken ct = default, bool ignoreClashes = false)
    {
        var (i, error) = await LoadForManagerAsync(id, ct);
        if (i is null) return Result.Failure(error!);
        if (i.Scorecards.Count > 0) return Result.Failure("Scorecards have already been submitted for this session.");
        if (startsAt is not { } when || when < DateTime.UtcNow) return Result.Failure("Choose a new date and time in the future.");
        location = Clean(location) ?? i.Location;
        if (location.Length > 500) return Result.Failure("The place must be 500 characters or fewer.");
        if (!ignoreClashes && await applications.GetByIdAsync(i.JobApplicationId, ct) is { } applicant
            && await ClashesAsync(when, i.DurationMinutes, i.Panel.Select(p => p.UserId).ToList(), applicant.ApplicantUserId, i.Id, ct) is { Count: > 0 } clashes)
            return Result.Failure([.. clashes]);

        i.StartsAt = when;
        i.Location = location;
        i.Version = Guid.NewGuid();
        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
        {
            var app = await applications.GetByIdAsync(i.JobApplicationId, ct);
            var text = $"The {WorkflowMailer.Enc(EnumText.Label(i.Type).ToLowerInvariant())} has moved to <b>{when.ToString("dddd d MMMM yyyy, HH:mm", Gb)} GMT</b> " +
                       $"at {WorkflowMailer.Enc(location)}.";
            if (app is not null)
            {
                await mailer.TemplateToUserAsync(app.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.InterviewRescheduled,
                    new Dictionary<string, string?>
                    {
                        ["Kind"] = EnumText.Label(i.Type).ToLowerInvariant(), ["When"] = when.ToString("dddd d MMMM yyyy, HH:mm", Gb), ["Location"] = location
                    },
                    links.MyApplication(app.Id), ct: ct);
                await mailer.TextAsync(app.Phone, $"GREL: your {EnumText.Label(i.Type).ToLowerInvariant()} has moved to {when.ToString("ddd d MMM HH:mm", Gb)} GMT, {location}.", ct);
            }
            foreach (var m in i.Panel)
                await mailer.ToUserAsync(m.UserId, $"Rescheduled: {app?.FullName}", text, links.Interview(i.Id), "Open the interview", ct);
        }
        return saved;
    }

    /// <summary>Starts every clash message, so the page knows to offer "Schedule anyway".</summary>
    public const string ClashPrefix = "Clash: ";

    /// <summary>
    /// 12 November 2026 (docs/34 quick win): the panel members and the candidate who already have a scheduled session overlapping
    /// <paramref name="start"/> for <paramref name="minutes"/> minutes (other than <paramref name="exceptId"/>). One message each.
    /// A warning only: the Recruitment Officer can still schedule (IgnoreClashes), e.g. when the other session is about to move.
    /// </summary>
    private async Task<List<string>> ClashesAsync(DateTime start, int minutes, IReadOnlyCollection<string> panel, string applicantUserId, int? exceptId,
        CancellationToken ct)
    {
        if (start <= DateTime.UtcNow) return []; // recording a session that already took place: nobody can be double-booked any more
        var end = start.AddMinutes(minutes);
        // Sessions are at most 8 hours long, so anything that overlaps starts within 8 hours before this one ends.
        var nearby = (await interviews.ListScheduledBetweenAsync(start.AddHours(-8), end, ct))
            .Where(x => x.Id != exceptId && x.StartsAt < end && x.StartsAt.AddMinutes(x.DurationMinutes) > start).ToList();
        if (nearby.Count == 0) return [];

        var appIds = nearby.Select(x => x.JobApplicationId).Distinct().ToList();
        var applicants = (await applications.ListAsync(a => appIds.Contains(a.Id), ct)).ToDictionary(a => a.Id);
        var names = await users.GetDisplayNamesAsync(panel.Append(applicantUserId).Distinct(), ct);
        var messages = new List<string>();
        foreach (var other in nearby.OrderBy(x => x.StartsAt))
        {
            var otherApplicant = applicants.GetValueOrDefault(other.JobApplicationId);
            var when = $"{other.StartsAt.ToString("HH:mm", Gb)}-{other.StartsAt.AddMinutes(other.DurationMinutes).ToString("HH:mm", Gb)}";
            foreach (var person in panel.Where(p => other.Panel.Any(m => m.UserId == p)))
                messages.Add($"{ClashPrefix}{names.GetValueOrDefault(person, "A panel member")} is already on a panel {when} (interview with {otherApplicant?.FullName ?? "another candidate"}).");
            if (otherApplicant?.ApplicantUserId == applicantUserId)
                messages.Add($"{ClashPrefix}the candidate already has a session {when} (round {other.Round}).");
        }
        return messages.Distinct().ToList();
    }

    // ===================================================================== Scorecards

    /// <summary>Submits (or, until the session is completed, updates) the current user's scorecard.</summary>
    public async Task<Result> SubmitScorecardAsync(int id, IReadOnlyDictionary<int, int> ratings, PanelRecommendation? recommendation,
        string? strengths, string? concerns, CancellationToken ct = default)
    {
        var i = await interviews.GetWithDetailsAsync(id, ct);
        if (i is null || i.Panel.All(p => p.UserId != MyId)) return Result.Failure("Only members of this panel can submit a scorecard.");
        if (!InterviewWorkflow.CanScore(i, DateTime.UtcNow))
            return Result.Failure(i.Status == InterviewStatus.Scheduled
                ? "Scorecards open when the session starts."
                : "This session is closed, so scorecards can no longer change.");
        if (recommendation is null) return Result.Failure("Choose your overall recommendation.");
        if (i.Criteria.Any(c => !ratings.TryGetValue(c.Id, out var r) || r < InterviewWorkflow.MinRating || r > InterviewWorkflow.MaxRating))
            return Result.Failure($"Rate every criterion from {InterviewWorkflow.MinRating} to {InterviewWorkflow.MaxRating}.");
        strengths = Clean(strengths);
        concerns = Clean(concerns);
        if (strengths?.Length > 4000 || concerns?.Length > 4000) return Result.Failure("Comments must be 4,000 characters or fewer.");
        if (recommendation == PanelRecommendation.DoNotRecommend && concerns is null)
            return Result.Failure("Say what your concerns are when you do not recommend the applicant.");

        var now = DateTime.UtcNow;
        var card = i.Scorecards.FirstOrDefault(s => s.PanelUserId == MyId);
        if (card is null)
        {
            card = new Scorecard { InterviewId = i.Id, PanelUserId = MyId };
            i.Scorecards.Add(card);
        }
        card.Recommendation = recommendation.Value;
        card.Strengths = strengths;
        card.Concerns = concerns;
        card.SubmittedAt = now;
        card.Ratings.Clear();
        foreach (var c in i.Criteria) card.Ratings.Add(new ScorecardRating { CriterionId = c.Id, Score = ratings[c.Id] });
        card.WeightedScore = InterviewWorkflow.WeightedScore(i.Criteria.Select(c => (ratings[c.Id], c.Weight)));

        // Score validation: submitting again clears a "returned for correction" mark, and scoring counts as taking part.
        card.ReturnedAt = null;
        card.ReturnReason = null;
        card.ReturnedByUserId = null;
        var member = i.Panel.First(p => p.UserId == MyId);
        if (member.Attendance != PanelAttendance.Present) MarkAttendance(member, PanelAttendance.Present, null, now);

        var before = i.Status;
        InterviewWorkflow.RefreshValidationStatus(i, now);
        i.Version = Guid.NewGuid();
        var saved = await SaveAsync(ct);
        if (saved.Succeeded && before != InterviewStatus.AwaitingValidation && i.Status == InterviewStatus.AwaitingValidation)
            await NotifyReadyForValidationAsync(i, ct);
        return saved;
    }

    // ===================================================================== Score validation (9 Nov 2026)
    // When every panel member who took part has submitted, the vacancy's Recruitment Officer (or another standing in)
    // validates the scores, or returns a scorecard to its member with a reason. Only then do the HOD, MD and the
    // interview results see the scores, and can a hiring recommendation be made.

    /// <summary>Validates the scores: the session is completed and its scorecards become final and visible.</summary>
    public async Task<Result> ValidateAsync(int id, string? note, CancellationToken ct = default)
    {
        var (i, error) = await LoadForValidatorAsync(id, ct);
        if (i is null) return Result.Failure(error!);
        var now = DateTime.UtcNow;
        var names = await users.GetDisplayNamesAsync(i.Panel.Select(p => p.UserId), ct);
        if (ValidationProblem(i, now, names) is { } problem) return Result.Failure(problem);
        note = Clean(note);
        if (note?.Length > 2000) return Result.Failure("The note must be 2,000 characters or fewer.");

        i.Status = InterviewStatus.Completed;
        i.StatusReason = null;
        i.ValidatedAt = now;
        i.ValidatedByUserId = MyId;
        i.ValidationNote = note;
        i.Version = Guid.NewGuid();
        var app = (await applications.GetWithHistoryAsync(i.JobApplicationId, ct))!;
        var cards = i.Scorecards.Where(s => s.IsValid).ToList();
        app.RecordStep(ApplicationAction.InterviewCompleted, app.Status, MyId, scope.UserName,
            $"Round {i.Round}: {cards.Count} scorecard{(cards.Count == 1 ? "" : "s")} validated by {scope.UserName}" +
            $", average {Math.Round(cards.Average(c => c.WeightedScore), 1).ToString(Gb)}/100{(note is null ? "" : $". {note}")}", now);

        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
        {
            var hods = await HodIdsAsync(i, ct);
            foreach (var userId in i.Panel.Select(p => p.UserId).Concat(hods).Distinct())
                await mailer.ToUserAsync(userId, $"Scores validated: {app.FullName}",
                    $"The scores of the {WorkflowMailer.Enc(EnumText.Label(i.Type).ToLowerInvariant())} with <b>{WorkflowMailer.Enc(app.FullName)}</b> " +
                    $"(round {i.Round}) have been checked and validated by {WorkflowMailer.Enc(scope.UserName)}. They are now final.",
                    links.Interview(i.Id), "See the results", ct);

            // Next step: hiring. The vacancy's Recruitment Officer (or every one, if the owner is not a Recruitment Officer)
            // is asked to write the hiring recommendation. Nobody is emailed about their own action.
            var owner = await VacancyValidatorAsync(i.VacancyId, ct);
            var recruiters = owner is not null
                ? [owner]
                : (await users.GetActiveUsersInRoleAsync(InterviewWorkflow.ValidatorRole, ct)).Select(u => u.Value).ToList();
            var average = Math.Round(cards.Average(c => c.WeightedScore), 1).ToString("0.0", Gb);
            foreach (var userId in recruiters)
                await mailer.ToUserAsync(userId, $"Ready for hiring: {app.FullName}",
                    $"The interview scores of <b>{WorkflowMailer.Enc(app.FullName)}</b> are validated (average <b>{average}/100</b>). " +
                    "<p>Next step: decide whether to proceed with this candidate. Proceeding starts the hiring process (a draft " +
                    "recommendation and the background checks), which you then submit to the Head of Department.</p>",
                    links.HiringCase(app.Id), "Open the hiring case", ct);
        }
        return saved;
    }

    /// <summary>Sends one panel member's scorecard back for correction. The member is emailed the reason.</summary>
    public async Task<Result> ReturnScorecardAsync(int id, string? panelUserId, string? reason, CancellationToken ct = default)
    {
        var (i, error) = await LoadForValidatorAsync(id, ct);
        if (i is null) return Result.Failure(error!);
        var card = i.Scorecards.FirstOrDefault(s => s.PanelUserId == panelUserId);
        if (card is null) return Result.Failure("That panel member has not submitted a scorecard.");
        if (!card.IsValid) return Result.Failure("That scorecard has already been returned for correction.");
        if (card.PanelUserId == MyId) return Result.Failure("You cannot return your own scorecard. Update it yourself instead.");
        reason = Clean(reason);
        if (reason is null || reason.Length < 10) return Result.Failure("Say what needs correcting (at least 10 characters), so the panel member knows what to change.");
        if (reason.Length > 2000) return Result.Failure("The reason must be 2,000 characters or fewer.");

        var now = DateTime.UtcNow;
        card.ReturnedAt = now;
        card.ReturnReason = reason;
        card.ReturnedByUserId = MyId;
        InterviewWorkflow.RefreshValidationStatus(i, now);
        i.Version = Guid.NewGuid();
        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
        {
            var app = await applications.GetByIdAsync(i.JobApplicationId, ct);
            await mailer.ToUserAsync(card.PanelUserId, $"Please correct your scorecard: {app?.FullName}",
                $"{WorkflowMailer.Enc(scope.UserName)} checked the scores of the {WorkflowMailer.Enc(EnumText.Label(i.Type).ToLowerInvariant())} with " +
                $"<b>{WorkflowMailer.Enc(app?.FullName)}</b> and has sent your scorecard back for correction:" +
                $"<p><i>{WorkflowMailer.Enc(reason)}</i></p><p>Please open the interview, update your scorecard and submit it again.</p>",
                links.Interview(i.Id), "Correct my scorecard", ct);
        }
        return saved;
    }

    /// <summary>
    /// Records that a panel member did not take part (Absent or Excused, with a note), so their scorecard is not waited for;
    /// or clears that again (NotRecorded). The Head of Department cannot be marked absent: their scorecard is required.
    /// </summary>
    public async Task<Result> SetAttendanceAsync(int id, string? panelUserId, PanelAttendance attendance, string? note, CancellationToken ct = default)
    {
        var (i, error) = await LoadForValidatorAsync(id, ct);
        if (i is null) return Result.Failure(error!);
        var member = i.Panel.FirstOrDefault(p => p.UserId == panelUserId);
        if (member is null) return Result.Failure("That person is not on this panel.");
        var now = DateTime.UtcNow;
        if (now < i.StartsAt) return Result.Failure("Attendance can be recorded once the session has started.");
        if (attendance is not (PanelAttendance.Absent or PanelAttendance.Excused or PanelAttendance.NotRecorded))
            return Result.Failure("Choose Absent or Excused.");
        if (i.Scorecards.Any(s => s.PanelUserId == member.UserId && s.IsValid))
            return Result.Failure("This panel member has submitted a scorecard, so they took part.");
        if (attendance != PanelAttendance.NotRecorded && (await HodIdsAsync(i, ct)).Contains(member.UserId))
            return Result.Failure("The Head of Department's scorecard is required, so they cannot be marked absent or excused. Reschedule the session if they could not attend.");
        note = Clean(note);
        if (attendance != PanelAttendance.NotRecorded && (note is null || note.Length < 5))
            return Result.Failure("Give a short reason (at least 5 characters).");
        if (note?.Length > 1000) return Result.Failure("The reason must be 1,000 characters or fewer.");

        MarkAttendance(member, attendance, attendance == PanelAttendance.NotRecorded ? null : note, now);
        InterviewWorkflow.RefreshValidationStatus(i, now);
        i.Version = Guid.NewGuid();
        return await SaveAsync(ct);
    }

    /// <summary>How many sessions wait for the current user to validate (for the menu and the Interviews tab).</summary>
    public async Task<int> CountAwaitingMyValidationAsync(CancellationToken ct = default) =>
        (await ListAwaitingMyValidationAsync(ct)).Count;

    /// <summary>
    /// Sessions waiting for the current user to validate (oldest first): all scores in, and the vacancy is theirs, or its owner is
    /// not a Recruitment Officer (then every Recruitment Officer is asked). Every waiting session is on the "To validate" tab.
    /// </summary>
    public async Task<IReadOnlyList<InterviewListItemDto>> ListAwaitingMyValidationAsync(CancellationToken ct = default)
    {
        if (!me.IsInRole(InterviewWorkflow.ValidatorRole) || me.UserId is null) return [];
        var waiting = await interviews.SearchAsync(new InterviewFilter { Status = InterviewStatus.AwaitingValidation, PageSize = 100 },
            await scope.VisibilityAsync(ct), null, false, DateTime.UtcNow, ct);
        var mine = new List<InterviewListItemDto>();
        foreach (var item in waiting.Items)
        {
            var i = await interviews.GetWithDetailsAsync(item.Id, ct);
            if (i is null) continue;
            var owner = await VacancyValidatorAsync(i.VacancyId, ct);
            if (owner is null || owner == me.UserId) mine.Add(item);
        }
        return mine;
    }

    /// <summary>The vacancy's owner when they are an active Recruitment Officer (the named validator); otherwise null.</summary>
    private async Task<string?> VacancyValidatorAsync(int vacancyId, CancellationToken ct)
    {
        var owner = (await vacancies.GetByIdAsync(vacancyId, ct))?.RecruiterUserId;
        return !string.IsNullOrEmpty(owner) && await users.IsActiveUserInRoleAsync(owner, InterviewWorkflow.ValidatorRole, ct) ? owner : null;
    }

    /// <summary>Why the scores cannot be validated now, or null when they can.</summary>
    private static string? ValidationProblem(Interview i, DateTime now, IReadOnlyDictionary<string, string> names)
    {
        if (i.Status == InterviewStatus.Completed) return "The scores have already been validated.";
        if (!InterviewWorkflow.IsOpen(i.Status)) return "This session is closed.";
        if (now < i.StartsAt) return "The session has not started yet.";
        var missing = InterviewWorkflow.MissingScorers(i);
        if (missing.Count > 0)
            return $"Waiting for scorecards from {string.Join(", ", missing.Select(p => names.GetValueOrDefault(p.UserId) ?? "?"))}. " +
                   "Ask them to submit, or mark a member who did not take part as absent or excused.";
        if (!i.Scorecards.Any(s => s.IsValid)) return "No scorecards have been submitted yet.";
        return null;
    }

    private void MarkAttendance(InterviewPanelMember member, PanelAttendance attendance, string? note, DateTime now)
    {
        var cleared = attendance == PanelAttendance.NotRecorded;
        member.Attendance = attendance;
        member.AttendanceNote = note;
        member.AttendanceRecordedAt = cleared ? null : now;
        member.AttendanceRecordedByUserId = cleared ? null : me.UserId;
    }

    /// <summary>
    /// Emails that every scorecard is in and the scores are ready to validate: the vacancy's Recruitment Officer, or, when the
    /// vacancy's owner is not a Recruitment Officer, every active Recruitment Officer.
    /// </summary>
    private async Task NotifyReadyForValidationAsync(Interview i, CancellationToken ct)
    {
        var v = await vacancies.GetByIdAsync(i.VacancyId, ct);
        var app = await applications.GetByIdAsync(i.JobApplicationId, ct);
        var owner = await VacancyValidatorAsync(i.VacancyId, ct);
        var to = owner is not null
            ? [owner]
            : (await users.GetActiveUsersInRoleAsync(InterviewWorkflow.ValidatorRole, ct)).Select(u => u.Value).ToList();
        foreach (var userId in to)
            await mailer.ToUserAsync(userId, $"Scores ready to validate: {app?.FullName}",
                $"Every panel member has submitted their scorecard for the {WorkflowMailer.Enc(EnumText.Label(i.Type).ToLowerInvariant())} with " +
                $"<b>{WorkflowMailer.Enc(app?.FullName)}</b> for <b>{WorkflowMailer.Enc(v?.JobTitle)}</b> (round {i.Round})." +
                "<p>Please check the scorecards and validate them, or return one to its panel member for correction. " +
                "The Head of Department and the interview results see the scores only once they are validated.</p>",
                links.Interview(i.Id), "Check and validate the scores", ct);
    }

    // ===================================================================== Closing a session

    public Task<Result> CancelAsync(int id, string? reason, CancellationToken ct = default) =>
        CloseAsync(id, InterviewStatus.Cancelled, ApplicationAction.InterviewCancelled, reason, ct);

    public Task<Result> NoShowAsync(int id, CancellationToken ct = default) =>
        CloseAsync(id, InterviewStatus.NoShow, ApplicationAction.InterviewNoShow, null, ct);

    private async Task<Result> CloseAsync(int id, InterviewStatus status, ApplicationAction action, string? reason, CancellationToken ct)
    {
        var (i, error) = await LoadForManagerAsync(id, ct);
        if (i is null) return Result.Failure(error!);
        reason = Clean(reason);
        if (status == InterviewStatus.Cancelled && (reason is null || reason.Length < 5)) return Result.Failure("Give the reason for cancelling.");
        if (reason?.Length > 2000) return Result.Failure("The reason must be 2,000 characters or fewer.");
        if (status == InterviewStatus.NoShow && DateTime.UtcNow < i.StartsAt)
            return Result.Failure("The session has not started yet.");

        var now = DateTime.UtcNow;
        i.Status = status;
        i.StatusReason = reason;
        i.Version = Guid.NewGuid();
        var app = (await applications.GetWithHistoryAsync(i.JobApplicationId, ct))!;
        app.RecordStep(action, app.Status, MyId, scope.UserName, $"Round {i.Round}{(reason is null ? "" : $": {reason}")}", now);

        var saved = await SaveAsync(ct);
        if (saved.Succeeded && status == InterviewStatus.Cancelled)
        {
            await mailer.TemplateToUserAsync(app.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.InterviewCancelled,
                new Dictionary<string, string?> { ["Kind"] = EnumText.Label(i.Type).ToLowerInvariant(), ["When"] = i.StartsAt.ToString("d MMMM yyyy, HH:mm", Gb) },
                links.MyApplication(app.Id), ct: ct);
            await mailer.TextAsync(app.Phone, $"GREL: your {EnumText.Label(i.Type).ToLowerInvariant()} on {i.StartsAt.ToString("ddd d MMM HH:mm", Gb)} has been cancelled. We will contact you.", ct);
            foreach (var m in i.Panel)
                await mailer.ToUserAsync(m.UserId, $"Cancelled: {app.FullName}, {i.StartsAt:dd MMM HH:mm}",
                    $"The session has been cancelled: {WorkflowMailer.Enc(reason)}", links.Interview(i.Id), "Open the interview", ct);
        }
        return saved;
    }

    // ===================================================================== Reading

    public async Task<PagedResult<InterviewListItemDto>> SearchAsync(InterviewFilter filter, CancellationToken ct = default)
    {
        var visibility = await scope.VisibilityAsync(ct);
        var mineOnly = filter.Tab is "mine" or "scorecards" || !(SeesEverything || me.IsInRole(Roles.HeadOfDepartment));
        if (filter.Tab == "upcoming") filter.Status = InterviewStatus.Scheduled;
        if (filter.Tab == "validate") filter.Status = InterviewStatus.AwaitingValidation; // score validation (9 Nov 2026)
        return await interviews.SearchAsync(filter, visibility, mineOnly ? MyId : null, filter.Tab == "scorecards", DateTime.UtcNow, ct);
    }

    /// <summary>How many sessions are waiting for the current user's scorecard (for the menu and Home page).</summary>
    public async Task<int> CountMyPendingScorecardsAsync(CancellationToken ct = default) =>
        (await interviews.SearchAsync(new InterviewFilter { Tab = "scorecards", PageSize = 1 }, await scope.VisibilityAsync(ct), MyId, true, DateTime.UtcNow, ct)).TotalCount;

    public async Task<InterviewDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var i = await interviews.GetWithDetailsAsync(id, ct);
        if (i is null) return null;
        var isPanel = i.Panel.Any(p => p.UserId == me.UserId);
        var isHod = await IsHodOfAsync(i.DepartmentId, ct);
        if (!isPanel && !isHod && !SeesEverything) return null;

        var app = (await applications.GetByIdAsync(i.JobApplicationId, ct))!;
        var v = (await vacancies.GetByIdAsync(i.VacancyId, ct))!;
        var names = await users.GetDisplayNamesAsync(
            i.Panel.Select(p => p.UserId).Concat(new[] { v.RecruiterUserId, i.ValidatedByUserId })
                .Concat(i.Scorecards.Select(s => s.ReturnedByUserId)).OfType<string>().Where(x => x.Length > 0).Distinct(), ct);
        var mine = i.Scorecards.FirstOrDefault(s => s.PanelUserId == me.UserId);
        var now = DateTime.UtcNow;

        // Who sees the scorecards (score validation, 9 Nov 2026):
        //   * panel members, once they have submitted their own valid scorecard (independent scoring);
        //   * Recruitment Officers, who validate them;
        //   * once validated: also reviewers, the department's HOD, the MD and HR Admin.
        var validated = i.Status == InterviewStatus.Completed;
        var isRecruitmentOfficer = me.IsInRole(InterviewWorkflow.ValidatorRole);
        var seeAll = (mine is not null && mine.IsValid) || isRecruitmentOfficer || (validated && (SeesEverything || isHod));
        var visibleCards = validated ? i.Scorecards.Where(s => s.IsValid).ToList() : i.Scorecards.ToList();
        ScorecardDto ToDto(Scorecard s) => new(s.PanelUserId, names.GetValueOrDefault(s.PanelUserId) ?? "?", s.Recommendation, s.WeightedScore,
            s.Ratings.ToDictionary(r => r.CriterionId, r => r.Score), s.Strengths, s.Concerns, s.SubmittedAt,
            s.ReturnedAt, s.ReturnReason, s.ReturnedByUserId is null ? null : names.GetValueOrDefault(s.ReturnedByUserId));
        var validCards = visibleCards.Where(s => s.IsValid).ToList();

        var isValidator = isRecruitmentOfficer && app.ApplicantUserId != me.UserId && InterviewWorkflow.IsOpen(i.Status);
        var namedValidator = await VacancyValidatorAsync(i.VacancyId, ct);
        var validationProblem = isValidator ? ValidationProblem(i, now, names) : null;

        return new InterviewDetailsDto
        {
            Id = i.Id,
            ApplicationId = app.Id,
            ApplicationNumber = app.ApplicationNumber,
            ApplicantName = app.FullName,
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            JobTitle = v.JobTitle,
            DepartmentName = (await departments.GetByIdAsync(i.DepartmentId, ct))?.Name ?? "",
            Round = i.Round,
            Type = i.Type,
            Mode = i.Mode,
            StartsAt = i.StartsAt,
            DurationMinutes = i.DurationMinutes,
            Location = i.Location,
            CandidateInstructions = i.CandidateInstructions,
            Status = i.Status,
            StatusReason = i.StatusReason,
            Panel = i.Panel.OrderByDescending(p => p.IsChair).ThenBy(p => names.GetValueOrDefault(p.UserId))
                .Select(p => new PanelMemberDto(p.UserId, names.GetValueOrDefault(p.UserId) ?? "?", p.IsChair,
                    i.Scorecards.Any(s => s.PanelUserId == p.UserId && s.IsValid),
                    i.Scorecards.Any(s => s.PanelUserId == p.UserId && !s.IsValid),
                    p.Attendance, p.AttendanceNote)).ToList(),
            Criteria = i.Criteria.OrderBy(c => c.SortOrder).Select(c => new CriterionDto(c.Id, c.Name, c.Weight)).ToList(),
            MyScorecard = mine is null ? null : ToDto(mine),
            MyScorecardReturned = mine is not null && !mine.IsValid && InterviewWorkflow.IsOpen(i.Status),
            Scorecards = seeAll ? visibleCards.OrderBy(s => s.SubmittedAt).Select(ToDto).ToList() : [],
            ScorecardsHidden = !seeAll && i.Scorecards.Count > 0,
            AverageScore = seeAll && validCards.Count > 0 ? Math.Round(validCards.Average(s => s.WeightedScore), 1) : null,
            IsPanelMember = isPanel,
            CanScore = isPanel && InterviewWorkflow.CanScore(i, now),
            ScoreNotYetReason = isPanel && InterviewWorkflow.IsOpen(i.Status) && now < i.StartsAt ? "Your scorecard opens when the session starts." : null,
            CanManage = IsScheduler && InterviewWorkflow.CanClose(i.Status),
            ValidatorName = namedValidator is null ? null : names.GetValueOrDefault(namedValidator),
            MissingScorers = InterviewWorkflow.IsOpen(i.Status) && now >= i.StartsAt
                ? InterviewWorkflow.MissingScorers(i).Select(p => names.GetValueOrDefault(p.UserId) ?? "?").ToList()
                : [],
            IsValidator = isValidator,
            CanValidate = isValidator && validationProblem is null,
            ValidationBlockedReason = validationProblem,
            ValidatedByName = i.ValidatedByUserId is null ? null : names.GetValueOrDefault(i.ValidatedByUserId),
            ValidatedAt = i.ValidatedAt,
            ValidationNote = i.ValidationNote
        };
    }

    /// <summary>
    /// The interview results of a vacancy: every applicant who reached the interview stage, with their sessions,
    /// average scorecard score and the panel's recommendations, best first. For reviewers, the department's HOD, MD and HR Admin.
    /// </summary>
    public async Task<(string VacancyNumber, string JobTitle, IReadOnlyList<InterviewResultDto> Results)?> GetResultsAsync(int vacancyId, CancellationToken ct = default)
    {
        var v = await vacancies.GetByIdAsync(vacancyId, ct);
        if (v is null || !(SeesEverything || await IsHodOfAsync(v.DepartmentId, ct))) return null;

        var apps = await applications.ListAsync(a => a.VacancyId == vacancyId, ct);
        var sessions = await interviews.ListForApplicationsAsync(apps.Select(a => a.Id).ToList(), ct);
        var results = apps
            .Where(a => sessions.Any(s => s.JobApplicationId == a.Id))
            .Select(a =>
            {
                // Only validated scores count (9 Nov 2026): a session awaiting validation shows no score yet.
                var mine = sessions.Where(s => s.JobApplicationId == a.Id).OrderBy(s => s.Round).ToList();
                var cards = mine.SelectMany(InterviewWorkflow.ValidatedScorecards).ToList();
                return new InterviewResultDto(a.Id, a.ApplicationNumber, a.FullName, a.Status, a.ScreeningScore,
                    mine.Select(s =>
                    {
                        var valid = InterviewWorkflow.ValidatedScorecards(s).ToList();
                        return (s.Id, s.Round, s.Type, s.Status,
                            valid.Count == 0 ? (decimal?)null : Math.Round(valid.Average(c => c.WeightedScore), 1),
                            s.Scorecards.Count(c => c.IsValid), s.Panel.Count);
                    }).ToList(),
                    cards.Count == 0 ? null : Math.Round(cards.Average(c => c.WeightedScore), 1),
                    cards.GroupBy(c => c.Recommendation).ToDictionary(g => g.Key, g => g.Count()));
            })
            .OrderByDescending(r => r.OverallAverage ?? -1)
            .ToList();
        return (v.VacancyNumber, v.JobTitle, results);
    }

    /// <summary>The sessions of one of the current user's applications (date, place, instructions only).</summary>
    public async Task<IReadOnlyList<ApplicantInterviewDto>> ForMyApplicationAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await applications.GetByIdAsync(applicationId, ct);
        if (app is null || app.ApplicantUserId != MyId) return [];
        return (await interviews.ListAsync(i => i.JobApplicationId == applicationId, ct))
            .OrderBy(i => i.StartsAt)
            .Select(i => new ApplicantInterviewDto(i.Type, i.Mode, i.StartsAt, i.DurationMinutes, i.Location, i.CandidateInstructions, i.Status))
            .ToList();
    }

    /// <summary>True when the current user may schedule a session for this application now.</summary>
    public async Task<bool> CanScheduleForAsync(int applicationId, CancellationToken ct = default) =>
        (await LoadApplicationForSchedulingAsync(applicationId, ct)).Application is not null;

    // ===================================================================== Helpers

    private async Task<(JobApplication? Application, string? Error)> LoadApplicationForSchedulingAsync(int applicationId, CancellationToken ct)
    {
        if (!IsScheduler) return (null, "Only a Recruitment Officer or HR Manager can schedule interviews.");
        var app = await applications.GetWithHistoryAsync(applicationId, ct);
        if (app is null) return (null, "Application not found.");
        if (app.ApplicantUserId == MyId) return (null, "This is your own application.");
        if (!InterviewWorkflow.CanSchedule(app.Status)) return (null, "Only shortlisted applicants can be invited to an interview or assessment.");
        return (app, null);
    }

    private async Task<(Interview? Interview, string? Error)> LoadForManagerAsync(int id, CancellationToken ct)
    {
        if (!IsScheduler) return (null, "Only a Recruitment Officer or HR Manager can do this.");
        var i = await interviews.GetWithDetailsAsync(id, ct);
        if (i is null) return (null, "Interview not found.");
        var app = await applications.GetByIdAsync(i.JobApplicationId, ct);
        if (app?.ApplicantUserId == MyId) return (null, "This is your own application.");
        return InterviewWorkflow.CanClose(i.Status) ? (i, null) : (null, "This session is already closed.");
    }

    /// <summary>
    /// An open session the current user may validate: any Recruitment Officer who is not the applicant, also one who sat on the
    /// panel (user's choice, 9 Nov 2026); they just cannot return their own scorecard.
    /// </summary>
    private async Task<(Interview? Interview, string? Error)> LoadForValidatorAsync(int id, CancellationToken ct)
    {
        if (!me.IsInRole(InterviewWorkflow.ValidatorRole)) return (null, "Only a Recruitment Officer can validate interview scores.");
        var i = await interviews.GetWithDetailsAsync(id, ct);
        if (i is null) return (null, "Interview not found.");
        var app = await applications.GetByIdAsync(i.JobApplicationId, ct);
        if (app?.ApplicantUserId == MyId) return (null, "This is your own application.");
        if (i.Status == InterviewStatus.Completed) return (null, "The scores have already been validated.");
        return InterviewWorkflow.IsOpen(i.Status) ? (i, null) : (null, "This session is closed.");
    }

    /// <summary>The Head(s) of Department of an interview's department who are on its panel: their scorecard is required.</summary>
    private async Task<IReadOnlyList<string>> HodIdsAsync(Interview i, CancellationToken ct)
    {
        var current = (await departments.GetByIdAsync(i.DepartmentId, ct))?.HeadOfDepartmentUserId;
        return new[] { current, i.RequestingHodUserId }.OfType<string>().Distinct().Where(id => i.Panel.Any(p => p.UserId == id)).ToList();
    }

    /// <summary>The department's Head(s) of Department: the HOD who raised the request and the HOD set on the department.</summary>
    private async Task<IReadOnlyList<string>> RequiredHodsAsync(JobApplication app, CancellationToken ct)
    {
        var current = (await departments.GetByIdAsync(app.DepartmentId, ct))?.HeadOfDepartmentUserId;
        var list = new[] { current, app.RequestingHodUserId }.OfType<string>().Distinct().Where(id => id != app.ApplicantUserId).ToList();
        var active = new List<string>();
        foreach (var id in list) if (await users.IsActiveStaffAsync(id, ct)) active.Add(id);
        return active;
    }

    private async Task<bool> IsHodOfAsync(int departmentId, CancellationToken ct) =>
        me.IsInRole(Roles.HeadOfDepartment) && (await scope.HodDepartmentIdsAsync(ct)).Contains(departmentId);

    /// <summary>Parses "Name | weight" lines. Weights must be whole numbers adding up to 100.</summary>
    private static List<(string Name, int Weight)> ParseCriteria(string? text, List<string> errors)
    {
        var list = new List<(string Name, int Weight)>();
        foreach (var line in (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            var parts = line.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[1].Trim(), out var weight) || weight is < 1 or > 100 || parts[0].Trim().Length is 0 or > 150)
            {
                errors.Add($"Write each criterion as \"name | weight\", e.g. \"Communication | 15\" (problem with: \"{line}\").");
                return list;
            }
            list.Add((parts[0].Trim(), weight));
        }
        if (list.Count is 0 or > 12) errors.Add("Give between 1 and 12 scorecard criteria.");
        else if (list.Sum(c => c.Weight) != 100) errors.Add($"The criteria weights must add up to 100 (they add up to {list.Sum(c => c.Weight)}).");
        if (list.Select(c => c.Name.ToLowerInvariant()).Distinct().Count() != list.Count) errors.Add("Each criterion must have a different name.");
        return list;
    }


    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);
}
