using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Vacancies;
using Grsms.Domain.Applications;
using Grsms.Domain.Documents;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Microsoft.Extensions.Logging;
using static Grsms.Application.Common.Text;

namespace Grsms.Application.Applications;

/// <summary>
/// Phase 15: the ATS (applicant tracking system) screening engine.
///
///   1. Read the CV: the application's CV file is turned into text on GREL's server and read by GRSMS's own rules
///      (<see cref="CvParser"/>), or by the optional Claude reader when the Super Admin has switched it on.
///   2. Score: education, experience, skills and certifications, the weighted overall match and its band
///      (<see cref="AtsEngine"/>), against the vacancy's requirements and knockout questions.
///   3. Reject automatically when a rule the Super Admin switched on is broken. The application is HELD:
///      status "Automatically rejected", the reason recorded, the applicant still sees "Under review".
///   4. HR decides: a Recruitment Officer or HR Manager confirms the rejection (→ Not successful, applicant emailed)
///      or overrides it with a reason (→ Under review). Nobody acts on their own application.
///
/// The ATS never stops anyone opening, reading or downloading an application. Every result, rejection and decision is
/// stored on the application, written to its history and to the audit trail.
/// </summary>
public class AtsService(
    IApplicationRepository applications,
    IVacancyRepository vacancies,
    IRepository<AtsSettings> settingsRepo,
    IRepository<Department> departments,
    AttachmentService files,
    ICvTextExtractor extractor,
    IAiCvReader ai,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links,
    ILogger<AtsService> logger)
{
    /// <summary>The name shown in the history for the ATS's own steps.</summary>
    public const string SystemActorName = "ATS (automatic screening)";

    private const int MaxCvTextLength = 100_000;
    private const string ConflictMessage = "Someone else updated this application at the same moment. Please reload the page and try again.";

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private string? MyId => me.UserId;
    private bool IsReviewer => ApplicationWorkflow.ReviewerRoles.Any(me.IsInRole);

    // ===================================================================== Settings

    /// <summary>The settings in force: the saved row, or the defaults if the Super Admin has never saved the screen.</summary>
    public async Task<AtsSettings> GetSettingsAsync(CancellationToken ct = default) =>
        (await settingsRepo.ListAsync(_ => true, ct)).OrderBy(s => s.Id).FirstOrDefault() ?? new AtsSettings();

    public async Task<AtsSettingsFormDto> GetSettingsFormAsync(CancellationToken ct = default)
    {
        var s = await GetSettingsAsync(ct);
        return new AtsSettingsFormDto
        {
            EducationWeight = s.EducationWeight, ExperienceWeight = s.ExperienceWeight, SkillsWeight = s.SkillsWeight,
            CertificationWeight = s.CertificationWeight, HighlyRecommendedFrom = s.HighlyRecommendedFrom,
            RecommendedFrom = s.RecommendedFrom, ReviewRequiredFrom = s.ReviewRequiredFrom, LowMatchFrom = s.LowMatchFrom,
            RejectOnKnockout = s.RejectOnKnockout, RejectBelowLowMatch = s.RejectBelowLowMatch,
            RejectBelowMinimumEducation = s.RejectBelowMinimumEducation, RejectBelowMinimumExperience = s.RejectBelowMinimumExperience,
            RejectMissingRequiredCertification = s.RejectMissingRequiredCertification, RejectMissingRequiredSkill = s.RejectMissingRequiredSkill,
            UseAiReader = s.UseAiReader, AiConfigured = ai.IsConfigured, UpdatedAt = s.UpdatedAt ?? (s.Id == 0 ? null : s.CreatedAt),
            UpdatedBy = s.UpdatedBy ?? s.CreatedBy
        };
    }

    /// <summary>Saves the ATS settings. Super Admin only (permission "ats.configure"). Applies to applications screened from now on.</summary>
    public async Task<Result> SaveSettingsAsync(AtsSettingsFormDto form, CancellationToken ct = default)
    {
        if (!me.HasPermission(Permissions.ConfigureAts)) return Result.Failure("Only a Super Admin can change the ATS settings.");
        var s = (await settingsRepo.ListAsync(_ => true, ct)).OrderBy(x => x.Id).FirstOrDefault();
        var isNew = s is null;
        s ??= new AtsSettings();
        s.EducationWeight = form.EducationWeight;
        s.ExperienceWeight = form.ExperienceWeight;
        s.SkillsWeight = form.SkillsWeight;
        s.CertificationWeight = form.CertificationWeight;
        s.HighlyRecommendedFrom = form.HighlyRecommendedFrom;
        s.RecommendedFrom = form.RecommendedFrom;
        s.ReviewRequiredFrom = form.ReviewRequiredFrom;
        s.LowMatchFrom = form.LowMatchFrom;
        s.RejectOnKnockout = form.RejectOnKnockout;
        s.RejectBelowLowMatch = form.RejectBelowLowMatch;
        s.RejectBelowMinimumEducation = form.RejectBelowMinimumEducation;
        s.RejectBelowMinimumExperience = form.RejectBelowMinimumExperience;
        s.RejectMissingRequiredCertification = form.RejectMissingRequiredCertification;
        s.RejectMissingRequiredSkill = form.RejectMissingRequiredSkill;
        if (form.UseAiReader && !ai.IsConfigured && !s.UseAiReader)
            return Result.Failure("The Claude AI reader has no API key on this server (setting Ats:Ai:ApiKey). Ask IT to set it first.");
        s.UseAiReader = form.UseAiReader;

        var errors = s.Validate();
        if (errors.Count > 0) return Result.Failure([.. errors]);
        if (isNew) settingsRepo.Add(s);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ===================================================================== Screening

    /// <summary>
    /// Screens a newly submitted application (reads its CV, scores it, and rejects it automatically if a rule says so).
    /// Never throws: a problem is logged and the application simply stays "Received" without a score.
    /// </summary>
    public async Task<AtsResult?> ScreenNewAsync(int applicationId, CancellationToken ct = default)
    {
        try
        {
            var a = await applications.GetWithHistoryAsync(applicationId, ct);
            var v = a is null ? null : await vacancies.GetWithDetailsAsync(a.VacancyId, ct);
            if (a is null || v is null) return null;
            // The ATS acts on its own here, inside the applicant's request: record it as the system, not the applicant,
            // so a held rejection never appears in the applicant's own audit trail.
            using (SystemActor.Begin(SystemActorName))
            {
                var result = await ScreenAsync(a, v, await GetSettingsAsync(ct), ct);
                await unitOfWork.SaveChangesAsync(ct);
                return result;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "ATS screening failed for application {ApplicationId}; it stays unscreened until screening is re-run", applicationId);
            return null;
        }
    }

    /// <summary>
    /// Re-runs the ATS for the vacancy's applications that are still at the screening stage (Received, Under review,
    /// Automatically rejected). Received ones can be rejected; a held rejection that no longer applies is cleared.
    /// Returns how many were screened.
    /// </summary>
    public async Task<Result<int>> RescreenVacancyAsync(int vacancyId, CancellationToken ct = default)
    {
        if (!IsReviewer) return Result.Failure<int>("Only a Recruitment Officer or HR Manager can run screening.");
        var v = await vacancies.GetWithDetailsAsync(vacancyId, ct);
        if (v is null) return Result.Failure<int>("Vacancy not found.");
        var settings = await GetSettingsAsync(ct);

        var ids = (await applications.ListAsync(a => a.VacancyId == vacancyId &&
            (a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview || a.Status == ApplicationStatus.AutoRejected), ct))
            .Select(a => a.Id).ToList();
        foreach (var id in ids)
        {
            var a = (await applications.GetWithHistoryAsync(id, ct))!;
            await ScreenAsync(a, v, settings, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ids.Count);
    }

    /// <summary>Reads the CV (once), scores the application and applies the automatic-rejection rules. Does not save.</summary>
    private async Task<AtsResult> ScreenAsync(JobApplication a, Vacancy v, AtsSettings settings, CancellationToken ct)
    {
        if (a.CvParsed is null) await ReadCvAsync(a, settings, ct);
        var result = AtsEngine.Score(v, a, a.ReadCv(), a.CvText, a.KnockoutInputs(), settings);
        var now = DateTime.UtcNow;
        a.ApplyScreening(result, now);

        var reason = string.Join("\n", result.RejectionReasons);
        if (result.AutoReject && a.Status == ApplicationStatus.Submitted)
        {
            a.AtsDecision = AtsDecision.AutoRejected;
            a.AtsRejectionReason = reason;
            a.AtsDecisionByUserId = null;
            a.AtsDecisionAt = now;
            a.AtsDecisionNote = null;
            a.RecordStep(ApplicationAction.AtsRejected, ApplicationStatus.AutoRejected, null, SystemActorName, reason, now);
        }
        else if (a.Status == ApplicationStatus.AutoRejected && !result.AutoReject)
        {
            a.AtsDecision = AtsDecision.Cleared;
            a.AtsDecisionAt = now;
            a.RecordStep(ApplicationAction.AtsCleared, ApplicationStatus.Submitted, null, SystemActorName,
                $"Re-screened at {result.Overall}%: no automatic-rejection rule applies any more.", now);
        }
        else if (a.Status == ApplicationStatus.AutoRejected)
            a.AtsRejectionReason = reason; // still rejected; keep the reason current
        return result;
    }

    /// <summary>Turns the application's CV file into text and reads it (AI reader if switched on and configured, else the rules).</summary>
    private async Task ReadCvAsync(JobApplication a, AtsSettings settings, CancellationToken ct)
    {
        var cv = (await files.ListAsync(AttachmentOwners.JobApplication, a.Id, ct))
            .Where(d => d.Category == AttachmentCategory.Cv).OrderByDescending(d => d.UploadedAt).FirstOrDefault();
        if (cv is null) return;

        if (a.CvText is null && await files.OpenAsync(AttachmentOwners.JobApplication, a.Id, cv.Id, ct) is { } file)
            await using (file.Content)
                a.CvText = await extractor.ExtractAsync(file.Content, file.FileName, ct) is { } text
                    ? text.Length > MaxCvTextLength ? text[..MaxCvTextLength] : text
                    : null;

        ParsedCv? parsed = null;
        if (settings.UseAiReader && ai.IsConfigured && a.CvText is not null)
            parsed = await ai.ReadAsync(a.CvText, Today, ct);
        parsed ??= CvParser.Parse(a.CvText, Today);
        a.CvParsed = System.Text.Json.JsonSerializer.Serialize(parsed);
    }

    // ===================================================================== HR decisions

    /// <summary>HR agrees with the ATS: the application becomes "Not successful" and the applicant is told politely.</summary>
    public async Task<Result> ConfirmRejectionAsync(int applicationId, string? note, CancellationToken ct = default)
    {
        var (a, error) = await LoadForDecisionAsync(applicationId, ct);
        if (a is null) return Result.Failure(error!);
        note = Clean(note);
        if (note?.Length > 2000) return Result.Failure("The note must be 2,000 characters or fewer.");
        Confirm(a, note, DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded) await TellNotSuccessfulAsync(a, ct);
        return saved;
    }

    /// <summary>Confirms every held ATS rejection of a vacancy (except the reviewer's own application). Returns how many.</summary>
    public async Task<Result<int>> ConfirmAllAsync(int vacancyId, CancellationToken ct = default)
    {
        if (!IsReviewer) return Result.Failure<int>("Only a Recruitment Officer or HR Manager can confirm ATS rejections.");
        var ids = (await applications.ListAsync(a => a.VacancyId == vacancyId && a.Status == ApplicationStatus.AutoRejected && a.ApplicantUserId != MyId, ct))
            .Select(a => a.Id).ToList();
        if (ids.Count == 0) return Result.Failure<int>("There are no ATS rejections waiting for HR on this vacancy.");

        var confirmed = new List<JobApplication>();
        var now = DateTime.UtcNow;
        foreach (var id in ids)
        {
            var a = (await applications.GetWithHistoryAsync(id, ct))!;
            Confirm(a, "Confirmed with all held ATS rejections of the vacancy.", now);
            confirmed.Add(a);
        }
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);
        foreach (var a in confirmed) await TellNotSuccessfulAsync(a, ct);
        return Result.Success(confirmed.Count);
    }

    /// <summary>HR disagrees with the ATS: the application goes to "Under review" for a normal review. A reason is required.</summary>
    public async Task<Result> OverrideAsync(int applicationId, string? reason, CancellationToken ct = default)
    {
        var (a, error) = await LoadForDecisionAsync(applicationId, ct);
        if (a is null) return Result.Failure(error!);
        reason = Clean(reason);
        if (reason is null || reason.Length < 5) return Result.Failure("Record why you are overriding the ATS (at least 5 characters).");
        if (reason.Length > 2000) return Result.Failure("The reason must be 2,000 characters or fewer.");

        var now = DateTime.UtcNow;
        a.AtsDecision = AtsDecision.Overridden;
        a.AtsDecisionByUserId = MyId;
        a.AtsDecisionAt = now;
        a.AtsDecisionNote = reason;
        a.RecordStep(ApplicationAction.AtsOverridden, ApplicationStatus.UnderReview, MyId, scope.UserName, reason, now);
        return await SaveAsync(ct);
    }

    private void Confirm(JobApplication a, string? note, DateTime now)
    {
        a.AtsDecision = AtsDecision.Confirmed;
        a.AtsDecisionByUserId = MyId;
        a.AtsDecisionAt = now;
        a.AtsDecisionNote = note;
        a.OutcomeReason = $"ATS: {a.AtsRejectionReason?.Replace("\n", "; ")}{(note is null ? "" : $" ({note})")}";
        a.RecordStep(ApplicationAction.NotSuccessful, ApplicationStatus.NotSuccessful, MyId, scope.UserName,
            $"ATS rejection confirmed. {a.AtsRejectionReason?.Replace("\n", "; ")}{(note is null ? "" : $" Note: {note}")}", now);
    }

    private async Task<(JobApplication? Application, string? Error)> LoadForDecisionAsync(int id, CancellationToken ct)
    {
        if (!IsReviewer) return (null, "Only a Recruitment Officer or HR Manager can decide on an ATS rejection.");
        var a = await applications.GetWithHistoryAsync(id, ct);
        if (a is null) return (null, "Application not found.");
        if (a.ApplicantUserId == MyId) return (null, "This is your own application, so another reviewer must handle it.");
        return ApplicationWorkflow.CanDecideAtsRejection(a.Status)
            ? (a, null)
            : (null, "This application is not waiting for a decision on an ATS rejection.");
    }

    private async Task TellNotSuccessfulAsync(JobApplication a, CancellationToken ct)
    {
        if (await vacancies.GetByIdAsync(a.VacancyId, ct) is not { } v) return;
        await mailer.TemplateToUserAsync(a.ApplicantUserId, Domain.Settings.EmailTemplateCatalogue.NotSuccessful,
            new Dictionary<string, string?> { ["JobTitle"] = v.JobTitle }, links.MyApplication(a.Id), ct: ct);
    }

    // ===================================================================== Dashboards

    /// <summary>Every vacancy the user may see that has applications, with its ATS counts. Newest vacancies first.</summary>
    public async Task<IReadOnlyList<AtsVacancySummaryDto>> OverviewAsync(CancellationToken ct = default)
    {
        var visibility = await scope.VisibilityAsync(ct);
        if (!visibility.SeeAll && visibility.HodDepartmentIds.Count == 0) return [];
        var hodDepts = visibility.HodDepartmentIds.ToList();
        var apps = await applications.ListAsync(a => a.Status != ApplicationStatus.Withdrawn &&
            (visibility.SeeAll || hodDepts.Contains(a.DepartmentId)), ct);

        // Audit P7: the vacancies and departments in two queries, not two per vacancy.
        var vacancyIds = apps.Select(a => a.VacancyId).Distinct().ToList();
        var byId = (await vacancies.ListAsync(v => vacancyIds.Contains(v.Id), ct)).ToDictionary(v => v.Id);
        var deptIds = byId.Values.Select(v => v.DepartmentId).Distinct().ToList();
        var deptNames = (await departments.ListAsync(d => deptIds.Contains(d.Id), ct)).ToDictionary(d => d.Id, d => d.Name);
        var result = new List<AtsVacancySummaryDto>();
        foreach (var g in apps.GroupBy(a => a.VacancyId))
        {
            if (byId.GetValueOrDefault(g.Key) is not { } v) continue;
            int Band(AtsClassification c) => g.Count(a => a.AtsClassification == c);
            result.Add(new AtsVacancySummaryDto(v.Id, v.VacancyNumber, v.JobTitle, deptNames.GetValueOrDefault(v.DepartmentId, ""),
                v.Status, g.Count(), g.Count(a => a.AtsClassification is not null),
                Band(AtsClassification.HighlyRecommended), Band(AtsClassification.Recommended), Band(AtsClassification.ReviewRequired),
                Band(AtsClassification.LowMatch), Band(AtsClassification.AutoReject),
                g.Count(a => a.Status == ApplicationStatus.AutoRejected), g.Max(a => a.ScreeningScore)));
        }
        return result.OrderByDescending(r => r.AwaitingHr > 0).ThenByDescending(r => r.VacancyId).ToList();
    }

    /// <summary>How many held ATS rejections the current reviewer could decide (sidebar badge).</summary>
    public async Task<int> AwaitingHrCountAsync(CancellationToken ct = default)
    {
        if (!IsReviewer) return 0;
        return (await applications.ListAsync(a => a.Status == ApplicationStatus.AutoRejected && a.ApplicantUserId != MyId, ct)).Count;
    }

    /// <summary>Usability U1: the automatic rejections waiting for HR to confirm, per vacancy (for the home to-dos), oldest first.</summary>
    public async Task<IReadOnlyList<(int VacancyId, string VacancyNumber, string JobTitle, int Count, DateTime Since)>> AwaitingHrByVacancyAsync(CancellationToken ct = default)
    {
        if (!IsReviewer) return [];
        var waiting = await applications.ListAsync(a => a.Status == ApplicationStatus.AutoRejected && a.ApplicantUserId != MyId, ct);
        if (waiting.Count == 0) return [];
        var ids = waiting.Select(a => a.VacancyId).Distinct().ToList();
        var byId = (await vacancies.ListAsync(v => ids.Contains(v.Id), ct)).ToDictionary(v => v.Id);
        return waiting.GroupBy(a => a.VacancyId).Where(g => byId.ContainsKey(g.Key))
            .Select(g => (g.Key, byId[g.Key].VacancyNumber, byId[g.Key].JobTitle, g.Count(), g.Min(a => a.StatusChangedAt)))
            .OrderBy(x => x.Item5).ToList();
    }

    /// <summary>A vacancy's ATS dashboard, or null if the user may not see it.</summary>
    public async Task<AtsBoardDto?> GetBoardAsync(int vacancyId, CancellationToken ct = default)
    {
        var v = await vacancies.GetWithDetailsAsync(vacancyId, ct);
        if (v is null) return null;
        var isHod = me.IsInRole(Roles.HeadOfDepartment) && (await scope.HodDepartmentIdsAsync(ct)).Contains(v.DepartmentId);
        if (!IsReviewer && !isHod && !scope.SeesAllDepartments) return null;

        var apps = await applications.ListAsync(a => a.VacancyId == vacancyId && a.Status != ApplicationStatus.Withdrawn, ct);
        var results = apps.ToDictionary(a => a.Id, a => a.ReadScreening());
        var rows = apps
            .OrderByDescending(a => a.ScreeningScore ?? -1)
            .ThenBy(a => a.SubmittedAt)
            .Select(a =>
            {
                var r = results[a.Id];
                var own = a.ApplicantUserId == MyId;
                var reader = a.CvParsed is null ? "no CV text" : a.ReadCv()?.Reader == "ai" ? "Claude AI" : "GRSMS rules";
                return new AtsRowDto(a.Id, a.ApplicationNumber, a.FullName, a.IsInternal, a.Status, r?.Overall, r?.Classification,
                    r?.Education, r?.Experience, r?.Skills, r?.Certifications, r?.MeetsMinimum, r?.AllMissingRequired ?? [],
                    r?.FailedKnockouts ?? [], a.AtsDecision, a.AtsRejectionReason, a.AtsDecisionNote, reader, own,
                    CanDecide: IsReviewer && !own && ApplicationWorkflow.CanDecideAtsRejection(a.Status));
            })
            .ToList();

        // Missing skill analysis: for each requirement, how many screened applicants have it.
        var screened = results.Values.OfType<AtsResult>().ToList();
        var coverage = v.Requirements.OrderBy(r => r.Type).ThenByDescending(r => r.IsEssential).ThenBy(r => r.Id).Select(req =>
        {
            var have = screened.Count(r => (req.Type, req.IsEssential) switch
            {
                (RequirementType.Skill, true) => r.MatchedRequiredSkills.Contains(req.Name),
                (RequirementType.Skill, false) => r.MatchedPreferredSkills.Contains(req.Name),
                (RequirementType.Certification, true) => r.MatchedRequiredCertifications.Contains(req.Name),
                _ => r.MatchedPreferredCertifications.Contains(req.Name)
            });
            return new RequirementCoverageDto(req.Name, req.Type, req.IsEssential, have, screened.Count - have);
        }).ToList();

        return new AtsBoardDto
        {
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            JobTitle = v.JobTitle,
            DepartmentName = (await departments.GetByIdAsync(v.DepartmentId, ct))?.Name ?? "",
            VacancyStatus = v.Status,
            MinimumEducation = v.MinimumEducation,
            MinYearsExperience = v.MinYearsExperience,
            KnockoutQuestions = v.KnockoutQuestions.OrderBy(q => q.Id).Select(q => q.Text).ToList(),
            Rows = rows,
            Coverage = coverage,
            ByBand = Enum.GetValues<AtsClassification>().ToDictionary(c => c, c => screened.Count(r => r.Classification == c)),
            AwaitingHr = apps.Count(a => a.Status == ApplicationStatus.AutoRejected),
            NotScreened = apps.Count - screened.Count,
            IsReviewer = IsReviewer,
            Settings = await GetSettingsAsync(ct)
        };
    }

    // ===================================================================== Helpers


    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);
}
