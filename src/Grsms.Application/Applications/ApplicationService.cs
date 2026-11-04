using Grsms.Application.Adverts;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Users;
using Grsms.Application.Vacancies;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Documents;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Applications;

/// <summary>
/// Candidate applications (Module 5): applying through a live advert, the applicant's own tracker and withdrawal,
/// and the staff side (list, review, "not successful").
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Apply / see own applications / withdraw : any signed-in person (candidates; GREL staff for internal jobs too).
///   * See applications                        : RO, HR Manager, MD, HR Administrator (all); a HOD (their departments).
///   * Start review / mark not successful      : Recruitment Officer or HR Manager, never on their own application.
/// Applicants never see staff comments or the internal reason for an outcome.
/// </summary>
public class ApplicationService(
    IApplicationRepository applications,
    IAdvertRepository adverts,
    IVacancyRepository vacancies,
    IRepository<Division> divisions,
    IRepository<Department> departments,
    IRepository<Site> sites,
    CandidateProfileService profiles,
    AttachmentService files,
    IUserDirectory users,
    INumberGenerator numbers,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links)
{
    private const string ConflictMessage = "Someone else updated this application at the same moment. Please reload the page and try again.";

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool IsStaff => me.UserId is not null && !me.IsInRole(Roles.Candidate) && Roles.Staff.Any(me.IsInRole);
    private bool IsReviewer => ApplicationWorkflow.ReviewerRoles.Any(me.IsInRole);

    // ===================================================================== Applying

    /// <summary>
    /// Can the current user apply through this advert? Returns the reason if not, and their existing application if any.
    /// Used by the careers page to show the right button.
    /// </summary>
    public async Task<(bool CanApply, string? Reason, int? ExistingApplicationId)> GetApplyStatusAsync(string advertNumber, CancellationToken ct = default)
    {
        if (me.UserId is null) return (false, null, null);
        var (_, v, error) = await LoadAdvertAsync(advertNumber, ct);
        if (v is null) return (false, error, null);
        var existing = (await applications.ListAsync(a => a.VacancyId == v.Id && a.ApplicantUserId == MyId, ct)).FirstOrDefault();
        return existing is null ? (true, null, null) : (false, "You have already applied for this vacancy.", existing.Id);
    }

    /// <summary>The apply page for an advert, or the reason the user cannot apply.</summary>
    public async Task<Result<ApplyFormDto>> GetApplyFormAsync(string advertNumber, CancellationToken ct = default)
    {
        var status = await GetApplyStatusAsync(advertNumber, ct);
        var form = new ApplyFormDto { AdvertNumber = advertNumber, Source = ApplicationSource.CareersPage };
        if (!status.CanApply) return Result.Failure<ApplyFormDto>(status.Reason ?? "This vacancy is not available.");
        await FillApplyFormAsync(form, ct);
        return Result.Success(form);
    }

    /// <summary>Fills the read-only parts of the apply page (job, profile summary, what is missing).</summary>
    public async Task FillApplyFormAsync(ApplyFormDto form, CancellationToken ct = default)
    {
        var (_, v, _) = await LoadAdvertAsync(form.AdvertNumber, ct);
        if (v is not null)
        {
            form.JobTitle = v.JobTitle;
            form.VacancyNumber = v.VacancyNumber;
            form.ClosingDate = v.ClosingDate;
            form.DepartmentName = (await departments.GetByIdAsync(v.DepartmentId, ct))?.Name ?? "";
        }
        form.Profile = await profiles.GetMineAsync(ct);
    }

    /// <summary>
    /// Submits an application: checks eligibility and the profile, freezes a copy of the profile and documents on the
    /// application, and emails the applicant and the vacancy's recruiter.
    /// </summary>
    public async Task<Result<int>> ApplyAsync(ApplyFormDto form, CancellationToken ct = default)
    {
        var (advert, v, error) = await LoadAdvertAsync(form.AdvertNumber, ct);
        if (v is null || advert is null) return Result.Failure<int>(error ?? "This vacancy is not available.");
        if (await applications.AnyAsync(a => a.VacancyId == v.Id && a.ApplicantUserId == MyId, ct))
            return Result.Failure<int>("You have already applied for this vacancy.");
        if (!form.Confirm) return Result.Failure<int>("Tick the box to confirm your information is true and complete.");
        if (form.Source is null) return Result.Failure<int>("Tell us where you saw this job.");

        var profile = await profiles.FindMineAsync(ct);
        var docs = profile is null ? [] : await files.ListAsync(AttachmentOwners.CandidateProfile, profile.Id, ct);
        var missing = CandidateProfileService.MissingFor(profile, docs);
        if (missing.Count > 0)
            return Result.Failure<int>($"Before applying, add {string.Join(", ", missing)} to your profile.");

        var contact = (await users.GetContactAsync(MyId, ct))!;
        var number = await numbers.NextAsync($"APP-{DateTime.UtcNow.Year}", ct); // before Add: it saves its own change
        var now = DateTime.UtcNow;
        var app = new JobApplication
        {
            ApplicationNumber = $"APP-{now.Year}-{number:00000}",
            VacancyId = v.Id,
            JobAdvertId = advert.Id,
            ApplicantUserId = MyId,
            IsInternal = IsStaff,
            Source = form.Source.Value,
            DivisionId = v.DivisionId,
            DepartmentId = v.DepartmentId,
            RequestingHodUserId = v.RequestingHodUserId,
            FullName = contact.FullName,
            Email = contact.Email,
            Phone = profile!.Phone!,
            Location = profile.Location,
            HighestEducation = profile.HighestEducation,
            FieldOfStudy = profile.FieldOfStudy,
            YearsExperience = profile.YearsExperience,
            CurrentEmployer = profile.CurrentEmployer,
            CurrentPosition = profile.CurrentPosition,
            Skills = profile.Skills,
            Certifications = profile.Certifications,
            LinkedInUrl = profile.LinkedInUrl,
            CoverLetter = string.IsNullOrWhiteSpace(form.CoverLetter) ? null : form.CoverLetter.Trim(),
            SubmittedAt = now,
            StatusChangedAt = now
        };
        app.RecordStep(ApplicationAction.Submitted, ApplicationStatus.Submitted, MyId, contact.FullName, null, now);

        // Phase 6: score the application against the vacancy's requirements straight away.
        if (await vacancies.GetWithDetailsAsync(v.Id, ct) is { } withRequirements)
            app.ApplyScreening(CvScreening.Score(withRequirements, app), now);
        applications.Add(app);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // A double click: the unique index (one application per person per vacancy) stopped the second copy.
            if (await applications.AnyAsync(a => a.VacancyId == v.Id && a.ApplicantUserId == MyId, ct))
                return Result.Failure<int>("You have already applied for this vacancy.");
            throw;
        }

        // Freeze copies of the CV and certificates on the application, so later profile changes don't alter what was sent.
        foreach (var doc in docs.Where(d => d.Category is AttachmentCategory.Cv or AttachmentCategory.Certificate))
        {
            var original = await files.OpenAsync(AttachmentOwners.CandidateProfile, profile.Id, doc.Id, ct);
            if (original is null) continue;
            await using (original.Content)
                await files.AddAsync(AttachmentOwners.JobApplication, app.Id, doc.Category, doc.FileName, original.Content, ct);
        }

        await mailer.ToUserIncludingMeAsync(MyId, $"We received your application: {v.JobTitle}",
            $"Thank you for applying for <b>{WorkflowMailer.Enc(v.JobTitle)}</b>. Your reference is <b>{app.ApplicationNumber}</b>." +
            "<p>You can follow its progress at any time under <i>My applications</i>.</p>",
            links.MyApplication(app.Id), "View my application", ct);
        await mailer.ToUserAsync(v.RecruiterUserId, $"New application for {v.VacancyNumber}",
            $"{WorkflowMailer.Enc(contact.FullName)}{(app.IsInternal ? " (GREL staff)" : "")} applied for <b>{WorkflowMailer.Enc(v.JobTitle)}</b>.",
            links.Application(app.Id), $"Open application {app.ApplicationNumber}", ct);
        return Result.Success(app.Id);
    }

    // ===================================================================== The applicant's own view

    public Task<IReadOnlyList<MyApplicationDto>> MyApplicationsAsync(CancellationToken ct = default) =>
        applications.ListForApplicantAsync(MyId, ct);

    public async Task<MyApplicationDetailsDto?> GetMineAsync(int id, CancellationToken ct = default)
    {
        var a = await applications.GetWithHistoryAsync(id, ct);
        if (a is null || a.ApplicantUserId != MyId) return null;
        var v = (await vacancies.GetByIdAsync(a.VacancyId, ct))!;

        return new MyApplicationDetailsDto
        {
            Id = a.Id,
            ApplicationNumber = a.ApplicationNumber,
            JobTitle = v.JobTitle,
            DepartmentName = (await departments.GetByIdAsync(a.DepartmentId, ct))?.Name ?? "",
            SiteName = (await sites.GetByIdAsync(v.SiteId, ct))?.Name ?? "",
            ClosingDate = v.ClosingDate,
            Status = a.Status,
            SubmittedAt = a.SubmittedAt,
            StatusChangedAt = a.StatusChangedAt,
            CoverLetter = a.CoverLetter,
            Source = a.Source,
            LastStageReached = a.Actions.SelectMany(x => new[] { x.FromStatus, x.ToStatus }).Append(a.Status)
                .Where(s => ApplicationWorkflow.TrackerStages.Contains(s))
                .MaxBy(s => ApplicationWorkflow.TrackerStages.ToList().IndexOf(s)),
            Documents = await files.ListAsync(AttachmentOwners.JobApplication, a.Id, ct),
            // Applicant-friendly wording only; staff comments are never shown here.
            Timeline = a.Actions.OrderBy(x => x.ActionAt).ThenBy(x => x.Id).Select(x => (x.Action switch
            {
                ApplicationAction.Submitted => "Application received",
                ApplicationAction.ReviewStarted => "Your application is being reviewed",
                ApplicationAction.NotSuccessful => "Outcome: not successful this time",
                ApplicationAction.Withdrawn => "You withdrew your application",
                _ => EnumText.Label(x.Action)
            }, x.ActionAt)).ToList(),
            CanWithdraw = ApplicationWorkflow.CanWithdraw(a.Status)
        };
    }

    public async Task<Result> WithdrawAsync(int id, CancellationToken ct = default)
    {
        var a = await applications.GetWithHistoryAsync(id, ct);
        if (a is null || a.ApplicantUserId != MyId) return Result.Failure("Application not found.");
        if (!ApplicationWorkflow.CanWithdraw(a.Status)) return Result.Failure("This application can no longer be withdrawn.");

        a.RecordStep(ApplicationAction.Withdrawn, ApplicationStatus.Withdrawn, MyId, me.FullName ?? a.FullName, null, DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded && await vacancies.GetByIdAsync(a.VacancyId, ct) is { } v)
            await mailer.ToUserAsync(v.RecruiterUserId, $"{a.ApplicationNumber} was withdrawn",
                $"{WorkflowMailer.Enc(a.FullName)} withdrew their application for <b>{WorkflowMailer.Enc(v.JobTitle)}</b>.",
                links.Application(a.Id), $"Open application {a.ApplicationNumber}", ct);
        return saved;
    }

    /// <summary>A document of one of my applications.</summary>
    public async Task<FileDownload?> OpenMyDocumentAsync(int id, int attachmentId, CancellationToken ct = default)
    {
        var a = await applications.GetByIdAsync(id, ct);
        return a is null || a.ApplicantUserId != MyId ? null : await files.OpenAsync(AttachmentOwners.JobApplication, id, attachmentId, ct);
    }

    // ===================================================================== Staff view

    public async Task<PagedResult<ApplicationListItemDto>> SearchAsync(ApplicationFilter filter, CancellationToken ct = default)
    {
        filter.Status = filter.Tab switch
        {
            "new" => ApplicationStatus.Submitted,
            "review" => ApplicationStatus.UnderReview,
            _ => filter.Status
        };
        return await applications.SearchAsync(filter, await scope.VisibilityAsync(ct), ct);
    }

    public async Task<ApplicationCounts> CountsAsync(CancellationToken ct = default) =>
        await applications.CountAsync(await scope.VisibilityAsync(ct), ct);

    public async Task<ApplicationDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var a = await applications.GetWithHistoryAsync(id, ct);
        if (a is null || !await CanStaffSeeAsync(a, ct)) return null;

        var v = (await vacancies.GetByIdAsync(a.VacancyId, ct))!;
        var advert = a.JobAdvertId is int advertId ? await adverts.GetByIdAsync(advertId, ct) : null;
        var hod = await users.GetDisplayNamesAsync([a.RequestingHodUserId], ct);
        var own = a.ApplicantUserId == me.UserId;

        return new ApplicationDetailsDto
        {
            Id = a.Id,
            ApplicationNumber = a.ApplicationNumber,
            Status = a.Status,
            SubmittedAt = a.SubmittedAt,
            IsInternal = a.IsInternal,
            Source = a.Source,
            AdvertId = advert?.Id,
            AdvertNumber = advert?.AdvertNumber,
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            JobTitle = v.JobTitle,
            DivisionName = (await divisions.GetByIdAsync(a.DivisionId, ct))?.Name ?? "?",
            DepartmentName = (await departments.GetByIdAsync(a.DepartmentId, ct))?.Name ?? "?",
            RequestingHodName = hod.GetValueOrDefault(a.RequestingHodUserId) ?? "?",
            FullName = a.FullName,
            Email = a.Email,
            Phone = a.Phone,
            Location = a.Location,
            HighestEducation = a.HighestEducation,
            FieldOfStudy = a.FieldOfStudy,
            YearsExperience = a.YearsExperience,
            CurrentEmployer = a.CurrentEmployer,
            CurrentPosition = a.CurrentPosition,
            Skills = Lines(a.Skills),
            Certifications = Lines(a.Certifications),
            LinkedInUrl = a.LinkedInUrl,
            CoverLetter = a.CoverLetter,
            OutcomeReason = a.OutcomeReason,
            Screening = a.ReadScreening(),
            RecruiterRecommendation = a.RecruiterRecommendation,
            RecruiterNote = a.RecruiterNote,
            HodRecommendation = a.HodRecommendation,
            HodNote = a.HodNote,
            Documents = await files.ListAsync(AttachmentOwners.JobApplication, a.Id, ct),
            History = a.Actions.OrderBy(x => x.ActionAt).ThenBy(x => x.Id)
                .Select(x => new ApplicationHistoryDto(x.Action, x.FromStatus, x.ToStatus, x.ActorName, x.Comments, x.ActionAt)).ToList(),
            CanStartReview = IsReviewer && !own && ApplicationWorkflow.CanStartReview(a.Status),
            CanMarkNotSuccessful = IsReviewer && !own && ApplicationWorkflow.CanMarkNotSuccessful(a.Status),
            ActionBlockedReason = own && IsReviewer ? "This is your own application, so another reviewer must handle it." : null
        };
    }

    public async Task<Result> StartReviewAsync(int id, CancellationToken ct = default)
    {
        var (a, error) = await LoadForReviewerAsync(id, ApplicationWorkflow.CanStartReview, "Only newly received applications can be moved to review.", ct);
        if (a is null) return Result.Failure(error!);
        a.RecordStep(ApplicationAction.ReviewStarted, ApplicationStatus.UnderReview, MyId, scope.UserName, null, DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    /// <summary>Ends an application with an internal reason. The applicant receives a polite email without the reason.</summary>
    public async Task<Result> MarkNotSuccessfulAsync(int id, string? reason, CancellationToken ct = default)
    {
        var (a, error) = await LoadForReviewerAsync(id, ApplicationWorkflow.CanMarkNotSuccessful, "This application can no longer be marked not successful here.", ct);
        if (a is null) return Result.Failure(error!);
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason is null || reason.Length < 5) return Result.Failure("Record the reason (for internal use; the applicant does not see it).");
        if (reason.Length > 2000) return Result.Failure("The reason must be 2,000 characters or fewer.");

        a.OutcomeReason = reason;
        a.RecordStep(ApplicationAction.NotSuccessful, ApplicationStatus.NotSuccessful, MyId, scope.UserName, reason, DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded && await vacancies.GetByIdAsync(a.VacancyId, ct) is { } v)
            await mailer.ToUserAsync(a.ApplicantUserId, $"Your application for {v.JobTitle}",
                $"Thank you for your interest in the <b>{WorkflowMailer.Enc(v.JobTitle)}</b> position at GREL. After careful consideration, " +
                "we will not be taking your application further on this occasion. We encourage you to apply for future vacancies that match your profile.",
                links.MyApplication(a.Id), "View my application", ct);
        return saved;
    }

    /// <summary>A document of an application, for staff who may see the application.</summary>
    public async Task<FileDownload?> OpenDocumentAsync(int id, int attachmentId, CancellationToken ct = default)
    {
        var a = await applications.GetByIdAsync(id, ct);
        return a is null || !await CanStaffSeeAsync(a, ct) ? null : await files.OpenAsync(AttachmentOwners.JobApplication, id, attachmentId, ct);
    }

    // ===================================================================== Helpers

    /// <summary>Loads a running advert and its vacancy, and checks the current user may apply through it.</summary>
    private async Task<(JobAdvert? Advert, Vacancy? Vacancy, string? Error)> LoadAdvertAsync(string advertNumber, CancellationToken ct)
    {
        var advert = await adverts.FindByNumberAsync(advertNumber ?? "", ct);
        if (advert is null) return (null, null, "This vacancy is not available.");
        var v = await vacancies.GetByIdAsync(advert.VacancyId, ct);
        if (v is null) return (null, null, "This vacancy is not available.");
        var posted = advert.Channels.Where(c => c.Status == ChannelPostStatus.Posted).Select(c => c.Channel);
        var why = ApplicationWorkflow.WhyCannotApply(v, advert, posted, IsStaff, Today);
        return why is null ? (advert, v, null) : (null, null, why);
    }

    private async Task<bool> CanStaffSeeAsync(JobApplication a, CancellationToken ct) =>
        Roles.Staff.Any(me.IsInRole) && !me.IsInRole(Roles.Candidate) &&
        (AccessScope.SeeAllRoles.Any(me.IsInRole) || me.IsInRole(Roles.HeadOfDepartment)) &&
        AccessScope.CanSee(await scope.VisibilityAsync(ct), a.DepartmentId, null);

    private async Task<(JobApplication? Application, string? Error)> LoadForReviewerAsync(int id, Func<ApplicationStatus, bool> allowed,
        string notAllowed, CancellationToken ct)
    {
        if (!IsReviewer) return (null, "Only a Recruitment Officer or HR Manager can do this.");
        var a = await applications.GetWithHistoryAsync(id, ct);
        if (a is null) return (null, "Application not found.");
        if (a.ApplicantUserId == MyId) return (null, "This is your own application, so another reviewer must handle it.");
        return allowed(a.Status) ? (a, null) : (null, notAllowed);
    }

    private static IReadOnlyList<string> Lines(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
