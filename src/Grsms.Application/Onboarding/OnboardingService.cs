using System.Globalization;
using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Application.Vacancies;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using static Grsms.Application.Common.Text;
using static Grsms.Application.Common.Signatures;

namespace Grsms.Application.Onboarding;

/// <summary>
/// Appointment letters and onboarding (Module 9). See <see cref="OnboardingWorkflow"/>.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server; Phase 27 = permissions, defaults in brackets):
///   * Start onboarding, issue or revise the letter, set the document requirements, change first-day details, the checklist and
///     notes, cancel                                                   : onboarding.manage (Recruitment Officer).
///   * Ask the new employee for a correction                          : onboarding.verify-documents (Recruitment Officer).
///   * Activate a ready employee, close the induction                 : onboarding.complete (Recruitment Officer, HR Manager).
///   * See every onboarding, its documents and personal record        : onboarding.view (Recruitment Officer, HR Manager).
///   * Tick off a checklist item                     : its owner, the department's HOD (department items), or onboarding.manage.
///   * Read and sign the appointment letter, see the first-day information             : the new employee, in their portal.
///   * See an onboarding  : onboarding.view, the roles that see every department, the department's HOD, and anyone with a task.
/// Nobody manages the onboarding of their own application. There is no HR Manager approval any more (user's decision, 10 Nov 2026).
/// </summary>
public class OnboardingService(
    IApplicationRepository applications,
    IVacancyRepository vacancies,
    IRepository<Offer> offers,
    IRepository<OnboardingRecord> onboardings,
    IRepository<OnboardingTask> tasks,
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
    IRepository<EmployeeDocument> documents,
    IRepository<OnboardingActionEntry> history,
    DocumentVerificationService documentService,
    IEmployeeAccounts accounts,
    InductionAreaService induction,
    PersonnelFileService personnelFiles,
    Probation.ProbationService probation,
    OnboardingProgressService progress,
    IRepository<OnboardingDocumentType> documentTypes,
    Settings.ISettingsStore settings)
{
    private const string ConflictMessage = "Someone else updated this record at the same moment. Please reload the page and try again.";
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");

    /// <summary>Phase 27: working days the new employee has to provide the documents (System settings, default 10).</summary>
    private int DocumentDeadlineDays
    {
        get
        {
            var days = settings.GetInt(Domain.Settings.SettingCatalogue.OnboardingDocumentDays);
            return days > 0 ? days : OnboardingWorkflow.DocumentDeadlineWorkingDays;
        }
    }

    // Phase 27: permissions instead of the RO / HR Manager roles.
    private bool MayView => me.HasPermission(Permissions.ViewOnboarding);
    private bool MayManage => me.HasPermission(Permissions.ManageOnboarding);
    private bool MayVerify => me.HasPermission(Permissions.VerifyOnboardingDocuments);
    private bool MayComplete => me.HasPermission(Permissions.CompleteOnboarding);

    // ===================================================================== Starting

    /// <summary>Accepted offers with no onboarding yet (onboarding.manage only; empty for everyone else).</summary>
    public async Task<IReadOnlyList<OnboardingToStartDto>> ToStartAsync(OnboardingFilter? filter = null, CancellationToken ct = default)
    {
        if (!MayManage) return [];
        var accepted = await offers.ListAsync(o => o.Status == OfferStatus.Accepted, ct);
        var acceptedApps = accepted.Select(o => o.JobApplicationId).ToList();
        var started = (await onboardings.ListAsync(o => acceptedApps.Contains(o.JobApplicationId), ct)).Select(o => o.JobApplicationId).ToHashSet(); // audit P3
        IEnumerable<Offer> q = accepted.Where(o => !started.Contains(o.JobApplicationId));
        if (filter?.DivisionId is { } div) q = q.Where(o => o.DivisionId == div);
        if (filter?.DepartmentId is { } dep) q = q.Where(o => o.DepartmentId == dep);
        var list = q.ToList();

        var appIds = list.Select(o => o.JobApplicationId).ToList();
        var apps = (await applications.ListAsync(a => appIds.Contains(a.Id), ct)).ToDictionary(a => a.Id);
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        var divNames = (await divisions.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        return list.Where(o => apps.ContainsKey(o.JobApplicationId) && apps[o.JobApplicationId].ApplicantUserId != me.UserId)
            .OrderBy(o => o.StartDate)
            .Select(o => new OnboardingToStartDto(o.JobApplicationId, apps[o.JobApplicationId].ApplicationNumber, o.Number,
                apps[o.JobApplicationId].FullName, o.JobTitle, divNames.GetValueOrDefault(o.DivisionId, ""), deptNames.GetValueOrDefault(o.DepartmentId, ""),
                o.StartDate, o.RespondedAt ?? o.SentAt))
            .ToList();
    }

    /// <summary>The onboarding of an application, if there is one (for links from the hiring case).</summary>
    public async Task<int?> IdForApplicationAsync(int applicationId, CancellationToken ct = default) =>
        (await onboardings.ListAsync(o => o.JobApplicationId == applicationId, ct)).FirstOrDefault()?.Id;

    /// <summary>The "Start onboarding" form, pre-filled from the accepted offer and the vacancy.</summary>
    public async Task<Result<StartOnboardingForm>> GetStartFormAsync(int applicationId, CancellationToken ct = default)
    {
        var (app, offer, error) = await LoadStartableAsync(applicationId, ct);
        if (app is null) return Result.Failure<StartOnboardingForm>(error!);
        var v = (await vacancies.GetByIdAsync(app.VacancyId, ct))!;
        var form = new StartOnboardingForm
        {
            ApplicationId = app.Id,
            StartDate = offer!.StartDate,
            ReportTime = new TimeOnly(8, 0),
            ReportLocation = $"Human Resources office, {(await sites.GetByIdAsync(v.SiteId, ct))?.Name}",
            ContactPersonUserId = v.ReportingManagerUserId
        };
        await FillStartFormAsync(form, ct);
        return Result.Success(form);
    }

    /// <summary>Fills the read-only parts of the start form (names and the staff drop-down).</summary>
    public async Task FillStartFormAsync(StartOnboardingForm form, CancellationToken ct = default)
    {
        var app = await applications.GetByIdAsync(form.ApplicationId, ct);
        if (app is null) return;
        var offer = (await offers.ListAsync(o => o.JobApplicationId == app.Id && o.Status == OfferStatus.Accepted, ct)).FirstOrDefault();
        form.EmployeeName = app.FullName;
        form.JobTitle = offer?.JobTitle ?? "";
        form.OfferNumber = offer?.Number ?? "";
        form.DepartmentName = (await departments.GetByIdAsync(app.DepartmentId, ct))?.Name ?? "";
        form.Staff = await users.GetActiveStaffAsync(null, ct);
        form.EducationOptions = await documentService.EducationChoicesAsync(form.RequiredEducation, ct);
    }

    /// <summary>
    /// The RO starts onboarding ("Click to proceed to onboarding"): the record, the document checklist (with the education
    /// certificates ticked and the expatriate documents if ticked), the default departmental checklist (with owners) and a
    /// provisional personnel file are created. The new employee is welcomed and can upload at once; each task owner is emailed.
    /// </summary>
    public async Task<Result<int>> StartAsync(StartOnboardingForm form, CancellationToken ct = default)
    {
        var (app, offer, error) = await LoadStartableAsync(form.ApplicationId, ct);
        if (app is null) return Result.Failure<int>(error!);
        var location = Clean(form.ReportLocation);
        var notes = Clean(form.FirstDayNotes);
        if (FirstDayProblem(form.StartDate, location, notes) is { } problem) return Result.Failure<int>(problem);
        if (await ContactProblemAsync(form.ContactPersonUserId, app.ApplicantUserId, ct) is { } contactProblem) return Result.Failure<int>(contactProblem);
        var allTypes = await documentTypes.ListAsync(ct: ct);
        var education = form.RequiredEducation.Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (education.Any(c => !allTypes.Any(t => t.IsActive && t.Requirement == DocumentRequirement.ChosenPerHire && string.Equals(t.Code, c, StringComparison.OrdinalIgnoreCase))))
            return Result.Failure<int>("Choose the education certificates from the list.");

        var v = (await vacancies.GetByIdAsync(app.VacancyId, ct))!;
        var start = form.StartDate!.Value;
        var now = DateTime.UtcNow;
        var n = await numbers.NextAsync($"ONB-{now.Year}", ct);
        var record = new OnboardingRecord
        {
            Number = $"ONB-{now.Year}-{n:0000}",
            JobApplicationId = app.Id,
            OfferId = offer!.Id,
            VacancyId = v.Id,
            DivisionId = app.DivisionId,
            DepartmentId = app.DepartmentId,
            RequestingHodUserId = app.RequestingHodUserId,
            SiteId = v.SiteId,
            EmployeeUserId = app.ApplicantUserId,
            EmployeeName = app.FullName,
            JobTitle = offer.JobTitle,
            MonthlySalary = offer.MonthlySalary,
            StartDate = start,
            ReportTime = form.ReportTime ?? new TimeOnly(8, 0),
            ReportLocation = location!,
            ContactPersonUserId = Clean(form.ContactPersonUserId),
            FirstDayNotes = notes,
            LetterTerms = offer.Terms,
            Status = OnboardingStatus.Draft,
            StatusChangedAt = now,
            ExpectedCompletionDate = start.AddDays(OnboardingWorkflow.ExpectedCompletionDays),
            CreatedByUserId = MyId,
            IsExpatriate = form.IsExpatriate,
            DocumentsDueDate = OnboardingWorkflow.AddWorkingDays(Today, DocumentDeadlineDays)
        };
        record.RecordStep(OnboardingAction.Started, OnboardingStatus.Draft, MyId, scope.UserName, $"Starting {start:dd MMM yyyy}", now);
        onboardings.Add(record);
        app.RecordStep(ApplicationAction.OnboardingStarted, app.Status, MyId, scope.UserName, $"{record.Number}, starting {start:dd MMM yyyy}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

        // Phase 18 / 27: the document checklist (one "Not submitted" row per active document type).
        foreach (var d in DocumentVerificationService.NewChecklist(record, allTypes, education)) documents.Add(d);

        // The checklist, with a default owner per team.
        var owners = await DefaultOwnersAsync(v.RecruiterUserId, app, ct);
        var order = 0;
        var created = new List<OnboardingTask>();
        foreach (var d in OnboardingWorkflow.DefaultChecklist)
        {
            var t = new OnboardingTask
            {
                OnboardingRecordId = record.Id, Team = d.Team, Area = OnboardingWorkflow.AreaOf(d.Team), Title = d.Title, DueDate = start.AddDays(d.DueDays),
                AssignedToUserId = owners.GetValueOrDefault(d.Team), AssignedAt = now, SortOrder = ++order
            };
            tasks.Add(t);
            created.Add(t);
        }
        saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

        // Phase 27: the provisional personnel file (filled when the employee is activated and the induction closed).
        await personnelFiles.OpenProvisionalAsync(record, ct);
        await mailer.TemplateToUserAsync(record.EmployeeUserId, Domain.Settings.EmailTemplateCatalogue.OnboardingWelcome,
            new Dictionary<string, string?>
            {
                ["JobTitle"] = record.JobTitle, ["StartDate"] = start.ToString("dddd d MMMM yyyy", Gb),
                ["DueDate"] = record.DocumentsDueDate?.ToString("dddd d MMMM yyyy", Gb)
            },
            links.MyApplication(record.JobApplicationId), ct: ct);

        foreach (var group in created.Where(t => t.AssignedToUserId is not null && t.AssignedToUserId != MyId).GroupBy(t => t.AssignedToUserId))
            await mailer.ToUserAsync(group.Key, $"Onboarding {record.Number}: {group.Count()} task(s) for you",
                $"<b>{WorkflowMailer.Enc(app.FullName)}</b> joins GREL as {WorkflowMailer.Enc(record.JobTitle)} on {start.ToString("d MMMM yyyy", Gb)}. " +
                "Please complete these onboarding tasks:<ul>" +
                string.Concat(group.Select(t => $"<li>{WorkflowMailer.Enc(t.Title)} (by {t.DueDate?.ToString("d MMM yyyy", Gb)})</li>")) + "</ul>",
                links.Onboarding(record.Id), "Open the onboarding", ct);
        return Result.Success(record.Id);
    }

    // ===================================================================== The onboarding page

    public async Task<OnboardingDetailsDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null) return null;
        var list = (await tasks.ListAsync(t => t.OnboardingRecordId == o.Id, ct)).OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();
        var isHod = await IsHodOfAsync(o.DepartmentId, ct);
        if (!MayView && !me.HasPermission(Permissions.SeeAllDepartments) && !isHod && !list.Any(t => t.AssignedToUserId == me.UserId) &&
            o.ContactPersonUserId != me.UserId)
            return null;

        var app = (await applications.GetByIdAsync(o.JobApplicationId, ct))!;
        var v = (await vacancies.GetByIdAsync(o.VacancyId, ct))!;
        var offer = await offers.GetByIdAsync(o.OfferId, ct);
        var names = await users.GetDisplayNamesAsync(list.SelectMany(t => new[] { t.AssignedToUserId, t.CompletedByUserId, t.VerifiedByUserId })
            .Concat([o.ContactPersonUserId, o.LetterIssuedByUserId, o.CreatedByUserId]).OfType<string>().Distinct(), ct);
        string? Name(string? uid) => uid is null ? null : names.GetValueOrDefault(uid);

        var own = o.EmployeeUserId == me.UserId;
        var hr = MayView && !own;
        var manage = MayManage && !own && OnboardingWorkflow.IsActive(o.Status);
        var complete = MayComplete && !own;
        var snap = await progress.SnapshotAsync(o, ct);
        var inductionBlocked = InductionProblem(o, list);
        var steps = (await history.ListAsync(a => a.OnboardingRecordId == o.Id, ct)).OrderBy(a => a.ActionAt).ThenBy(a => a.Id)
            .Select(a => new OnboardingHistoryDto(a.Action, a.FromStatus, a.ToStatus, a.ActorName, a.Comments, a.ActionAt)).ToList();
        return new OnboardingDetailsDto
        {
            Id = o.Id,
            Number = o.Number,
            Status = o.Status,
            ApplicationId = app.Id,
            ApplicationNumber = app.ApplicationNumber,
            OfferNumber = offer?.Number ?? "",
            EmployeeName = o.EmployeeName,
            EmployeeEmail = app.Email,
            EmployeeNumber = o.EmployeeNumber,
            JobTitle = o.JobTitle,
            VacancyNumber = v.VacancyNumber,
            DivisionName = (await divisions.GetByIdAsync(o.DivisionId, ct))?.Name ?? "",
            DepartmentName = (await departments.GetByIdAsync(o.DepartmentId, ct))?.Name ?? "",
            SiteName = (await sites.GetByIdAsync(o.SiteId, ct))?.Name ?? "",
            MonthlySalary = o.MonthlySalary,
            StartDate = o.StartDate,
            ReportTime = o.ReportTime,
            ReportLocation = o.ReportLocation,
            ContactPersonUserId = o.ContactPersonUserId,
            ContactPerson = Name(o.ContactPersonUserId),
            FirstDayNotes = o.FirstDayNotes,
            LetterNumber = o.LetterNumber,
            LetterTerms = o.LetterTerms,
            LetterIssuedAt = o.LetterIssuedAt,
            LetterIssuedBy = Name(o.LetterIssuedByUserId),
            LetterSignedAt = o.LetterAcknowledgedAt,
            LetterSignedName = o.LetterAcknowledgedName,
            CreatedBy = Name(o.CreatedByUserId) ?? "?",
            CreatedAt = o.CreatedAt,
            CancelReason = o.CancelReason,
            Tasks = list.Select(t => new OnboardingTaskDto(t.Id, t.Team, t.Title, t.DueDate, t.AssignedToUserId, Name(t.AssignedToUserId), t.Status, t.Notes,
                    t.CompletedAt, Name(t.CompletedByUserId), OnboardingWorkflow.ChecklistOpen(o.Status) && CanUpdateTask(t, o, isHod),
                    t.Status == OnboardingTaskStatus.Open && t.DueDate < Today)
                {
                    Area = t.Area, Description = t.Description, AssignedAt = t.AssignedAt ?? t.CreatedAt, VerifiedAt = t.VerifiedAt,
                    VerifiedBy = Name(t.VerifiedByUserId),
                    CanVerify = manage && t.Status != OnboardingTaskStatus.Open && t.VerifiedAt is null
                })
                .ToList(),
            InductionChoices = complete && o.Status == OnboardingStatus.AwaitingActivation ? await InductionChoicesAsync(o, list, ct) : [],
            PersonnelFileId = personnelFiles.CanView ? await personnelFiles.IdForEmployeeAsync(o.EmployeeUserId, ct) : null,
            ExpectedCompletionDate = o.ExpectedCompletionDate,
            CompletedAt = o.CompletedAt,
            Notes = hr ? o.Notes : null,
            History = steps,
            Documents = hr ? await documentService.GetForHrAsync(snap, ct) : null,
            PersonalRecordSigned = hr ? snap.PersonalRecordSigned : null,
            CanManage = manage,
            CanEditStartDate = manage && !o.LetterAcknowledged,
            CanIssueLetter = manage && OnboardingWorkflow.LetterMayBeIssued(o.Status) && !o.LetterAcknowledged,
            CanRequestCorrection = MayVerify && !own && OnboardingWorkflow.BeforeActivation(o.Status),
            CorrectionMessage = hr ? o.CorrectionMessage : null,
            CanSetRequirements = manage && OnboardingWorkflow.BeforeActivation(o.Status),
            IsExpatriate = o.IsExpatriate,
            EducationChoices = hr ? DocumentVerificationService.EducationChoices(snap) : [],
            DocumentsDueDate = o.DocumentsDueDate,
            DocumentsDueDateExtended = o.DocumentsDueDateExtended,
            Outstanding = hr ? snap.Readiness.ForEmployee : [],
            Percent = snap.Percent,
            CanCompleteInduction = complete && o.Status == OnboardingStatus.AwaitingDepartmentalInduction && inductionBlocked is null,
            InductionBlockedReason = complete && o.Status == OnboardingStatus.AwaitingDepartmentalInduction ? inductionBlocked : null,
            CanActivate = complete && o.Status == OnboardingStatus.AwaitingActivation
        };
    }

    /// <summary>The staff drop-down for the contact person and task owners.</summary>
    public Task<IReadOnlyList<LookupItem>> StaffAsync(CancellationToken ct = default) => users.GetActiveStaffAsync(null, ct);

    /// <summary>HR changes the first-day details or the staff number. The start date is locked once the letter is signed.</summary>
    public async Task<Result> UpdateFirstDayAsync(int id, DateOnly? startDate, TimeOnly? reportTime, string? location, string? contactPersonUserId,
        string? notes, string? employeeNumber, CancellationToken ct = default)
    {
        var (o, error) = await LoadForHrAsync(id, ct);
        if (o is null) return Result.Failure(error!);
        location = Clean(location);
        notes = Clean(notes);
        employeeNumber = Clean(employeeNumber);
        startDate ??= o.StartDate;
        if (startDate != o.StartDate && o.LetterAcknowledged)
            return Result.Failure("The new employee has signed the appointment letter, so the start date can no longer be changed.");
        if (startDate != o.StartDate && FirstDayProblem(startDate, location, notes) is { } problem) return Result.Failure(problem);
        if (location is null) return Result.Failure("Say where the new employee should report.");
        if (location.Length > 300) return Result.Failure("Keep the place under 300 characters.");
        if (notes?.Length > 4000) return Result.Failure("The first-day notes must be 4,000 characters or fewer.");
        if (employeeNumber?.Length > 30) return Result.Failure("The staff number must be 30 characters or fewer.");
        if (await ContactProblemAsync(contactPersonUserId, o.EmployeeUserId, ct) is { } contactProblem) return Result.Failure(contactProblem);

        // Moving the start date moves the open checklist items with it.
        var shift = startDate.Value.DayNumber - o.StartDate.DayNumber;
        if (shift != 0)
            foreach (var t in await tasks.ListAsync(t => t.OnboardingRecordId == o.Id && t.Status == OnboardingTaskStatus.Open && t.DueDate != null, ct))
                t.DueDate = t.DueDate!.Value.AddDays(shift);

        var firstDayChanged = startDate != o.StartDate || reportTime is { } rt && rt != o.ReportTime || location != o.ReportLocation ||
                              Clean(contactPersonUserId) != o.ContactPersonUserId || notes != o.FirstDayNotes;
        if (shift != 0 && o.ExpectedCompletionDate is { } expected) o.ExpectedCompletionDate = expected.AddDays(shift);
        o.StartDate = startDate.Value;
        o.ReportTime = reportTime ?? o.ReportTime;
        o.ReportLocation = location;
        o.ContactPersonUserId = Clean(contactPersonUserId);
        o.FirstDayNotes = notes;
        o.EmployeeNumber = employeeNumber;
        o.Version = Guid.NewGuid();
        var saved = await SaveAsync(ct);

        if (saved.Succeeded && firstDayChanged && o.LetterIssued)
            await mailer.ToUserAsync(o.EmployeeUserId, "Your first day at GREL: updated details",
                $"Your first-day details have changed. Please report on <b>{o.StartDate.ToString("dddd d MMMM yyyy", Gb)}</b> at " +
                $"<b>{o.ReportTime:HH:mm}</b>, {WorkflowMailer.Enc(o.ReportLocation)}.", links.MyApplication(o.JobApplicationId), "See my first-day information", ct);
        return saved;
    }

    // ===================================================================== Appointment letter

    /// <summary>The RO issues the appointment letter (or revises it before it is signed). The new employee is emailed.</summary>
    public async Task<Result> IssueLetterAsync(int id, string? terms, CancellationToken ct = default)
    {
        if (!MayManage) return Result.Failure("Only a Recruitment Officer can issue the appointment letter.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null) return Result.Failure("Onboarding not found.");
        if (o.EmployeeUserId == MyId) return Result.Failure("This is your own appointment.");
        if (!OnboardingWorkflow.LetterMayBeIssued(o.Status)) return Result.Failure("The appointment letter can no longer be issued at this stage.");
        if (o.LetterAcknowledged) return Result.Failure("The new employee has already signed the letter, so it can no longer be changed.");
        terms = Clean(terms);
        if (terms is null || terms.Length < 20) return Result.Failure("Write the conditions of the appointment (at least 20 characters).");
        if (terms.Length > 8000) return Result.Failure("The conditions must be 8,000 characters or fewer.");

        var app = (await applications.GetWithHistoryAsync(o.JobApplicationId, ct))!;
        var now = DateTime.UtcNow;
        var revised = o.LetterIssued;
        if (!revised)
        {
            var n = await numbers.NextAsync($"APT-{now.Year}", ct);
            o.LetterNumber = $"APT-{now.Year}-{n:0000}";
        }
        o.LetterTerms = terms;
        o.LetterIssuedAt = now;
        o.LetterIssuedByUserId = MyId;
        o.Version = Guid.NewGuid();
        // Phase 27: the stage now follows from the facts (usually "Awaiting the new employee": the letter is to be signed).
        o.RecordStep(OnboardingAction.LetterIssued, await progress.TargetAsync(o, ct), MyId, scope.UserName,
            $"{o.LetterNumber}{(revised ? " (revised)" : "")}", now);
        app.RecordStep(ApplicationAction.LetterIssued, app.Status, MyId, scope.UserName, $"{o.LetterNumber}{(revised ? " (revised)" : "")}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        if (revised)
            await mailer.ToUserAsync(o.EmployeeUserId, $"Your revised appointment letter: {o.JobTitle}",
                $"Your revised appointment letter for the position of <b>{WorkflowMailer.Enc(o.JobTitle)}</b> is ready. " +
                "Please read it and sign it in your portal. It also tells you when and where to report on your first day " +
                $"(<b>{o.StartDate.ToString("dddd d MMMM yyyy", Gb)}</b>).",
                links.MyApplication(o.JobApplicationId), "Read my appointment letter", ct);
        else
        {
            await mailer.TemplateToUserAsync(o.EmployeeUserId, Domain.Settings.EmailTemplateCatalogue.OnboardingDocumentsRequested,
                new Dictionary<string, string?> { ["JobTitle"] = o.JobTitle, ["StartDate"] = o.StartDate.ToString("dddd d MMMM yyyy", Gb) },
                links.MyApplication(o.JobApplicationId), ct: ct);
            await mailer.TextAsync(app.Phone, $"GREL: your appointment letter for {o.JobTitle} is ready. Please sign in to GRSMS to sign it and upload your documents.", ct);
        }
        return saved;
    }

    // ===================================================================== The new employee's side

    /// <summary>My appointment (letter and first-day information) for one of my applications, or null.</summary>
    public async Task<MyAppointmentDto?> GetMyAppointmentAsync(int applicationId, CancellationToken ct = default)
    {
        var o = (await onboardings.ListAsync(x => x.JobApplicationId == applicationId, ct)).FirstOrDefault();
        if (o is null || o.EmployeeUserId != me.UserId || o.Status == OnboardingStatus.Cancelled) return null;
        return await AppointmentAsync(o, ct);
    }

    /// <summary>The letter and first-day information exactly as the new employee sees them, for staff who may see the onboarding.</summary>
    public async Task<MyAppointmentDto?> GetLetterPreviewAsync(int id, CancellationToken ct = default)
    {
        if (await GetAsync(id, ct) is null) return null; // applies the visibility rules
        return await AppointmentAsync((await onboardings.GetByIdAsync(id, ct))!, ct);
    }

    private async Task<MyAppointmentDto> AppointmentAsync(OnboardingRecord o, CancellationToken ct)
    {
        var v = (await vacancies.GetByIdAsync(o.VacancyId, ct))!;
        var contact = o.ContactPersonUserId is null ? null : await users.GetContactAsync(o.ContactPersonUserId, ct);
        var manager = v.ReportingManagerUserId is null ? null : (await users.GetDisplayNamesAsync([v.ReportingManagerUserId], ct)).GetValueOrDefault(v.ReportingManagerUserId);
        var toBring = (await tasks.ListAsync(t => t.OnboardingRecordId == o.Id && t.Team == OnboardingTeam.NewEmployee, ct))
            .Where(t => t.Status != OnboardingTaskStatus.NotNeeded).OrderBy(t => t.SortOrder)
            .Select(t => (t.Title, t.Status == OnboardingTaskStatus.Done)).ToList();
        return new MyAppointmentDto(o.Id, o.LetterNumber, o.EmployeeName, o.JobTitle, (await departments.GetByIdAsync(o.DepartmentId, ct))?.Name ?? "",
            (await sites.GetByIdAsync(o.SiteId, ct))?.Name ?? "", manager, o.MonthlySalary, o.StartDate, o.ReportTime, o.ReportLocation,
            contact?.FullName, contact?.Email, o.FirstDayNotes, o.LetterIssued ? o.LetterTerms : null, o.LetterIssuedAt, o.LetterAcknowledgedAt,
            o.LetterAcknowledgedName, o.Status, toBring);
    }

    /// <summary>The new employee signs the appointment letter by typing their full name as it appears on the letter.</summary>
    public async Task<Result> SignLetterAsync(int applicationId, string? typedName, CancellationToken ct = default)
    {
        var o = (await onboardings.ListAsync(x => x.JobApplicationId == applicationId, ct)).FirstOrDefault();
        if (o is null || o.EmployeeUserId != me.UserId) return Result.Failure("Appointment letter not found.");
        if (!OnboardingWorkflow.IsActive(o.Status) || !o.LetterIssued) return Result.Failure("There is no appointment letter waiting for your signature.");
        if (o.LetterAcknowledged) return Result.Failure("You have already signed this letter.");
        typedName = Clean(typedName);
        if (typedName is null || !SameName(typedName, o.EmployeeName))
            return Result.Failure($"Type your full name exactly as it appears on the letter ({o.EmployeeName}) to sign.");

        var app = (await applications.GetWithHistoryAsync(o.JobApplicationId, ct))!;
        var now = DateTime.UtcNow;
        o.LetterAcknowledgedAt = now;
        o.LetterAcknowledgedName = typedName;
        o.LetterAcknowledgedIp = me.IpAddress;
        o.Version = Guid.NewGuid();
        app.RecordStep(ApplicationAction.LetterSigned, app.Status, MyId, o.EmployeeName, o.LetterNumber, now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;
        await progress.RefreshAsync(o.Id, ct);

        foreach (var uid in new[] { o.LetterIssuedByUserId, o.CreatedByUserId }.OfType<string>().Distinct())
            await mailer.ToUserAsync(uid, $"{o.LetterNumber} signed",
                $"<b>{WorkflowMailer.Enc(o.EmployeeName)}</b> has signed the appointment letter for {WorkflowMailer.Enc(o.JobTitle)} " +
                $"(starts {o.StartDate.ToString("d MMMM yyyy", Gb)}).", links.Onboarding(o.Id), "Open the onboarding", ct);
        return saved;
    }

    // ===================================================================== Checklist

    /// <summary>Marks a checklist item done, not needed, or back to "to do", with optional notes.</summary>
    public async Task<Result> UpdateTaskAsync(int taskId, OnboardingTaskStatus status, string? notes, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(taskId, ct);
        if (t is null) return Result.Failure("Task not found.");
        var o = (await onboardings.GetByIdAsync(t.OnboardingRecordId, ct))!;
        if (!OnboardingWorkflow.ChecklistOpen(o.Status)) return Result.Failure("This onboarding is no longer in progress.");
        if (!CanUpdateTask(t, o, await IsHodOfAsync(o.DepartmentId, ct)))
            return Result.Failure("Only the person responsible, the Head of Department (department tasks) or HR can update this task.");
        notes = Clean(notes);
        if (status == OnboardingTaskStatus.NotNeeded && notes is null) return Result.Failure("Say why this task is not needed.");
        if (notes?.Length > 2000) return Result.Failure("Notes must be 2,000 characters or fewer.");

        t.Status = status;
        t.Notes = notes ?? (status == OnboardingTaskStatus.Open ? t.Notes : null);
        var now = DateTime.UtcNow;
        (t.CompletedAt, t.CompletedByUserId) = status == OnboardingTaskStatus.Open ? ((DateTime?)null, (string?)null) : (now, MyId);
        // Phase 20: what HR marks done is verified at once; what anyone else marks done waits for HR to verify it.
        (t.VerifiedAt, t.VerifiedByUserId) = status != OnboardingTaskStatus.Open && MayManage ? (now, MyId) : ((DateTime?)null, (string?)null);
        // The record's Version is deliberately not changed, so two people ticking different items at once both succeed.
        return await SaveAsync(ct);
    }

    /// <summary>Phase 20: HR confirms that a task someone else marked done (or not needed) really is.</summary>
    public async Task<Result> VerifyTaskAsync(int taskId, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(taskId, ct);
        if (t is null) return Result.Failure("Task not found.");
        var (o, error) = await LoadForHrAsync(t.OnboardingRecordId, ct);
        if (o is null) return Result.Failure(error!);
        if (t.Status == OnboardingTaskStatus.Open) return Result.Failure("The task is still to do.");
        if (t.VerifiedAt is not null) return Result.Failure("This task has already been verified.");
        (t.VerifiedAt, t.VerifiedByUserId) = (DateTime.UtcNow, MyId);
        return await SaveAsync(ct);
    }

    /// <summary>HR gives a task to someone else and/or changes its due date. The new owner is emailed.</summary>
    public async Task<Result> AssignTaskAsync(int taskId, string? userId, DateOnly? dueDate, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(taskId, ct);
        if (t is null) return Result.Failure("Task not found.");
        var (o, error) = await LoadForHrAsync(t.OnboardingRecordId, ct);
        if (o is null) return Result.Failure(error!);
        userId = Clean(userId);
        if (userId is not null && (userId == o.EmployeeUserId || !await users.IsActiveStaffAsync(userId, ct)))
            return Result.Failure("Choose an active GREL staff member (not the new employee).");
        var newOwner = userId != t.AssignedToUserId ? userId : null;
        if (userId != t.AssignedToUserId) t.AssignedAt = DateTime.UtcNow;
        t.AssignedToUserId = userId;
        t.DueDate = dueDate;
        var saved = await SaveAsync(ct);
        if (saved.Succeeded && newOwner is not null && newOwner != MyId)
            await mailer.ToUserAsync(newOwner, $"Onboarding {o.Number}: a task for you",
                $"Please complete this task for <b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}, starts " +
                $"{o.StartDate.ToString("d MMMM yyyy", Gb)}): <b>{WorkflowMailer.Enc(t.Title)}</b>{(dueDate is null ? "" : $", by {dueDate.Value.ToString("d MMMM yyyy", Gb)}")}.",
                links.Onboarding(o.Id), "Open the onboarding", ct);
        return saved;
    }

    /// <param name="area">Phase 20: the induction area; null with <paramref name="team"/> Department = the new employee's own department.</param>
    public async Task<Result> AddTaskAsync(int id, OnboardingTeam team, string? title, DateOnly? dueDate, string? userId, CancellationToken ct = default,
        InductionArea? area = null, string? description = null)
    {
        var (o, error) = await LoadForHrAsync(id, ct);
        if (o is null) return Result.Failure(error!);
        title = Clean(title);
        description = Clean(description);
        if (title is null) return Result.Failure("Describe the task.");
        if (title.Length > 300) return Result.Failure("Keep the task under 300 characters.");
        if (description?.Length > 2000) return Result.Failure("Keep the description under 2,000 characters.");
        if (area is { } a && !Enum.IsDefined(a)) return Result.Failure("Unknown induction area.");
        var order = (await tasks.ListAsync(t => t.OnboardingRecordId == o.Id, ct)).Select(t => t.SortOrder).DefaultIfEmpty(0).Max() + 1;
        var t = new OnboardingTask
        {
            OnboardingRecordId = o.Id, Team = area is { } ar ? OnboardingWorkflow.TeamOf(ar) : team, Area = area ?? OnboardingWorkflow.AreaOf(team),
            Title = title, Description = description, AssignedAt = DateTime.UtcNow, SortOrder = order
        };
        tasks.Add(t);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded || (Clean(userId) is null && dueDate is null)) return saved;
        return await AssignTaskAsync(t.Id, userId, dueDate, ct);
    }

    /// <summary>Removes a checklist item that is still "to do" (soft delete; it stays in the audit trail).</summary>
    public async Task<Result> RemoveTaskAsync(int taskId, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(taskId, ct);
        if (t is null) return Result.Failure("Task not found.");
        var (o, error) = await LoadForHrAsync(t.OnboardingRecordId, ct);
        if (o is null) return Result.Failure(error!);
        if (t.Status != OnboardingTaskStatus.Open) return Result.Failure("Only tasks still to do can be removed. Mark it \"not needed\" instead.");
        tasks.Remove(t);
        return await SaveAsync(ct);
    }

    // ===================================================================== Phase 27: the workflow stages

    /// <summary>
    /// The RO asks the new employee to correct something that is not a document (usually the personal record). The onboarding
    /// stays with the new employee until they confirm the correction (<see cref="ConfirmCorrectionAsync"/>). The new employee is
    /// emailed the message and the rejected documents, if any.
    /// </summary>
    public async Task<Result> RequestCorrectionAsync(int id, string? message, CancellationToken ct = default)
    {
        if (!MayVerify) return Result.Failure("Only a Recruitment Officer can ask for a correction.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null || o.EmployeeUserId == MyId) return Result.Failure("Onboarding not found.");
        if (!OnboardingWorkflow.BeforeActivation(o.Status)) return Result.Failure("The employee is activated; corrections are no longer requested here.");
        message = Clean(message);
        if (message is null || message.Length < 5) return Result.Failure("Write what the new employee must correct (e.g. in the personal record).");
        if (message.Length > 2000) return Result.Failure("Keep the message under 2,000 characters.");

        var now = DateTime.UtcNow;
        o.CorrectionRequestedAt = now;
        o.CorrectionMessage = message;
        o.RecordStep(OnboardingAction.CorrectionRequested, o.Status, MyId, scope.UserName, message, now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        var rejected = (await progress.SnapshotAsync(o, ct)).Documents.Where(x => x.Document.Status == EmployeeDocumentStatus.Rejected).ToList();
        await mailer.TemplateToUserAsync(o.EmployeeUserId, Domain.Settings.EmailTemplateCatalogue.OnboardingDocumentsReturned,
            new Dictionary<string, string?>
            {
                ["Documents"] = message + (rejected.Count == 0 ? "" : "\n\n" + string.Join("\n", rejected.Select(x => $"- {x.Type.Name}: {x.Document.RejectionReason}")))
            },
            links.MyApplication(o.JobApplicationId), ct: ct);
        await progress.RefreshAsync(o.Id, ct);
        return saved;
    }

    /// <summary>The new employee confirms they made the correction the RO asked for. The RO is told.</summary>
    public async Task<Result> ConfirmCorrectionAsync(int onboardingId, CancellationToken ct = default)
    {
        var o = await onboardings.GetByIdAsync(onboardingId, ct);
        if (o is null || o.EmployeeUserId != me.UserId) return Result.Failure("Onboarding not found.");
        if (o.CorrectionRequestedAt is null) return Result.Failure("HR has not asked you for a correction.");
        var message = o.CorrectionMessage;
        o.CorrectionRequestedAt = null;
        o.CorrectionMessage = null;
        o.RecordStep(OnboardingAction.Submitted, o.Status, MyId, o.EmployeeName, $"Correction made: {message}", DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;
        foreach (var uid in await progress.HrOwnersAsync(o, ct))
            await mailer.ToUserAsync(uid, $"Onboarding {o.Number}: correction made",
                $"<b>{WorkflowMailer.Enc(o.EmployeeName)}</b> says the correction you asked for is made: {WorkflowMailer.Enc(message)}",
                links.Onboarding(o.Id), "Open the onboarding", ct);
        await progress.RefreshAsync(o.Id, ct);
        return saved;
    }

    /// <summary>
    /// The RO or HR Manager (onboarding.complete) activates a ready employee with their staff number: the account becomes a GREL
    /// staff account (Employee role, department, site), the personnel file is filled, the probation starts and the departmental
    /// induction tasks of the ticked areas are created (→ "Induction in progress"). The employee, the task owners and the HOD are
    /// emailed.
    /// </summary>
    /// <param name="areas">The induction areas whose tasks are added; null = the areas ticked by default on the Induction areas screen.</param>
    public async Task<Result> ActivateAsync(int id, string? staffNumber, CancellationToken ct = default, IReadOnlyCollection<InductionArea>? areas = null)
    {
        if (!MayComplete) return Result.Failure("Only a Recruitment Officer or HR Manager can activate the employee.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null || o.EmployeeUserId == MyId) return Result.Failure("Onboarding not found.");
        if (o.Status != OnboardingStatus.AwaitingActivation)
            return Result.Failure(o.Status is OnboardingStatus.AwaitingDepartmentalInduction or OnboardingStatus.Completed
                ? "The employee is already activated."
                : "The employee can be activated once every required document is verified and the letter and personal record are signed.");
        // The stage is worked out from the facts, but check them again: a document may have changed a moment ago.
        if (await progress.TargetAsync(o, ct) != OnboardingStatus.AwaitingActivation)
        {
            await progress.RefreshAsync(o.Id, ct);
            return Result.Failure("Something changed: not every required document is verified any more. Reload the page.");
        }
        staffNumber = Clean(staffNumber)?.ToUpperInvariant() ?? o.EmployeeNumber;
        if (staffNumber is null) return Result.Failure("Enter the staff number from payroll.");
        if (staffNumber.Length > 30) return Result.Failure("The staff number must be 30 characters or fewer.");

        var activated = await accounts.ActivateAsStaffAsync(o.EmployeeUserId, staffNumber, o.DepartmentId, o.SiteId, ct);
        if (!activated.Succeeded) return activated;

        var now = DateTime.UtcNow;
        // Phase 20 / 27: the departmental induction tasks of the chosen areas, each to the area's owner (HR when it has none).
        var rules = await induction.RulesAsync(ct);
        var chosen = (areas ?? rules.Values.Where(r => r.IsDefault).Select(r => r.Area).ToList()).Where(Enum.IsDefined).Distinct().Order().ToList();
        var existing = await tasks.ListAsync(t => t.OnboardingRecordId == o.Id, ct);
        var titles = existing.Select(t => t.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var order = existing.Select(t => t.SortOrder).DefaultIfEmpty(0).Max();
        var due = (o.StartDate > Today ? o.StartDate : Today).AddDays(OnboardingWorkflow.InductionTaskDueDays);
        var hrOwner = await HrFallbackOwnerAsync(o, ct);
        foreach (var area in chosen)
        {
            var owner = rules[area].OwnerUserId is { } ow && ow != o.EmployeeUserId ? ow : hrOwner;
            foreach (var title in rules[area].Tasks.Where(titles.Add))
                tasks.Add(new OnboardingTask
                {
                    OnboardingRecordId = o.Id, Area = area, Team = OnboardingWorkflow.TeamOf(area), Title = title, DueDate = due,
                    AssignedToUserId = owner, AssignedAt = now, SortOrder = ++order
                });
        }

        var app = (await applications.GetWithHistoryAsync(o.JobApplicationId, ct))!;
        o.EmployeeNumber = staffNumber;
        o.RecordStep(OnboardingAction.Activated, OnboardingStatus.AwaitingDepartmentalInduction, MyId, scope.UserName,
            $"Staff number {staffNumber}." + (chosen.Count == 0 ? "" : $" Induction areas: {string.Join(", ", chosen.Select(a => EnumText.Label(a)))}."), now);
        app.RecordStep(ApplicationAction.OnboardingCompleted, app.Status, MyId, scope.UserName, $"{o.Number}: activated", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        // Phase 20: fill the personnel file (the induction record is filed when the induction closes).
        await personnelFiles.OpenForOnboardingAsync(o, ct, includeInductionRecord: false);
        // Phase 21: the probation starts automatically (the HOD and the employee are emailed).
        await probation.StartForOnboardingAsync(o, ct);
        await mailer.TemplateToUserAsync(o.EmployeeUserId, Domain.Settings.EmailTemplateCatalogue.OnboardingActivated,
            new Dictionary<string, string?> { ["JobTitle"] = o.JobTitle, ["StaffNumber"] = staffNumber }, links.MyApplication(o.JobApplicationId), ct: ct);
        var owners = (await tasks.ListAsync(t => t.OnboardingRecordId == o.Id && t.Status == OnboardingTaskStatus.Open, ct))
            .Select(t => t.AssignedToUserId).OfType<string>().Concat(await HodsAsync(o, ct)).Distinct();
        foreach (var uid in owners)
            await mailer.ToUserAsync(uid, $"Onboarding {o.Number}: induction can begin",
                $"<b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}, starts {o.StartDate.ToString("d MMMM yyyy", Gb)}) is now an " +
                $"active GREL employee, staff number {WorkflowMailer.Enc(staffNumber)}. Please complete your induction tasks.",
                links.Onboarding(o.Id), "Open the onboarding", ct);
        return saved;
    }

    /// <summary>
    /// Every induction task is done (or not needed) and verified, and the start date has come: the onboarding is Completed and the
    /// induction record is filed in the personnel file.
    /// </summary>
    public async Task<Result> CompleteInductionAsync(int id, CancellationToken ct = default)
    {
        if (!MayComplete) return Result.Failure("Only a Recruitment Officer or HR Manager can close the induction.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null || o.EmployeeUserId == MyId) return Result.Failure("Onboarding not found.");
        if (InductionProblem(o, await tasks.ListAsync(t => t.OnboardingRecordId == o.Id, ct)) is { } problem) return Result.Failure(problem);

        var now = DateTime.UtcNow;
        o.CompletedAt = now;
        o.RecordStep(OnboardingAction.InductionCompleted, OnboardingStatus.Completed, MyId, scope.UserName, null, now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;
        await personnelFiles.OpenForOnboardingAsync(o, ct);
        foreach (var hod in await HodsAsync(o, ct))
            await mailer.ToUserAsync(hod, $"Onboarding {o.Number} completed",
                $"The induction of <b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}) is complete.",
                links.Onboarding(o.Id), "Open the onboarding", ct);
        return saved;
    }

    /// <summary>
    /// Phase 20: opens the personnel file of an onboarding completed before personnel files existed (or files anything
    /// missing). HR with "Open personnel files" only.
    /// </summary>
    public async Task<Result<int>> OpenPersonnelFileAsync(int id, CancellationToken ct = default)
    {
        if (!MayView || !personnelFiles.CanView) return Result.Failure<int>("Only HR with access to personnel files can do this.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null || o.EmployeeUserId == MyId) return Result.Failure<int>("Onboarding not found.");
        if (o.Status != OnboardingStatus.Completed) return Result.Failure<int>("A personnel file is opened when the onboarding is completed.");
        return Result.Success(await personnelFiles.OpenForOnboardingAsync(o, ct));
    }

    /// <summary>HR's internal notes on the onboarding.</summary>
    public async Task<Result> SaveNotesAsync(int id, string? notes, CancellationToken ct = default)
    {
        var (o, error) = await LoadForHrAsync(id, ct);
        if (o is null) return Result.Failure(error!);
        notes = Clean(notes);
        if (notes?.Length > 4000) return Result.Failure("Keep the notes under 4,000 characters.");
        o.Notes = notes;
        o.Version = Guid.NewGuid();
        return await SaveAsync(ct);
    }

    public async Task<Result> CancelAsync(int id, string? reason, CancellationToken ct = default)
    {
        var (o, error) = await LoadForHrAsync(id, ct);
        if (o is null) return Result.Failure(error!);
        reason = Clean(reason);
        if (reason is null || reason.Length < 5) return Result.Failure("Give the reason for cancelling (e.g. the new employee did not report).");
        if (reason.Length > 2000) return Result.Failure("Keep the reason under 2,000 characters.");

        var app = (await applications.GetWithHistoryAsync(o.JobApplicationId, ct))!;
        var now = DateTime.UtcNow;
        o.CancelReason = reason;
        o.RecordStep(OnboardingAction.Cancelled, OnboardingStatus.Cancelled, MyId, scope.UserName, reason, now);
        app.RecordStep(ApplicationAction.OnboardingCancelled, app.Status, MyId, scope.UserName, reason, now);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
            foreach (var hod in await HodsAsync(o, ct))
                await mailer.ToUserAsync(hod, $"Onboarding {o.Number} cancelled",
                    $"The onboarding of <b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}) was cancelled: {WorkflowMailer.Enc(reason)}",
                    links.Onboarding(o.Id), "Open the onboarding", ct);
        return saved;
    }

    // ===================================================================== Lists and tasks

    public async Task<IReadOnlyList<OnboardingListItemDto>> SearchAsync(OnboardingFilter filter, CancellationToken ct = default)
    {
        var visibility = await scope.VisibilityAsync(ct);
        var hodDepts = (await scope.HodDepartmentIdsAsync(ct)).ToList();
        var myRecords = (await tasks.ListAsync(t => t.AssignedToUserId == visibility.UserId, ct)).Select(t => t.OnboardingRecordId).Distinct().ToList();
        var seeAll = visibility.SeeAll || MayView;
        IEnumerable<OnboardingRecord> q = await onboardings.ListAsync(o =>
            seeAll || hodDepts.Contains(o.DepartmentId) || myRecords.Contains(o.Id) || o.ContactPersonUserId == visibility.UserId, ct);

        if (filter.Tab == "progress") q = q.Where(o => OnboardingWorkflow.IsActive(o.Status));
        if (filter.Status is { } s) q = q.Where(o => o.Status == s);
        if (filter.DivisionId is { } div) q = q.Where(o => o.DivisionId == div);
        if (filter.DepartmentId is { } dep) q = q.Where(o => o.DepartmentId == dep);
        if (filter.From is { } from) q = q.Where(o => o.StartDate >= from);
        if (filter.To is { } to) q = q.Where(o => o.StartDate <= to);

        var list = q.ToList();
        var ids = list.Select(o => o.Id).ToList();
        var counts = (await tasks.ListAsync(t => ids.Contains(t.OnboardingRecordId), ct)).GroupBy(t => t.OnboardingRecordId)
            .ToDictionary(g => g.Key, g => (Done: g.Count(t => t.Status != OnboardingTaskStatus.Open), Total: g.Count()));
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        var divNames = (await divisions.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);

        return list.OrderBy(o => o.Status).ThenBy(o => o.StartDate)
            .Select(o => new OnboardingListItemDto(o.Id, o.Number, o.JobApplicationId, o.EmployeeName, o.JobTitle, divNames.GetValueOrDefault(o.DivisionId, ""),
                deptNames.GetValueOrDefault(o.DepartmentId, ""), o.StartDate, o.Status, o.LetterIssued, o.LetterAcknowledged,
                counts.GetValueOrDefault(o.Id).Done, counts.GetValueOrDefault(o.Id).Total))
            .ToList();
    }

    /// <summary>Open checklist items assigned to me, on onboardings in progress (oldest due first).</summary>
    public async Task<IReadOnlyList<MyOnboardingTaskDto>> MyTasksAsync(CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return [];
        var mine = await tasks.ListAsync(t => t.AssignedToUserId == uid && t.Status == OnboardingTaskStatus.Open, ct);
        if (mine.Count == 0) return [];
        var ids = mine.Select(t => t.OnboardingRecordId).Distinct().ToList();
        var records = (await onboardings.ListAsync(o => ids.Contains(o.Id) && o.Status != OnboardingStatus.Completed && o.Status != OnboardingStatus.Cancelled, ct)).ToDictionary(o => o.Id);
        return mine.Where(t => records.ContainsKey(t.OnboardingRecordId))
            .Select(t => (Task: t, Record: records[t.OnboardingRecordId]))
            .OrderBy(x => x.Task.DueDate ?? x.Record.StartDate).ThenBy(x => x.Task.SortOrder)
            .Select(x => new MyOnboardingTaskDto(x.Task.Id, x.Record.Id, x.Record.Number, x.Record.EmployeeName, x.Record.JobTitle, x.Task.Team,
                x.Task.Title, x.Task.DueDate, x.Record.StartDate, x.Task.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Usability U1: the onboarding work waiting for me, by permission: appointment letters to issue (onboarding.manage),
    /// documents waiting for verification (onboarding.verify-documents), employees ready to activate and inductions ready to close
    /// (onboarding.complete). Never my own onboarding. Oldest first.
    /// </summary>
    public async Task<IReadOnlyList<OnboardingWorkItemDto>> MyWorkAsync(CancellationToken ct = default)
    {
        if (me.UserId is not { } uid || !(MayManage || MayVerify || MayComplete)) return [];
        var active = await onboardings.ListAsync(o => o.Status != OnboardingStatus.Completed && o.Status != OnboardingStatus.Cancelled && o.EmployeeUserId != uid, ct);
        var work = new List<OnboardingWorkItemDto>();
        OnboardingWorkItemDto Item(OnboardingRecord o, OnboardingWorkKind kind, int count, DateTime since) =>
            new(o.Id, o.Number, o.EmployeeName, o.JobTitle, kind, count, since);

        if (MayManage)
            work.AddRange(active.Where(o => o.Status == OnboardingStatus.Draft && !o.LetterIssued).Select(o => Item(o, OnboardingWorkKind.IssueLetter, 1, o.CreatedAt)));
        if (MayVerify)
        {
            var ids = active.Where(o => OnboardingWorkflow.BeforeActivation(o.Status)).Select(o => o.Id).ToList();
            var waiting = (await documents.ListAsync(d => ids.Contains(d.OnboardingRecordId) && d.IsApplicable &&
                    (d.Status == EmployeeDocumentStatus.Submitted || d.Status == EmployeeDocumentStatus.Resubmitted || d.Status == EmployeeDocumentStatus.UnderReview), ct))
                .GroupBy(d => d.OnboardingRecordId).ToDictionary(g => g.Key, g => (Count: g.Count(), Since: g.Min(d => d.SubmittedAt ?? d.CreatedAt)));
            work.AddRange(active.Where(o => waiting.ContainsKey(o.Id)).Select(o => Item(o, OnboardingWorkKind.VerifyDocuments, waiting[o.Id].Count, waiting[o.Id].Since)));
        }
        if (MayComplete)
        {
            work.AddRange(active.Where(o => o.Status == OnboardingStatus.AwaitingActivation).Select(o => Item(o, OnboardingWorkKind.Activate, 1, o.StatusChangedAt)));
            var inducting = active.Where(o => o.Status == OnboardingStatus.AwaitingDepartmentalInduction).ToList();
            var inductingIds = inducting.Select(o => o.Id).ToList();
            var byRecord = (await tasks.ListAsync(t => inductingIds.Contains(t.OnboardingRecordId), ct)).ToLookup(t => t.OnboardingRecordId);
            work.AddRange(inducting.Where(o => InductionProblem(o, byRecord[o.Id].ToList()) is null)
                .Select(o => Item(o, OnboardingWorkKind.CloseInduction, 1, o.StatusChangedAt)));
        }
        return work.OrderBy(w => w.Since).ToList();
    }

    /// <summary>The sidebar badge: open tasks assigned to me on onboardings in progress (two small queries, no DTOs; audit P6).</summary>
    public async Task<int> CountMyTasksAsync(CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return 0;
        var mine = await tasks.ListAsync(t => t.AssignedToUserId == uid && t.Status == OnboardingTaskStatus.Open, ct);
        if (mine.Count == 0) return 0;
        var ids = mine.Select(t => t.OnboardingRecordId).Distinct().ToList();
        var active = (await onboardings.ListAsync(o => ids.Contains(o.Id) && o.Status != OnboardingStatus.Completed && o.Status != OnboardingStatus.Cancelled, ct))
            .Select(o => o.Id).ToHashSet();
        return mine.Count(t => active.Contains(t.OnboardingRecordId));
    }

    // ===================================================================== Helpers

    private async Task<(JobApplication? Application, Offer? Offer, string? Error)> LoadStartableAsync(int applicationId, CancellationToken ct)
    {
        if (!MayManage) return (null, null, "Only a Recruitment Officer can start onboarding.");
        var app = await applications.GetWithHistoryAsync(applicationId, ct);
        if (app is null) return (null, null, "Application not found.");
        if (app.ApplicantUserId == MyId) return (null, null, "This is your own application.");
        if (await onboardings.AnyAsync(o => o.JobApplicationId == app.Id, ct)) return (null, null, "Onboarding has already been started for this new employee.");
        var offer = (await offers.ListAsync(o => o.JobApplicationId == app.Id && o.Status == OfferStatus.Accepted, ct)).FirstOrDefault();
        if (offer is null || app.Status != ApplicationStatus.Hired) return (null, null, "Onboarding starts once the candidate has accepted the offer.");
        return (app, offer, null);
    }

    private async Task<(OnboardingRecord? Record, string? Error)> LoadForHrAsync(int id, CancellationToken ct)
    {
        if (!MayManage) return (null, "Only a Recruitment Officer can do this.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null) return (null, "Onboarding not found.");
        if (o.EmployeeUserId == MyId) return (null, "This is your own appointment.");
        return !OnboardingWorkflow.IsActive(o.Status) ? (null, "This onboarding is no longer in progress.") : (o, null);
    }

    private static string? FirstDayProblem(DateOnly? start, string? location, string? notes)
    {
        if (start is not { } s || s < Today.AddDays(-30) || s > Today.AddDays(365))
            return "Choose a start date between 30 days ago and a year from now.";
        if (location is null) return "Say where the new employee should report.";
        if (location.Length > 300) return "Keep the place under 300 characters.";
        if (notes?.Length > 4000) return "The first-day notes must be 4,000 characters or fewer.";
        return null;
    }

    private async Task<string?> ContactProblemAsync(string? contactUserId, string employeeUserId, CancellationToken ct)
    {
        contactUserId = Clean(contactUserId);
        if (contactUserId is null) return null;
        return contactUserId == employeeUserId || !await users.IsActiveStaffAsync(contactUserId, ct)
            ? "The first-day contact must be an active GREL staff member." : null;
    }

    /// <summary>Why the departmental induction can't be closed yet, or null.</summary>
    private static string? InductionProblem(OnboardingRecord o, IReadOnlyCollection<OnboardingTask> list)
    {
        if (o.Status != OnboardingStatus.AwaitingDepartmentalInduction) return "The onboarding is not at the departmental induction stage.";
        if (o.StartDate > Today) return $"The induction can be closed from the start date ({o.StartDate:dd MMM yyyy}).";
        var open = list.Count(t => t.Status == OnboardingTaskStatus.Open);
        if (open > 0) return $"{open} checklist item(s) are still to do. Mark each one done or not needed.";
        // Phase 20: every task someone outside HR marked done must be verified by HR.
        var unverified = list.Count(t => t.VerifiedAt is null);
        return unverified > 0 ? $"{unverified} task(s) marked done still need HR to verify them." : null;
    }

    /// <summary>Phase 20: the induction areas the HR Manager can tick on approval, with their owners and tasks.</summary>
    private async Task<IReadOnlyList<InductionAreaChoiceDto>> InductionChoicesAsync(OnboardingRecord o, IReadOnlyCollection<OnboardingTask> list, CancellationToken ct)
    {
        var rules = await induction.RulesAsync(ct);
        var hrName = (await users.GetDisplayNamesAsync([await HrFallbackOwnerAsync(o, ct)], ct)).Values.FirstOrDefault() ?? "HR";
        var owners = await users.GetDisplayNamesAsync(rules.Values.Select(r => r.OwnerUserId).OfType<string>().Distinct(), ct);
        return rules.Values.OrderBy(r => r.Area)
            .Select(r => new InductionAreaChoiceDto(r.Area, r.IsDefault || list.Any(t => t.Area == r.Area),
                r.OwnerUserId is { } ow ? owners.GetValueOrDefault(ow) ?? "?" : $"{hrName} (HR)", r.Tasks))
            .ToList();
    }

    private bool CanUpdateTask(OnboardingTask t, OnboardingRecord o, bool isHod)
    {
        if (o.EmployeeUserId == me.UserId) return false;
        return MayManage || t.AssignedToUserId == me.UserId || isHod && t.Team == OnboardingTeam.Department;
    }

    /// <summary>
    /// Default owner per team: HR = the Human Resource area's owner, else the vacancy's recruiter; the department's HOD; IT = the
    /// Corporate IT area's owner, else the Head of the IT department (Phase 20: from the Induction areas settings).
    /// </summary>
    private async Task<Dictionary<OnboardingTeam, string?>> DefaultOwnersAsync(string? recruiterUserId, JobApplication app, CancellationToken ct)
    {
        var dept = await departments.GetByIdAsync(app.DepartmentId, ct);
        var rules = await induction.RulesAsync(ct);
        string? NotTheEmployee(string? uid) => uid == app.ApplicantUserId ? null : uid;
        return new Dictionary<OnboardingTeam, string?>
        {
            [OnboardingTeam.HumanResources] = NotTheEmployee(rules[InductionArea.HumanResource].OwnerUserId) ?? NotTheEmployee(recruiterUserId ?? MyId) ?? MyId,
            [OnboardingTeam.Department] = NotTheEmployee(dept?.HeadOfDepartmentUserId ?? app.RequestingHodUserId),
            [OnboardingTeam.InformationTechnology] = NotTheEmployee(rules[InductionArea.CorporateIt].OwnerUserId),
            [OnboardingTeam.NewEmployee] = null
        };
    }

    /// <summary>HR's fallback owner for an area without one: the vacancy's recruiter, else whoever started the onboarding.</summary>
    private async Task<string> HrFallbackOwnerAsync(OnboardingRecord o, CancellationToken ct)
    {
        var recruiter = (await vacancies.GetByIdAsync(o.VacancyId, ct))?.RecruiterUserId;
        return recruiter is not null && recruiter != o.EmployeeUserId ? recruiter : o.CreatedByUserId;
    }

    private async Task<IReadOnlyList<string>> HodsAsync(OnboardingRecord o, CancellationToken ct) =>
        new[] { o.RequestingHodUserId, (await departments.GetByIdAsync(o.DepartmentId, ct))?.HeadOfDepartmentUserId }
            .OfType<string>().Distinct().Where(id => id != o.EmployeeUserId && id != me.UserId).ToList();

    private async Task<bool> IsHodOfAsync(int departmentId, CancellationToken ct) =>
        me.IsInRole(Roles.HeadOfDepartment) && (await scope.HodDepartmentIdsAsync(ct)).Contains(departmentId);



    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);
}
