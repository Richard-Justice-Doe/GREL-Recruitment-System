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

namespace Grsms.Application.Interviews;

/// <summary>
/// Assessments, interviews and scorecards (Module 7). See <see cref="InterviewWorkflow"/> for the rules.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Schedule / reschedule / complete / cancel / record no-show : Recruitment Officer or HR Manager.
///   * Submit a scorecard                                        : members of that interview's panel.
///   * See a session                                             : its panel; reviewers; the department's HOD; MD; HR Admin.
///   * See all scorecards                                        : reviewers, the department's HOD, MD, HR Admin, and panel
///                                                                 members who have submitted their own.
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
    public async Task<Result> RescheduleAsync(int id, DateTime? startsAt, string? location, CancellationToken ct = default)
    {
        var (i, error) = await LoadForManagerAsync(id, ct);
        if (i is null) return Result.Failure(error!);
        if (i.Scorecards.Count > 0) return Result.Failure("Scorecards have already been submitted for this session.");
        if (startsAt is not { } when || when < DateTime.UtcNow) return Result.Failure("Choose a new date and time in the future.");
        location = Clean(location) ?? i.Location;
        if (location.Length > 500) return Result.Failure("The place must be 500 characters or fewer.");

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

        var card = i.Scorecards.FirstOrDefault(s => s.PanelUserId == MyId);
        if (card is null)
        {
            card = new Scorecard { InterviewId = i.Id, PanelUserId = MyId };
            i.Scorecards.Add(card);
        }
        card.Recommendation = recommendation.Value;
        card.Strengths = strengths;
        card.Concerns = concerns;
        card.SubmittedAt = DateTime.UtcNow;
        card.Ratings.Clear();
        foreach (var c in i.Criteria) card.Ratings.Add(new ScorecardRating { CriterionId = c.Id, Score = ratings[c.Id] });
        card.WeightedScore = InterviewWorkflow.WeightedScore(i.Criteria.Select(c => (ratings[c.Id], c.Weight)));
        i.Version = Guid.NewGuid();
        return await SaveAsync(ct);
    }

    // ===================================================================== Closing a session

    public Task<Result> CompleteAsync(int id, CancellationToken ct = default) =>
        CloseAsync(id, InterviewStatus.Completed, ApplicationAction.InterviewCompleted, null, ct);

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
        if (status is InterviewStatus.Completed or InterviewStatus.NoShow && DateTime.UtcNow < i.StartsAt)
            return Result.Failure("The session has not started yet.");
        if (status == InterviewStatus.Completed && i.Scorecards.Count == 0)
            return Result.Failure("No scorecards have been submitted yet. Ask the panel to submit theirs first.");

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
        var names = await users.GetDisplayNamesAsync(i.Panel.Select(p => p.UserId), ct);
        var mine = i.Scorecards.FirstOrDefault(s => s.PanelUserId == me.UserId);

        // Panel members see the others' scorecards only after submitting their own (independent scoring).
        var seeAll = SeesEverything || isHod || mine is not null;
        ScorecardDto ToDto(Scorecard s) => new(s.PanelUserId, names.GetValueOrDefault(s.PanelUserId) ?? "?", s.Recommendation, s.WeightedScore,
            s.Ratings.ToDictionary(r => r.CriterionId, r => r.Score), s.Strengths, s.Concerns, s.SubmittedAt);
        var now = DateTime.UtcNow;

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
                    i.Scorecards.Any(s => s.PanelUserId == p.UserId))).ToList(),
            Criteria = i.Criteria.OrderBy(c => c.SortOrder).Select(c => new CriterionDto(c.Id, c.Name, c.Weight)).ToList(),
            MyScorecard = mine is null ? null : ToDto(mine),
            Scorecards = seeAll ? i.Scorecards.OrderBy(s => s.SubmittedAt).Select(ToDto).ToList() : [],
            ScorecardsHidden = !seeAll && i.Scorecards.Count > 0,
            AverageScore = seeAll && i.Scorecards.Count > 0 ? Math.Round(i.Scorecards.Average(s => s.WeightedScore), 1) : null,
            IsPanelMember = isPanel,
            CanScore = isPanel && InterviewWorkflow.CanScore(i, now),
            ScoreNotYetReason = isPanel && i.Status == InterviewStatus.Scheduled && now < i.StartsAt ? "Your scorecard opens when the session starts." : null,
            CanManage = IsScheduler && InterviewWorkflow.CanClose(i.Status)
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
                var mine = sessions.Where(s => s.JobApplicationId == a.Id).OrderBy(s => s.Round).ToList();
                var cards = mine.SelectMany(s => s.Scorecards).ToList();
                return new InterviewResultDto(a.Id, a.ApplicationNumber, a.FullName, a.Status, a.ScreeningScore,
                    mine.Select(s => (s.Id, s.Round, s.Type, s.Status,
                        s.Scorecards.Count == 0 ? (decimal?)null : Math.Round(s.Scorecards.Average(c => c.WeightedScore), 1),
                        s.Scorecards.Count, s.Panel.Count)).ToList(),
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
