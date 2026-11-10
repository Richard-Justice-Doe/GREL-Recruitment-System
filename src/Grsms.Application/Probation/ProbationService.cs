using System.Globalization;
using System.Net;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Onboarding;
using Grsms.Application.Users;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Probation;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using static Grsms.Application.Common.Text;
using static Grsms.Application.Common.Signatures;

namespace Grsms.Application.Probation;

/// <summary>
/// Phase 21: probation management and employment confirmation. See <see cref="ProbationWorkflow"/>.
///
/// WHO MAY DO WHAT (checked here, on the server):
///   * Start                       : automatic, when the onboarding is completed (<see cref="StartForOnboardingAsync"/>).
///   * Initial meeting, assessments: the Head of the employee's department (never on their own probation).
///   * Sign the initial meeting    : the employee (My probation) and the HOD (when saving the meeting).
///   * Decide the final outcome    : an HR Manager, never the assessor or the employee.
///   * See                         : HR (Recruitment Officers, HR Managers) and holders of "See all departments"; the HOD
///                                   for their departments; the employee their own (My probation).
/// Each step is in the history and the audit trail; the signed initial meeting, each assessment and the outcome (with the
/// confirmation letter) are filed in the employee's personnel file.
/// </summary>
public class ProbationService(
    IProbationRepository probations,
    IRepository<ProbationAssessment> assessments,
    IRepository<ConfirmationRecord> confirmations,
    IRepository<Department> departments,
    IUserDirectory users,
    INumberGenerator numbers,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links,
    PersonnelFileService personnelFiles)
{
    private const string ConflictMessage = "Someone else updated this probation at the same moment. Please reload the page and try again.";
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool IsHr => Domain.Onboarding.OnboardingWorkflow.HrRoles.Any(me.IsInRole);

    // ===================================================================== Start (automatic)

    /// <summary>Creates the probation of a completed onboarding (once). The HOD and the employee are emailed.</summary>
    public async Task<int> StartForOnboardingAsync(OnboardingRecord o, CancellationToken ct = default)
    {
        if ((await probations.ListAsync(p => p.OnboardingRecordId == o.Id, ct)).FirstOrDefault() is { } existing) return existing.Id;
        var hod = (await departments.GetByIdAsync(o.DepartmentId, ct))?.HeadOfDepartmentUserId ?? o.RequestingHodUserId;
        var now = DateTime.UtcNow;
        var n = await numbers.NextAsync($"PRB-{now.Year}", ct);
        var p = new ProbationRecord
        {
            Number = $"PRB-{now.Year}-{n:0000}", EmployeeUserId = o.EmployeeUserId, EmployeeName = o.EmployeeName, StaffNumber = o.EmployeeNumber,
            JobTitle = o.JobTitle, DepartmentId = o.DepartmentId, OnboardingRecordId = o.Id, StartDate = o.StartDate,
            EndDate = o.StartDate.AddMonths(ProbationWorkflow.DurationMonths), OriginalEndDate = o.StartDate.AddMonths(ProbationWorkflow.DurationMonths),
            InitialMeetingDueDate = o.StartDate.AddDays(ProbationWorkflow.InitialMeetingDays), HodUserId = hod == o.EmployeeUserId ? null : hod,
            Status = ProbationStatus.InitialMeeting, StatusChangedAt = now
        };
        p.RecordStep(ProbationAction.Started, ProbationStatus.InitialMeeting, MyId, scope.UserName,
            $"{ProbationWorkflow.DurationMonths} months, to {p.EndDate.ToString("d MMM yyyy", Gb)}", now);
        probations.Add(p);
        await unitOfWork.SaveChangesAsync(ct);

        await mailer.ToUserAsync(p.HodUserId, $"Probation {p.Number}: initial meeting with {p.EmployeeName}",
            $"<b>{E(p.EmployeeName)}</b> ({E(p.JobTitle)}) has started a {ProbationWorkflow.DurationMonths}-month probation. Please hold the initial meeting " +
            $"by <b>{p.InitialMeetingDueDate.ToString("d MMMM yyyy", Gb)}</b>: agree the objectives and the development plan, and sign them.",
            links.Probation(p.Id), "Open the probation", ct);
        await mailer.ToUserIncludingMeAsync(p.EmployeeUserId, "Your probation has started",
            $"Your {ProbationWorkflow.DurationMonths}-month probation as <b>{E(p.JobTitle)}</b> runs to {p.EndDate.ToString("d MMMM yyyy", Gb)}. " +
            "Your Head of Department will meet you in your first week to agree your objectives.", links.MyProbation(), "See my probation", ct);
        return p.Id;
    }

    // ===================================================================== Reading

    public async Task<IReadOnlyList<ProbationListItemDto>> SearchAsync(ProbationFilter filter, CancellationToken ct = default)
    {
        var (all, mine) = await VisibilityAsync(ct);
        if (!all && mine.Count == 0) return [];
        IEnumerable<ProbationRecord> q = await probations.ListAsync(p => all || mine.Contains(p.DepartmentId), ct);
        if (filter.DepartmentId is { } dep) q = q.Where(p => p.DepartmentId == dep);
        var firstDone = (await assessments.ListAsync(a => a.Kind == ProbationAssessmentKind.First, ct)).Select(a => a.ProbationRecordId).ToHashSet();
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);

        var rows = q.Select(p =>
        {
            var (step, due) = NextStep(p, firstDone.Contains(p.Id));
            return new ProbationListItemDto(p.Id, p.Number, p.EmployeeName, p.StaffNumber, p.JobTitle, deptNames.GetValueOrDefault(p.DepartmentId, ""),
                p.StartDate, p.EndDate, p.Status, step, due, due is { } d && d < Today);
        });
        rows = filter.Tab switch
        {
            "due" => rows.Where(r => r.NextDue is { } d && d <= Today.AddDays(ProbationWorkflow.AssessmentWindowDays)),
            "decision" => rows.Where(r => r.Status == ProbationStatus.AwaitingHrDecision),
            "active" => rows.Where(r => ProbationWorkflow.IsActive(r.Status)),
            _ => rows
        };
        return rows.OrderBy(r => r.NextDue ?? DateOnly.MaxValue).ThenBy(r => r.EmployeeName).ToList();
    }

    /// <summary>What has to happen next and by when (null when nothing is due from the HOD).</summary>
    public static (string? Step, DateOnly? Due) NextStep(ProbationRecord p, bool firstAssessmentDone) => p.Status switch
    {
        ProbationStatus.InitialMeeting => (p.HodSignedAt is null ? "Initial meeting" : "Employee's signature", p.InitialMeetingDueDate),
        ProbationStatus.InProgress when !firstAssessmentDone => ("First assessment", p.FirstAssessmentDueDate),
        ProbationStatus.InProgress or ProbationStatus.Extended => ("Final assessment", p.EndDate),
        ProbationStatus.AwaitingHrDecision => ("HR decision", null),
        _ => (null, null)
    };

    public async Task<ProbationDetailsDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var p = await probations.GetFullAsync(id, ct);
        if (p is null) return null;
        var (all, mine) = await VisibilityAsync(ct);
        if (!all && !mine.Contains(p.DepartmentId) && p.EmployeeUserId != me.UserId) return null;
        return await DetailsAsync(p, ct);
    }

    /// <summary>The employee's own probations (normally one), for My probation.</summary>
    public async Task<IReadOnlyList<ProbationDetailsDto>> GetMineAsync(CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return [];
        var list = new List<ProbationDetailsDto>();
        foreach (var p in (await probations.ListAsync(x => x.EmployeeUserId == uid, ct)).OrderByDescending(x => x.StartDate))
            list.Add(await DetailsAsync((await probations.GetFullAsync(p.Id, ct))!, ct));
        return list;
    }

    /// <summary>The employee's confirmation letter (theirs only), or HR's view of it.</summary>
    public async Task<FiledLetterDto?> GetConfirmationLetterAsync(int probationId, CancellationToken ct = default)
    {
        var p = await probations.GetByIdAsync(probationId, ct);
        var c = p is null ? null : (await confirmations.ListAsync(x => x.ProbationRecordId == p.Id, ct)).FirstOrDefault();
        if (p is null || c?.PersonnelFileDocumentId is not int docId) return null;
        // The employee, and anyone who may see the probation (HR, the HOD), may read the confirmation letter.
        if (p.EmployeeUserId != me.UserId && await GetAsync(probationId, ct) is null) return null;
        return await personnelFiles.GetLetterUncheckedAsync(docId, ct);
    }

    private async Task<ProbationDetailsDto> DetailsAsync(ProbationRecord p, CancellationToken ct)
    {
        var list = (await assessments.ListAsync(a => a.ProbationRecordId == p.Id, ct)).OrderBy(a => a.AssessedAt).ToList();
        var names = await users.GetDisplayNamesAsync(list.Select(a => a.AssessorUserId).Concat([p.HodUserId, p.HrManagerUserId]).OfType<string>().Distinct(), ct);
        string? Name(string? uid) => uid is null ? null : names.GetValueOrDefault(uid);
        var confirmation = (await confirmations.ListAsync(c => c.ProbationRecordId == p.Id, ct)).FirstOrDefault();
        var isHod = await IsHodOfAsync(p, ct);
        var own = p.EmployeeUserId == me.UserId;
        var assessBlocked = AssessProblem(p, list);
        var lastFinal = list.LastOrDefault(a => a.Kind == ProbationAssessmentKind.Final);
        var decisionBlocked = HrDecisionProblem(p, lastFinal);

        return new ProbationDetailsDto
        {
            Id = p.Id, Number = p.Number, EmployeeName = p.EmployeeName, StaffNumber = p.StaffNumber, JobTitle = p.JobTitle,
            DepartmentName = (await departments.GetByIdAsync(p.DepartmentId, ct))?.Name ?? "", HodName = Name(p.HodUserId), HrManagerName = Name(p.HrManagerUserId),
            OnboardingRecordId = p.OnboardingRecordId, StartDate = p.StartDate, EndDate = p.EndDate, OriginalEndDate = p.OriginalEndDate, Status = p.Status,
            InitialMeetingDueDate = p.InitialMeetingDueDate, InitialMeetingDate = p.InitialMeetingDate, InitialMeetingNotes = p.InitialMeetingNotes,
            EmployeeSignedAt = p.EmployeeSignedAt, EmployeeSignatureName = p.EmployeeSignatureName, HodSignedAt = p.HodSignedAt, HodSignatureName = p.HodSignatureName,
            Objectives = p.Objectives.OrderBy(o => o.Id).Select(o => new ObjectiveRow { Objective = o.Objective, SuccessIndicators = o.SuccessIndicators, DueDate = o.DueDate }).ToList(),
            DevelopmentPlan = p.DevelopmentPlan.OrderBy(d => d.Id).Select(d => new DevelopmentPlanRow { TrainingNeed = d.TrainingNeed, DevelopmentNeed = d.DevelopmentNeed, Timeline = d.Timeline }).ToList(),
            FirstAssessmentDueDate = p.FirstAssessmentDueDate, ExtendedMonths = p.ExtendedMonths, ExtensionReason = p.ExtensionReason,
            ExtensionImprovementPlan = p.ExtensionImprovementPlan,
            Assessments = list.Select(a => new AssessmentDto(a.Id, a.Kind, Name(a.AssessorUserId) ?? "?", a.AssessedAt,
                [(ProbationRatingCategory.ApplicationOfSkills, a.ApplicationOfSkills), (ProbationRatingCategory.QualityOfWork, a.QualityOfWork),
                 (ProbationRatingCategory.Productivity, a.Productivity), (ProbationRatingCategory.Cooperation, a.Cooperation),
                 (ProbationRatingCategory.Dependability, a.Dependability), (ProbationRatingCategory.AbilityToSupervise, a.AbilityToSupervise)],
                a.Comments, a.ImprovementPlan, a.Outcome, a.ExtensionMonths, a.ExtensionReason, a.HrApproved, a.HrComments)).ToList(),
            History = p.Actions.OrderBy(a => a.ActionAt).ThenBy(a => a.Id)
                .Select(a => new ProbationHistoryDto(a.Action, a.FromStatus, a.ToStatus, a.ActorName, a.Comments, a.ActionAt)).ToList(),
            ConfirmationLetterNumber = confirmation?.LetterNumber,
            ConfirmationLetterDocumentId = confirmation?.PersonnelFileDocumentId,
            CanRecordInitialMeeting = isHod && !own && p.Status == ProbationStatus.InitialMeeting && p.EmployeeSignedAt is null,
            CanSignAsEmployee = own && p.Status == ProbationStatus.InitialMeeting && p.HodSignedAt is not null && p.EmployeeSignedAt is null,
            CanAssess = isHod && !own && assessBlocked is null,
            NextAssessment = ProbationWorkflow.CanBeAssessed(p.Status) ? NextKind(list) : null,
            AssessBlockedReason = isHod && !own && ProbationWorkflow.CanBeAssessed(p.Status) ? assessBlocked : null,
            CanHrDecide = me.IsInRole(Roles.HrManager) && !own && decisionBlocked is null,
            HrDecisionBlockedReason = me.IsInRole(Roles.HrManager) && p.Status == ProbationStatus.AwaitingHrDecision ? decisionBlocked : null
        };
    }

    // ===================================================================== Initial meeting

    /// <summary>The HOD records the initial meeting (objectives and development plan) and signs it; the employee is asked to sign.</summary>
    public async Task<Result> RecordInitialMeetingAsync(int id, InitialMeetingForm form, CancellationToken ct = default)
    {
        var p = await probations.GetFullAsync(id, ct);
        if (p is null) return Result.Failure("Probation not found.");
        if (!await IsHodOfAsync(p, ct) || p.EmployeeUserId == MyId) return Result.Failure("Only the Head of the employee's department holds the initial meeting.");
        if (p.Status != ProbationStatus.InitialMeeting || p.EmployeeSignedAt is not null) return Result.Failure("The initial meeting has already been agreed and signed.");

        var objectives = form.Objectives.Where(o => !o.IsEmpty).ToList();
        var plan = form.DevelopmentPlan.Where(d => !d.IsEmpty).ToList();
        var errors = new List<string>();
        if (form.MeetingDate is not { } date || date < p.StartDate.AddDays(-14) || date > Today) errors.Add("Enter the date of the meeting (not in the future).");
        if (objectives.Count == 0) errors.Add("Agree at least one objective.");
        if (objectives.Any(o => string.IsNullOrWhiteSpace(o.Objective) || string.IsNullOrWhiteSpace(o.SuccessIndicators)))
            errors.Add("Give each objective and how success will be measured (success indicators).");
        if (objectives.Any(o => o.DueDate is { } d && (d < p.StartDate || d > p.EndDate.AddMonths(ProbationWorkflow.MaxExtensionMonths))))
            errors.Add("Objectives must be due within the probation.");
        if (plan.Any(d => string.IsNullOrWhiteSpace(d.TrainingNeed) && string.IsNullOrWhiteSpace(d.DevelopmentNeed)))
            errors.Add("Give each line of the development plan a training or a development need.");
        if (objectives.Count > 20 || plan.Count > 20) errors.Add("List at most 20 objectives and 20 development plan lines.");
        var signature = Clean(form.SignatureName);
        if (signature is null || !SameName(signature, scope.UserName)) errors.Add($"Type your full name ({scope.UserName}) to sign.");
        if (errors.Count > 0) return Result.Failure([.. errors]);

        p.InitialMeetingDate = form.MeetingDate;
        p.InitialMeetingNotes = Clean(form.Notes);
        p.Objectives.Clear();
        foreach (var o in objectives)
            p.Objectives.Add(new ProbationObjective { Objective = o.Objective!.Trim(), SuccessIndicators = o.SuccessIndicators!.Trim(), DueDate = o.DueDate });
        p.DevelopmentPlan.Clear();
        foreach (var d in plan)
            p.DevelopmentPlan.Add(new ProbationDevelopmentPlan { TrainingNeed = Clean(d.TrainingNeed), DevelopmentNeed = Clean(d.DevelopmentNeed), Timeline = Clean(d.Timeline) });
        var now = DateTime.UtcNow;
        (p.HodSignedAt, p.HodSignatureName) = (now, signature);
        p.RecordStep(ProbationAction.InitialMeetingRecorded, ProbationStatus.InitialMeeting, MyId, scope.UserName,
            $"{objectives.Count} objective(s); signed by the Head of Department" + (form.MeetingDate > p.InitialMeetingDueDate ? " (held after the 7-day deadline)" : ""), now);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
            await mailer.ToUserIncludingMeAsync(p.EmployeeUserId, "Please sign your probation objectives",
                $"Your Head of Department has recorded your probation objectives and development plan. Please read and sign them.",
                links.MyProbation(), "See my probation", ct);
        return saved;
    }

    /// <summary>The employee signs the initial meeting record; probation is then In progress and the record is filed.</summary>
    public async Task<Result> SignAsEmployeeAsync(int id, string? typedName, CancellationToken ct = default)
    {
        var p = await probations.GetFullAsync(id, ct);
        if (p is null || p.EmployeeUserId != me.UserId) return Result.Failure("Probation not found.");
        if (p.Status != ProbationStatus.InitialMeeting || p.HodSignedAt is null) return Result.Failure("There is nothing to sign yet.");
        if (p.EmployeeSignedAt is not null) return Result.Failure("You have already signed.");
        typedName = Clean(typedName);
        if (typedName is null || !SameName(typedName, p.EmployeeName)) return Result.Failure($"Type your full name ({p.EmployeeName}) to sign.");

        var now = DateTime.UtcNow;
        (p.EmployeeSignedAt, p.EmployeeSignatureName) = (now, typedName);
        p.RecordStep(ProbationAction.EmployeeSigned, ProbationStatus.InProgress, MyId, p.EmployeeName, null, now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        await personnelFiles.FileLetterAsync(p.EmployeeUserId, PersonnelFileCategories.ProbationRecords, $"Probation {p.Number}: initial meeting, objectives and development plan",
            InitialMeetingHtml(p), "ProbationInitialMeeting", p.Id, now, ct);
        await mailer.ToUserAsync(p.HodUserId, $"Probation {p.Number}: objectives signed",
            $"<b>{E(p.EmployeeName)}</b> has signed the probation objectives. The first assessment is due on {p.FirstAssessmentDueDate.ToString("d MMMM yyyy", Gb)}.",
            links.Probation(p.Id), "Open the probation", ct);
        return saved;
    }

    // ===================================================================== Assessments

    /// <summary>The HOD records the first (3-month) or final assessment. A final assessment goes to the HR Manager for decision.</summary>
    public async Task<Result> AssessAsync(int id, AssessmentForm form, CancellationToken ct = default)
    {
        var p = await probations.GetFullAsync(id, ct);
        if (p is null) return Result.Failure("Probation not found.");
        if (!await IsHodOfAsync(p, ct) || p.EmployeeUserId == MyId) return Result.Failure("Only the Head of the employee's department assesses the probation.");
        var list = await assessments.ListAsync(a => a.ProbationRecordId == p.Id, ct);
        if (AssessProblem(p, list) is { } problem) return Result.Failure(problem);
        var kind = NextKind(list);

        var errors = new List<string>();
        if (new[] { form.ApplicationOfSkills, form.QualityOfWork, form.Productivity, form.Cooperation, form.Dependability }.Any(r => r is null))
            errors.Add("Rate every category (ability to supervise may be left empty when the job supervises nobody).");
        var comments = Clean(form.Comments);
        if (comments is null || comments.Length < 10) errors.Add("Write your comments on the employee's performance.");
        var plan = Clean(form.ImprovementPlan);
        if (kind == ProbationAssessmentKind.Final)
        {
            if (form.Outcome is null) errors.Add("Choose the outcome: confirm employment, extend probation, or do not confirm.");
            if (form.Outcome == ProbationOutcome.Extend)
            {
                var left = ProbationWorkflow.MaxExtensionMonths - p.ExtendedMonths;
                if (left <= 0) errors.Add($"The probation has already been extended by the maximum of {ProbationWorkflow.MaxExtensionMonths} months.");
                else if (form.ExtensionMonths is not { } m || m < 1 || m > left) errors.Add($"Extend by 1 to {left} month(s): at most {ProbationWorkflow.MaxExtensionMonths} months in total.");
                if (Clean(form.ExtensionReason) is null) errors.Add("Give the reason for the extension.");
                if (plan is null) errors.Add("Write the improvement plan for the extension.");
            }
        }
        if (errors.Count > 0) return Result.Failure([.. errors]);

        var now = DateTime.UtcNow;
        var a = new ProbationAssessment
        {
            ProbationRecordId = p.Id, Kind = kind, AssessorUserId = MyId, AssessedAt = now,
            ApplicationOfSkills = form.ApplicationOfSkills!.Value, QualityOfWork = form.QualityOfWork!.Value, Productivity = form.Productivity!.Value,
            Cooperation = form.Cooperation!.Value, Dependability = form.Dependability!.Value, AbilityToSupervise = form.AbilityToSupervise,
            Comments = comments!, ImprovementPlan = plan,
            Outcome = kind == ProbationAssessmentKind.Final ? form.Outcome : null,
            ExtensionMonths = form.Outcome == ProbationOutcome.Extend && kind == ProbationAssessmentKind.Final ? form.ExtensionMonths : null,
            ExtensionReason = form.Outcome == ProbationOutcome.Extend && kind == ProbationAssessmentKind.Final ? Clean(form.ExtensionReason) : null
        };
        assessments.Add(a);
        if (kind == ProbationAssessmentKind.First)
            p.RecordStep(ProbationAction.FirstAssessment, p.Status, MyId, scope.UserName, null, now);
        else
            p.RecordStep(ProbationAction.FinalAssessment, ProbationStatus.AwaitingHrDecision, MyId, scope.UserName, $"Recommendation: {EnumText.Label(a.Outcome!.Value)}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        await personnelFiles.FileLetterAsync(p.EmployeeUserId, PersonnelFileCategories.ProbationRecords, $"Probation {p.Number}: {EnumText.Label(kind)}",
            AssessmentHtml(p, a, scope.UserName), "ProbationAssessment", a.Id, now, ct);
        if (kind == ProbationAssessmentKind.Final)
            await mailer.ToRoleAsync(Roles.HrManager, [p.EmployeeUserId], $"Probation {p.Number} needs your decision",
                $"{E(scope.UserName)} has completed the final probation assessment of <b>{E(p.EmployeeName)}</b> ({E(p.JobTitle)}) and recommends: " +
                $"<b>{E(EnumText.Label(a.Outcome!.Value))}</b>.", links.Probation(p.Id), "Open the probation", ct);
        return saved;
    }

    // ===================================================================== HR Manager's decision

    /// <summary>
    /// The HR Manager approves the final assessment's recommendation (confirm: letter generated and filed; extend: new end
    /// date; do not confirm) or returns it to the HOD with comments.
    /// </summary>
    public async Task<Result> HrDecideAsync(int id, bool approve, string? comments, CancellationToken ct = default)
    {
        if (!me.IsInRole(Roles.HrManager)) return Result.Failure("Only an HR Manager decides the outcome of a probation.");
        var p = await probations.GetFullAsync(id, ct);
        if (p is null || p.EmployeeUserId == MyId) return Result.Failure("Probation not found.");
        var final = (await assessments.ListAsync(a => a.ProbationRecordId == p.Id && a.Kind == ProbationAssessmentKind.Final, ct)).OrderBy(a => a.AssessedAt).LastOrDefault();
        if (HrDecisionProblem(p, final) is { } problem) return Result.Failure(problem);
        comments = Clean(comments);
        if (!approve && (comments is null || comments.Length < 5)) return Result.Failure("Say what the Head of Department should look at again.");

        var now = DateTime.UtcNow;
        final!.HrApproved = approve;
        final.HrComments = comments;
        p.HrManagerUserId = MyId;
        if (!approve)
        {
            p.RecordStep(ProbationAction.ReturnedToHod, p.ExtendedMonths > 0 ? ProbationStatus.Extended : ProbationStatus.InProgress, MyId, scope.UserName, comments, now);
            var returned = await SaveAsync(ct);
            if (returned.Succeeded)
                await mailer.ToUserAsync(p.HodUserId, $"Probation {p.Number} returned",
                    $"The HR Manager returned the final assessment of <b>{E(p.EmployeeName)}</b>: {E(comments)}", links.Probation(p.Id), "Open the probation", ct);
            return returned;
        }

        ConfirmationRecord? confirmation = null;
        switch (final.Outcome)
        {
            case ProbationOutcome.Confirm:
                var n = await numbers.NextAsync($"CNF-{now.Year}", ct);
                confirmation = new ConfirmationRecord
                {
                    EmployeeUserId = p.EmployeeUserId, ProbationRecordId = p.Id, LetterNumber = $"CNF-{now.Year}-{n:0000}",
                    ConfirmationDate = Today > p.EndDate ? Today : p.EndDate, ApprovedByUserId = MyId
                };
                confirmations.Add(confirmation);
                p.RecordStep(ProbationAction.Confirmed, ProbationStatus.Confirmed, MyId, scope.UserName, comments, now);
                break;
            case ProbationOutcome.Extend:
                var months = final.ExtensionMonths!.Value;
                p.EndDate = p.EndDate.AddMonths(months);
                p.ExtendedMonths += months;
                p.ExtensionReason = final.ExtensionReason;
                p.ExtensionImprovementPlan = final.ImprovementPlan;
                p.RecordStep(ProbationAction.Extended, ProbationStatus.Extended, MyId, scope.UserName,
                    $"By {months} month(s), to {p.EndDate.ToString("d MMM yyyy", Gb)}. {comments}".Trim(), now);
                break;
            default:
                p.RecordStep(ProbationAction.NotConfirmed, ProbationStatus.NotConfirmed, MyId, scope.UserName, comments, now);
                break;
        }
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        var values = new Dictionary<string, string?> { ["JobTitle"] = p.JobTitle };
        if (confirmation is not null)
        {
            var filed = await personnelFiles.FileLetterAsync(p.EmployeeUserId, PersonnelFileCategories.GeneratedLetters, $"Confirmation of employment {confirmation.LetterNumber}",
                ConfirmationLetterHtml(p, confirmation, scope.UserName), "ConfirmationLetter", confirmation.Id, now, ct);
            if (filed.Succeeded)
            {
                confirmation.PersonnelFileDocumentId = filed.Value;
                await SaveAsync(ct);
            }
            values["ConfirmationDate"] = confirmation.ConfirmationDate.ToString("d MMMM yyyy", Gb);
            values["LetterNumber"] = confirmation.LetterNumber;
            await mailer.TemplateToUserAsync(p.EmployeeUserId, EmailTemplateCatalogue.ProbationConfirmed, values, links.MyProbation(), ct: ct);
        }
        else if (p.Status == ProbationStatus.Extended)
        {
            values["NewEndDate"] = p.EndDate.ToString("d MMMM yyyy", Gb);
            values["Reason"] = p.ExtensionReason;
            await mailer.TemplateToUserAsync(p.EmployeeUserId, EmailTemplateCatalogue.ProbationExtended, values, links.MyProbation(), ct: ct);
        }
        else
            await mailer.TemplateToUserAsync(p.EmployeeUserId, EmailTemplateCatalogue.ProbationNotConfirmed, values, links.MyProbation(), ct: ct);

        await mailer.ToUserAsync(p.HodUserId, $"Probation {p.Number}: {EnumText.Label(p.Status)}",
            $"The HR Manager approved the outcome of <b>{E(p.EmployeeName)}</b>'s probation: <b>{E(EnumText.Label(p.Status))}</b>" +
            (p.Status == ProbationStatus.Extended ? $" to {p.EndDate.ToString("d MMMM yyyy", Gb)}" : "") + ".", links.Probation(p.Id), "Open the probation", ct);
        return saved;
    }

    // ===================================================================== Rules

    private static ProbationAssessmentKind NextKind(IEnumerable<ProbationAssessment> list) =>
        list.Any(a => a.Kind == ProbationAssessmentKind.First) ? ProbationAssessmentKind.Final : ProbationAssessmentKind.First;

    /// <summary>Why the HOD can't assess now, or null.</summary>
    private static string? AssessProblem(ProbationRecord p, IReadOnlyCollection<ProbationAssessment> list)
    {
        if (p.Status == ProbationStatus.InitialMeeting) return "Hold and sign the initial meeting first.";
        if (!ProbationWorkflow.CanBeAssessed(p.Status)) return "The probation is not waiting for an assessment.";
        var kind = NextKind(list);
        var due = kind == ProbationAssessmentKind.First ? p.FirstAssessmentDueDate : p.EndDate;
        return Today < due.AddDays(-ProbationWorkflow.AssessmentWindowDays)
            ? $"The {(kind == ProbationAssessmentKind.First ? "first" : "final")} assessment can be recorded from {due.AddDays(-ProbationWorkflow.AssessmentWindowDays).ToString("d MMM yyyy", Gb)} (due {due.ToString("d MMM yyyy", Gb)})."
            : null;
    }

    private string? HrDecisionProblem(ProbationRecord p, ProbationAssessment? final)
    {
        if (p.Status != ProbationStatus.AwaitingHrDecision || final is null || final.HrApproved is not null) return "The probation is not waiting for a decision.";
        return final.AssessorUserId == me.UserId ? "You assessed this probation, so another HR Manager must decide." : null;
    }

    private async Task<(bool All, IReadOnlyCollection<int> Mine)> VisibilityAsync(CancellationToken ct)
    {
        var all = IsHr || me.HasPermission(Permissions.SeeAllDepartments);
        return (all, me.IsInRole(Roles.HeadOfDepartment) ? await scope.HodDepartmentIdsAsync(ct) : []);
    }

    private async Task<bool> IsHodOfAsync(ProbationRecord p, CancellationToken ct) =>
        me.IsInRole(Roles.HeadOfDepartment) && (await scope.HodDepartmentIdsAsync(ct)).Contains(p.DepartmentId);

    // ===================================================================== Records and letters (built from encoded values only)

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string InitialMeetingHtml(ProbationRecord p) =>
        $"<h3>Probation initial meeting: {E(p.EmployeeName)}</h3>" +
        $"<p>{E(p.JobTitle)}, probation {E(p.Number)}, {p.StartDate.ToString("d MMM yyyy", Gb)} to {p.EndDate.ToString("d MMM yyyy", Gb)}. " +
        $"Meeting held on {p.InitialMeetingDate?.ToString("d MMM yyyy", Gb)}.</p>" +
        "<h4>Objectives</h4><ol>" + string.Concat(p.Objectives.OrderBy(o => o.Id).Select(o =>
            $"<li><strong>{E(o.Objective)}</strong><br>Success indicators: {E(o.SuccessIndicators)}" +
            (o.DueDate is { } d ? $"<br>Due: {d.ToString("d MMM yyyy", Gb)}" : "") + "</li>")) + "</ol>" +
        (p.DevelopmentPlan.Count == 0 ? "" : "<h4>Development plan</h4><ul>" + string.Concat(p.DevelopmentPlan.OrderBy(d => d.Id).Select(d =>
            $"<li>Training: {E(d.TrainingNeed)}; development: {E(d.DevelopmentNeed)}; timeline: {E(d.Timeline)}</li>")) + "</ul>") +
        (p.InitialMeetingNotes is null ? "" : $"<p>{E(p.InitialMeetingNotes)}</p>") +
        $"<p><strong>Signed</strong> by {E(p.HodSignatureName)} (Head of Department) on {p.HodSignedAt?.ToString("d MMM yyyy HH:mm", Gb)} and by " +
        $"{E(p.EmployeeSignatureName)} (employee) on {p.EmployeeSignedAt?.ToString("d MMM yyyy HH:mm", Gb)}.</p>";

    private static string AssessmentHtml(ProbationRecord p, ProbationAssessment a, string assessor)
    {
        string R(ProbationRating? r) => r is { } x ? E(EnumText.Label(x)) : "Not applicable";
        return $"<h3>{E(EnumText.Label(a.Kind))}: {E(p.EmployeeName)}</h3>" +
               $"<p>{E(p.JobTitle)}, probation {E(p.Number)}. Assessed by {E(assessor)} on {a.AssessedAt.ToString("d MMM yyyy", Gb)}.</p><ul>" +
               $"<li>Application of skills and knowledge: {R(a.ApplicationOfSkills)}</li><li>Quality of work: {R(a.QualityOfWork)}</li>" +
               $"<li>Productivity: {R(a.Productivity)}</li><li>Cooperation: {R(a.Cooperation)}</li><li>Dependability: {R(a.Dependability)}</li>" +
               $"<li>Ability to supervise: {R(a.AbilityToSupervise)}</li></ul>" +
               $"<h4>Comments</h4><p>{E(a.Comments)}</p>" +
               (a.ImprovementPlan is null ? "" : $"<h4>Improvement plan</h4><p>{E(a.ImprovementPlan)}</p>") +
               (a.Outcome is { } o ? $"<p><strong>Recommendation:</strong> {E(EnumText.Label(o))}" +
                                     (a.ExtensionMonths is { } m ? $", by {m} month(s): {E(a.ExtensionReason)}" : "") + "</p>" : "");
    }

    private static string ConfirmationLetterHtml(ProbationRecord p, ConfirmationRecord c, string approver) =>
        $"<h3>Confirmation of employment</h3>" +
        $"<p><strong>Reference:</strong> {E(c.LetterNumber)}<br><strong>Date:</strong> {DateTime.UtcNow.ToString("d MMMM yyyy", Gb)}</p>" +
        $"<p>Dear {E(p.EmployeeName)},</p>" +
        $"<p>Following the satisfactory completion of your probation (staff number {E(p.StaffNumber)}), we are pleased to confirm your appointment as " +
        $"<strong>{E(p.JobTitle)}</strong> with effect from <strong>{c.ConfirmationDate.ToString("d MMMM yyyy", Gb)}</strong>.</p>" +
        "<p>All other terms and conditions of your appointment remain unchanged.</p>" +
        $"<p>Yours faithfully,<br>{E(approver)}<br>HR Manager</p>";



    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);
}
