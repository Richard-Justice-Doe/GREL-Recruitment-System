using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Applications;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using Grsms.Domain.Surveys;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Surveys;

public record SurveyFormDto(int InvitationId, SurveyType Type, string JobTitle, IReadOnlyList<SurveyQuestion> Questions, DateOnly ExpiresOn,
    bool IsCompleted, bool IsExpired);

/// <summary>An unanswered survey on one of my applications (for the card on the application page).</summary>
public record MySurveyDto(int InvitationId, SurveyType Type, DateOnly ExpiresOn);

public class SurveyResultsFilter
{
    public SurveyType Type { get; set; } = SurveyType.CandidateExperience;
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }

    /// <summary>Invitations sent from / to.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record QuestionResultDto(string Key, string Text, decimal? Average, int Answers, IReadOnlyList<int> Distribution);
public record SurveyCommentDto(DateTime AnsweredAt, string DepartmentName, ApplicationStatus Outcome, string Text);

public record SurveyResultsDto(int Invited, int Responded, bool EnoughToShow, IReadOnlyList<QuestionResultDto> Questions,
    IReadOnlyList<SurveyCommentDto> Comments)
{
    public int ResponseRate => Invited == 0 ? 0 : Responded * 100 / Invited;
}

/// <summary>
/// Feedback surveys (Module 10).
///
///   * Candidate experience : sent the day after an application ends (not successful, offer declined, hired).
///   * New employee         : sent the day after the onboarding is completed.
/// Invitations go out from the background job, by email and the bell, and can be answered for 30 days.
/// Answers are confidential: they are not in the audit trail, and the results page shows averages and comments without
/// names, only once at least <see cref="SurveyQuestions.MinimumResponsesToShow"/> responses match the filters.
/// Results are open to the HR Manager, Recruitment Officers and the Managing Director.
/// </summary>
public class SurveyService(
    IApplicationRepository applications,
    IRepository<OnboardingRecord> onboardings,
    IRepository<SurveyInvitation> invitations,
    IRepository<SurveyAnswer> answers,
    IRepository<Vacancy> vacancies,
    IRepository<Department> departments,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    WorkflowMailer mailer,
    IAppLinks links)
{
    /// <summary>Roles that may read the results.</summary>
    public static readonly IReadOnlyList<string> ResultRoles = [Roles.HrManager, Roles.RecruitmentOfficer, Roles.ManagingDirector];

    /// <summary>Invitations stay open this many days.</summary>
    public const int AnswerDays = 30;

    // ===================================================================== Invitations (background job)

    /// <summary>Invites everyone whose application ended, or whose onboarding was completed, 1 to 30 days ago. Returns how many.</summary>
    public async Task<int> InviteDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var latest = nowUtc.AddDays(-1);
        var earliest = nowUtc.AddDays(-30);
        var today = DateOnly.FromDateTime(nowUtc);
        var created = new List<(SurveyInvitation Invitation, string JobTitle)>();

        // Candidate experience
        var outcomes = SurveyQuestions.FinalOutcomes.ToList();
        var ended = await applications.ListAsync(a => outcomes.Contains(a.Status) && a.StatusChangedAt <= latest && a.StatusChangedAt >= earliest, ct);
        if (ended.Count > 0)
        {
            var ids = ended.Select(a => a.Id).ToList();
            var already = (await invitations.ListAsync(i => i.Type == SurveyType.CandidateExperience && ids.Contains(i.JobApplicationId), ct))
                .Select(i => i.JobApplicationId).ToHashSet();
            var titles = await TitlesAsync(ended.Select(a => a.VacancyId), ct);
            foreach (var a in ended.Where(a => !already.Contains(a.Id)))
                created.Add((new SurveyInvitation
                {
                    Type = SurveyType.CandidateExperience, UserId = a.ApplicantUserId, JobApplicationId = a.Id, VacancyId = a.VacancyId,
                    DivisionId = a.DivisionId, DepartmentId = a.DepartmentId, RequestingHodUserId = a.RequestingHodUserId, Outcome = a.Status,
                    SentAt = nowUtc, ExpiresOn = today.AddDays(AnswerDays)
                }, titles.GetValueOrDefault(a.VacancyId, "")));
        }

        // New employee
        var completed = await onboardings.ListAsync(o => o.Status == OnboardingStatus.Completed && o.StatusChangedAt <= latest && o.StatusChangedAt >= earliest, ct);
        if (completed.Count > 0)
        {
            var ids = completed.Select(o => o.JobApplicationId).ToList();
            var already = (await invitations.ListAsync(i => i.Type == SurveyType.NewEmployee && ids.Contains(i.JobApplicationId), ct))
                .Select(i => i.JobApplicationId).ToHashSet();
            foreach (var o in completed.Where(o => !already.Contains(o.JobApplicationId)))
                created.Add((new SurveyInvitation
                {
                    Type = SurveyType.NewEmployee, UserId = o.EmployeeUserId, JobApplicationId = o.JobApplicationId, OnboardingRecordId = o.Id,
                    VacancyId = o.VacancyId, DivisionId = o.DivisionId, DepartmentId = o.DepartmentId, RequestingHodUserId = o.RequestingHodUserId,
                    Outcome = ApplicationStatus.Hired, SentAt = nowUtc, ExpiresOn = today.AddDays(AnswerDays)
                }, o.JobTitle));
        }

        if (created.Count == 0) return 0;
        foreach (var (inv, _) in created) invitations.Add(inv);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return 0; }

        foreach (var (inv, title) in created)
            await mailer.ToUserAsync(inv.UserId,
                inv.Type == SurveyType.CandidateExperience ? "How was your experience of applying to GREL?" : "How was your start at GREL?",
                (inv.Type == SurveyType.CandidateExperience
                    ? $"Thank you for applying for <b>{WorkflowMailer.Enc(title)}</b>. Whatever the outcome, we would value your views on how we handled your application."
                    : $"Welcome again to GREL. Please tell us how your first days as <b>{WorkflowMailer.Enc(title)}</b> went, so we can improve for the next new employee.") +
                $" The survey takes about two minutes and your answers are confidential. Please reply by " +
                $"{inv.ExpiresOn.ToString("d MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"))}.",
                links.Survey(inv.Id), "Answer the survey", ct);
        return created.Count;
    }

    // ===================================================================== Answering

    public async Task<SurveyFormDto?> GetFormAsync(int invitationId, CancellationToken ct = default)
    {
        var inv = await invitations.GetByIdAsync(invitationId, ct);
        if (inv is null || inv.UserId != me.UserId) return null;
        var title = (await vacancies.GetByIdAsync(inv.VacancyId, ct))?.JobTitle ?? "";
        return new SurveyFormDto(inv.Id, inv.Type, title, SurveyQuestions.For(inv.Type), inv.ExpiresOn, inv.CompletedAt is not null, IsExpired(inv));
    }

    /// <summary>Unanswered, unexpired surveys on one of my applications.</summary>
    public async Task<IReadOnlyList<MySurveyDto>> OpenForApplicationAsync(int applicationId, CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return [];
        return (await invitations.ListAsync(i => i.JobApplicationId == applicationId && i.UserId == uid && i.CompletedAt == null, ct))
            .Where(i => !IsExpired(i)).Select(i => new MySurveyDto(i.Id, i.Type, i.ExpiresOn)).ToList();
    }

    /// <param name="ratings">Question key → 1-5, or null for "not applicable".</param>
    /// <param name="texts">Question key → free text.</param>
    public async Task<Result> SubmitAsync(int invitationId, IReadOnlyDictionary<string, int?> ratings, IReadOnlyDictionary<string, string?> texts,
        CancellationToken ct = default)
    {
        var inv = await invitations.GetByIdAsync(invitationId, ct);
        if (inv is null || inv.UserId != me.UserId) return Result.Failure("Survey not found.");
        if (inv.CompletedAt is not null) return Result.Failure("You have already answered this survey. Thank you!");
        if (IsExpired(inv)) return Result.Failure("This survey has closed.");

        var questions = SurveyQuestions.For(inv.Type);
        foreach (var q in questions)
        {
            if (q.IsRating)
            {
                var r = ratings.GetValueOrDefault(q.Key);
                if (r is not null and (< 1 or > 5)) return Result.Failure("Each rating must be between 1 and 5.");
                if (q.Required && r is null) return Result.Failure("Please give an overall rating.");
                if (r is not null) answers.Add(new SurveyAnswer { SurveyInvitationId = inv.Id, QuestionKey = q.Key, Rating = r });
            }
            else
            {
                var text = texts.GetValueOrDefault(q.Key)?.Trim();
                if (text?.Length > 4000) return Result.Failure("Please keep your comments under 4,000 characters.");
                if (!string.IsNullOrEmpty(text)) answers.Add(new SurveyAnswer { SurveyInvitationId = inv.Id, QuestionKey = q.Key, Text = text });
            }
        }
        inv.CompletedAt = DateTime.UtcNow;
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure("Your answers could not be saved. Please try again.");
        }
    }

    // ===================================================================== Results

    /// <summary>Response rate, average per question and comments (no names). Null if the user may not see results.</summary>
    public async Task<SurveyResultsDto?> ResultsAsync(SurveyResultsFilter filter, CancellationToken ct = default)
    {
        if (!ResultRoles.Any(me.IsInRole)) return null;
        IEnumerable<SurveyInvitation> q = await invitations.ListAsync(i => i.Type == filter.Type, ct);
        if (filter.DivisionId is { } div) q = q.Where(i => i.DivisionId == div);
        if (filter.DepartmentId is { } dep) q = q.Where(i => i.DepartmentId == dep);
        if (filter.From is { } from) q = q.Where(i => DateOnly.FromDateTime(i.SentAt) >= from);
        if (filter.To is { } to) q = q.Where(i => DateOnly.FromDateTime(i.SentAt) <= to);
        var list = q.ToList();
        var done = list.Where(i => i.CompletedAt is not null).ToDictionary(i => i.Id);
        var enough = done.Count >= SurveyQuestions.MinimumResponsesToShow;
        if (!enough) return new SurveyResultsDto(list.Count, done.Count, false, [], []);

        var ids = done.Keys.ToList();
        var all = await answers.ListAsync(a => ids.Contains(a.SurveyInvitationId), ct);
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        var questions = SurveyQuestions.For(filter.Type);

        var results = questions.Where(x => x.IsRating).Select(x =>
        {
            var given = all.Where(a => a.QuestionKey == x.Key && a.Rating is not null).Select(a => a.Rating!.Value).ToList();
            return new QuestionResultDto(x.Key, x.Text, given.Count == 0 ? null : Math.Round((decimal)given.Average(), 1), given.Count,
                Enumerable.Range(1, 5).Select(n => given.Count(g => g == n)).ToList());
        }).ToList();
        var comments = all.Where(a => a.Text is not null)
            .Select(a => new SurveyCommentDto(done[a.SurveyInvitationId].CompletedAt!.Value, deptNames.GetValueOrDefault(done[a.SurveyInvitationId].DepartmentId, ""),
                done[a.SurveyInvitationId].Outcome, a.Text!))
            .OrderByDescending(c => c.AnsweredAt).ToList();
        return new SurveyResultsDto(list.Count, done.Count, true, results, comments);
    }

    private static bool IsExpired(SurveyInvitation inv) => inv.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<Dictionary<int, string>> TitlesAsync(IEnumerable<int> vacancyIds, CancellationToken ct)
    {
        var ids = vacancyIds.Distinct().ToList();
        return (await vacancies.ListAsync(v => ids.Contains(v.Id), ct)).ToDictionary(v => v.Id, v => v.JobTitle);
    }
}
