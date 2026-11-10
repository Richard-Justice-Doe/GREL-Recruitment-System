using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Vacancies;
using Grsms.Domain.Applications;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Shortlisting;

/// <summary>
/// CV screening and shortlisting (Module 6).
///
///   1. Screening   : every application is scored by the ATS when it is submitted (Phase 15, see <see cref="AtsService"/>);
///                    reviewers can re-run it for a whole vacancy.
///   2. Recommend   : a Recruitment Officer or HR Manager recommends "shortlist" or "do not shortlist" (with a note),
///                    then sends the recommendations to the department's Head of Department.
///   3. HOD's view  : the HOD of the vacancy's department agrees or disagrees with each recommendation (a note is
///                    required when they disagree).
///   4. Shortlist   : a Recruitment Officer or HR Manager shortlists. If the HOD has not agreed, only an HR Manager can,
///                    and must record why (an override). The applicant is emailed.
///   Not taking an application further still uses "Mark not successful" (Phase 5).
///
/// Nobody acts on their own application. Every step is in the application's history and the audit trail.
/// </summary>
public class ShortlistingService(
    IApplicationRepository applications,
    IVacancyRepository vacancies,
    IRepository<Department> departments,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links,
    AtsService ats)
{
    private const string ConflictMessage = "Someone else updated this application at the same moment. Please reload the page and try again.";

    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool IsReviewer => ApplicationWorkflow.ReviewerRoles.Any(me.IsInRole);
    private bool CanOverride => me.IsInRole(ApplicationWorkflow.OverrideRole);

    // ===================================================================== Board

    /// <summary>The ranked shortlisting board of a vacancy, or null if the user may not see it.</summary>
    public async Task<ShortlistBoardDto?> GetBoardAsync(int vacancyId, CancellationToken ct = default)
    {
        var v = await vacancies.GetWithDetailsAsync(vacancyId, ct);
        if (v is null) return null;
        var isHod = await IsHodOfAsync(v.DepartmentId, ct);
        if (!IsReviewer && !isHod && !me.HasPermission(Permissions.SeeAllDepartments)) return null;

        var apps = await applications.ListAsync(a => a.VacancyId == vacancyId && a.Status != ApplicationStatus.Withdrawn, ct);
        var rows = apps
            .OrderByDescending(a => a.MeetsMinimum == true)
            .ThenByDescending(a => a.ScreeningScore ?? -1)
            .ThenBy(a => a.SubmittedAt)
            .Select(a =>
            {
                var own = a.ApplicantUserId == me.UserId;
                return new ShortlistRowDto(a.Id, a.ApplicationNumber, a.FullName, a.IsInternal, a.Status, a.ScreeningScore, a.MeetsMinimum,
                    a.ReadScreening()?.AllMissingRequired ?? [], a.HighestEducation, a.YearsExperience,
                    a.RecruiterRecommendation, a.RecruiterNote, a.HodRecommendation, a.HodNote, own,
                    CanRecommend: IsReviewer && !own && ApplicationWorkflow.CanRecommend(a.Status),
                    CanHodReview: isHod && !own && ApplicationWorkflow.CanHodReview(a),
                    CanShortlist: IsReviewer && !own && ApplicationWorkflow.CanShortlist(a.Status) &&
                                  (!ApplicationWorkflow.NeedsOverride(a) || CanOverride),
                    NeedsOverride: ApplicationWorkflow.NeedsOverride(a));
            })
            .ToList();

        return new ShortlistBoardDto
        {
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            JobTitle = v.JobTitle,
            DepartmentName = (await departments.GetByIdAsync(v.DepartmentId, ct))?.Name ?? "",
            VacancyStatus = v.Status,
            ClosingDate = v.ClosingDate,
            NumberOfPositions = v.NumberOfPositions,
            MinimumEducation = v.MinimumEducation,
            MinYearsExperience = v.MinYearsExperience,
            EssentialItems = v.Requirements.Where(r => r.IsEssential).Select(r => r.Name).ToList(),
            DesirableItems = v.Requirements.Where(r => !r.IsEssential).Select(r => r.Name).ToList(),
            Rows = rows,
            IsReviewer = IsReviewer,
            IsHod = isHod,
            CanOverride = CanOverride,
            AwaitingHod = apps.Count(a => ApplicationWorkflow.CanHodReview(a) && a.HodRecommendation == ShortlistRecommendation.None),
            Shortlisted = apps.Count(a => a.Status == ApplicationStatus.Shortlisted)
        };
    }

    // ===================================================================== Screening

    /// <summary>Re-runs the ATS (Phase 15) for the vacancy's applications still at the screening stage. Returns how many were scored.</summary>
    public Task<Result<int>> ScreenVacancyAsync(int vacancyId, CancellationToken ct = default) => ats.RescreenVacancyAsync(vacancyId, ct);

    // ===================================================================== Steps

    /// <summary>The recruiter's recommendation. Moves a newly received application to "Under review".</summary>
    public async Task<Result> RecommendAsync(int applicationId, ShortlistRecommendation recommendation, string? note, CancellationToken ct = default)
    {
        if (!IsReviewer) return Result.Failure("Only a Recruitment Officer or HR Manager can recommend.");
        var a = await applications.GetWithHistoryAsync(applicationId, ct);
        if (a is null) return Result.Failure("Application not found.");
        if (a.ApplicantUserId == MyId) return Result.Failure("This is your own application, so another reviewer must handle it.");
        if (!ApplicationWorkflow.CanRecommend(a.Status)) return Result.Failure("Recommendations can only be made while the application is being reviewed.");
        if (recommendation == ShortlistRecommendation.None) return Result.Failure("Choose shortlist or do not shortlist.");
        note = Clean(note);
        if (recommendation == ShortlistRecommendation.DoNotShortlist && (note is null || note.Length < 5))
            return Result.Failure("Explain why you recommend not shortlisting (the HOD will see this).");
        if (note?.Length > 2000) return Result.Failure("The note must be 2,000 characters or fewer.");

        var now = DateTime.UtcNow;
        if (a.Status == ApplicationStatus.Submitted)
            a.RecordStep(ApplicationAction.ReviewStarted, ApplicationStatus.UnderReview, MyId, scope.UserName, null, now);

        a.RecruiterRecommendation = recommendation;
        a.RecruiterNote = note;
        a.RecruiterRecommendedByUserId = MyId;
        a.RecruiterRecommendedAt = now;
        // A changed recommendation needs the HOD to look again.
        a.HodRecommendation = ShortlistRecommendation.None;
        a.HodNote = null;
        a.HodRecommendedByUserId = null;
        a.HodRecommendedAt = null;
        a.RecordStep(ApplicationAction.Recommended, a.Status, MyId, scope.UserName, $"{EnumText.Label(recommendation)}{(note is null ? "" : $": {note}")}", now);
        return await SaveAsync(ct);
    }

    /// <summary>Emails the department's Head of Department that recommendations are waiting for their view.</summary>
    public async Task<Result<int>> SendToHodAsync(int vacancyId, CancellationToken ct = default)
    {
        if (!IsReviewer) return Result.Failure<int>("Only a Recruitment Officer or HR Manager can do this.");
        var v = await vacancies.GetByIdAsync(vacancyId, ct);
        if (v is null) return Result.Failure<int>("Vacancy not found.");
        var waiting = (await applications.ListAsync(a => a.VacancyId == vacancyId && a.Status == ApplicationStatus.UnderReview &&
            a.RecruiterRecommendation != ShortlistRecommendation.None && a.HodRecommendation == ShortlistRecommendation.None, ct)).Count;
        if (waiting == 0) return Result.Failure<int>("There are no recommendations waiting for the Head of Department.");

        var hods = new[] { v.RequestingHodUserId, (await departments.GetByIdAsync(v.DepartmentId, ct))?.HeadOfDepartmentUserId }
            .OfType<string>().Distinct();
        foreach (var hod in hods)
            await mailer.ToUserAsync(hod, $"Shortlist for {v.VacancyNumber} needs your view",
                $"{WorkflowMailer.Enc(scope.UserName)} has made shortlisting recommendations on <b>{waiting}</b> application(s) for " +
                $"<b>{WorkflowMailer.Enc(v.JobTitle)}</b>. Please agree or disagree with each one.",
                links.Shortlist(v.Id), "Open the shortlisting board", ct);
        return Result.Success(waiting);
    }

    /// <summary>The Head of Department's view on the recruiter's recommendation.</summary>
    public async Task<Result> HodReviewAsync(int applicationId, ShortlistRecommendation view, string? note, CancellationToken ct = default)
    {
        var a = await applications.GetWithHistoryAsync(applicationId, ct);
        if (a is null || !await IsHodOfAsync(a.DepartmentId, ct))
            return Result.Failure("Only the Head of this department can give this view.");
        if (a.ApplicantUserId == MyId) return Result.Failure("This is your own application.");
        if (!ApplicationWorkflow.CanHodReview(a)) return Result.Failure("There is no recruiter recommendation to respond to yet.");
        if (view == ShortlistRecommendation.None) return Result.Failure("Choose shortlist or do not shortlist.");
        note = Clean(note);
        if (view != a.RecruiterRecommendation && (note is null || note.Length < 5))
            return Result.Failure("You disagree with the recruiter, so please explain why.");
        if (note?.Length > 2000) return Result.Failure("The comment must be 2,000 characters or fewer.");

        var now = DateTime.UtcNow;
        a.HodRecommendation = view;
        a.HodNote = note;
        a.HodRecommendedByUserId = MyId;
        a.HodRecommendedAt = now;
        a.RecordStep(ApplicationAction.HodReviewed, a.Status, MyId, scope.UserName, $"{EnumText.Label(view)}{(note is null ? "" : $": {note}")}", now);
        return await SaveAsync(ct);
    }

    /// <summary>
    /// Shortlists an application. With the HOD's agreement any reviewer can; otherwise only an HR Manager, giving
    /// <paramref name="overrideReason"/>. The applicant is emailed.
    /// </summary>
    public async Task<Result> ShortlistAsync(int applicationId, string? overrideReason, CancellationToken ct = default)
    {
        if (!IsReviewer) return Result.Failure("Only a Recruitment Officer or HR Manager can shortlist.");
        var a = await applications.GetWithHistoryAsync(applicationId, ct);
        if (a is null) return Result.Failure("Application not found.");
        if (a.ApplicantUserId == MyId) return Result.Failure("This is your own application, so another reviewer must handle it.");
        if (!ApplicationWorkflow.CanShortlist(a.Status)) return Result.Failure("Only applications under review can be shortlisted.");

        var action = ApplicationAction.Shortlisted;
        string? comments = null;
        if (ApplicationWorkflow.NeedsOverride(a))
        {
            if (!CanOverride)
                return Result.Failure("The Head of Department has not agreed to shortlist this applicant. Only an HR Manager can override that.");
            overrideReason = Clean(overrideReason);
            if (overrideReason is null || overrideReason.Length < 5)
                return Result.Failure("The Head of Department has not agreed, so record why you are shortlisting anyway.");
            if (overrideReason.Length > 2000) return Result.Failure("The reason must be 2,000 characters or fewer.");
            action = ApplicationAction.ShortlistedOverride;
            comments = overrideReason;
        }

        var now = DateTime.UtcNow;
        a.ShortlistedAt = now;
        a.RecordStep(action, ApplicationStatus.Shortlisted, MyId, scope.UserName, comments, now);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded && await vacancies.GetByIdAsync(a.VacancyId, ct) is { } v)
            await mailer.TemplateToUserAsync(a.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.Shortlisted,
                new Dictionary<string, string?> { ["JobTitle"] = v.JobTitle }, links.MyApplication(a.Id), ct: ct);
        return saved;
    }

    // ===================================================================== For the dashboard

    /// <summary>Vacancies in the current user's departments with recommendations waiting for their view as HOD.</summary>
    public async Task<IReadOnlyList<HodShortlistTaskDto>> HodTasksAsync(CancellationToken ct = default)
    {
        if (!me.IsInRole(Roles.HeadOfDepartment)) return [];
        var depts = (await scope.HodDepartmentIdsAsync(ct)).ToList();
        if (depts.Count == 0) return [];

        var waiting = await applications.ListAsync(a => depts.Contains(a.DepartmentId) && a.Status == ApplicationStatus.UnderReview &&
            a.RecruiterRecommendation != ShortlistRecommendation.None && a.HodRecommendation == ShortlistRecommendation.None &&
            a.ApplicantUserId != MyId, ct);
        var vacancyIds = waiting.Select(a => a.VacancyId).Distinct().ToList();
        var byId = (await vacancies.ListAsync(v => vacancyIds.Contains(v.Id), ct)).ToDictionary(v => v.Id); // audit P7: one query
        var result = new List<HodShortlistTaskDto>();
        foreach (var g in waiting.GroupBy(a => a.VacancyId))
            if (byId.GetValueOrDefault(g.Key) is { } v)
                result.Add(new HodShortlistTaskDto(v.Id, v.VacancyNumber, v.JobTitle, g.Count(), g.Min(a => a.RecruiterRecommendedAt ?? a.SubmittedAt)));
        return result;
    }

    // ===================================================================== Helpers

    /// <summary>True when the current user heads <paramref name="departmentId"/> (as set on the department or their home department).</summary>
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
