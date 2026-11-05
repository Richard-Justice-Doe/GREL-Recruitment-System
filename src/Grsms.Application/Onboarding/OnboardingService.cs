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

namespace Grsms.Application.Onboarding;

/// <summary>
/// Appointment letters and onboarding (Module 9). See <see cref="OnboardingWorkflow"/>.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Start onboarding, change first-day details and the checklist, complete, cancel : Recruitment Officer or HR Manager.
///   * Issue or revise the appointment letter                                          : HR Manager.
///   * Tick off a checklist item                     : its owner, the department's HOD (department items), or HR (any item).
///   * Read and sign the appointment letter, see the first-day information             : the new employee, in their portal.
///   * See an onboarding  : the roles that see every department, the department's HOD, and anyone with a task on it.
/// Nobody manages the onboarding of their own application.
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
    IAppLinks links)
{
    private const string ConflictMessage = "Someone else updated this record at the same moment. Please reload the page and try again.";
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool IsHr => OnboardingWorkflow.HrRoles.Any(me.IsInRole);

    // ===================================================================== Starting

    /// <summary>Accepted offers with no onboarding yet (HR only; empty for everyone else).</summary>
    public async Task<IReadOnlyList<OnboardingToStartDto>> ToStartAsync(OnboardingFilter? filter = null, CancellationToken ct = default)
    {
        if (!IsHr) return [];
        var started = (await onboardings.ListAsync(ct: ct)).Select(o => o.JobApplicationId).ToHashSet();
        IEnumerable<Offer> q = (await offers.ListAsync(o => o.Status == OfferStatus.Accepted, ct)).Where(o => !started.Contains(o.JobApplicationId));
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
    }

    /// <summary>HR starts onboarding: the record and the default checklist (with owners) are created, and each owner is emailed.</summary>
    public async Task<Result<int>> StartAsync(StartOnboardingForm form, CancellationToken ct = default)
    {
        var (app, offer, error) = await LoadStartableAsync(form.ApplicationId, ct);
        if (app is null) return Result.Failure<int>(error!);
        var location = Clean(form.ReportLocation);
        var notes = Clean(form.FirstDayNotes);
        if (FirstDayProblem(form.StartDate, location, notes) is { } problem) return Result.Failure<int>(problem);
        if (await ContactProblemAsync(form.ContactPersonUserId, app.ApplicantUserId, ct) is { } contactProblem) return Result.Failure<int>(contactProblem);

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
            StatusChangedAt = now,
            CreatedByUserId = MyId
        };
        onboardings.Add(record);
        app.RecordStep(ApplicationAction.OnboardingStarted, app.Status, MyId, scope.UserName, $"{record.Number}, starting {start:dd MMM yyyy}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

        // The checklist, with a default owner per team.
        var owners = await DefaultOwnersAsync(v.RecruiterUserId, app, ct);
        var order = 0;
        var created = new List<OnboardingTask>();
        foreach (var d in OnboardingWorkflow.DefaultChecklist)
        {
            var t = new OnboardingTask
            {
                OnboardingRecordId = record.Id, Team = d.Team, Title = d.Title, DueDate = start.AddDays(d.DueDays),
                AssignedToUserId = owners.GetValueOrDefault(d.Team), SortOrder = ++order
            };
            tasks.Add(t);
            created.Add(t);
        }
        saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);

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
        if (!me.HasPermission(Permissions.SeeAllDepartments) && !isHod && !list.Any(t => t.AssignedToUserId == me.UserId) && o.ContactPersonUserId != me.UserId)
            return null;

        var app = (await applications.GetByIdAsync(o.JobApplicationId, ct))!;
        var v = (await vacancies.GetByIdAsync(o.VacancyId, ct))!;
        var offer = await offers.GetByIdAsync(o.OfferId, ct);
        var names = await users.GetDisplayNamesAsync(list.SelectMany(t => new[] { t.AssignedToUserId, t.CompletedByUserId })
            .Concat([o.ContactPersonUserId, o.LetterIssuedByUserId, o.CreatedByUserId]).OfType<string>().Distinct(), ct);
        string? Name(string? uid) => uid is null ? null : names.GetValueOrDefault(uid);

        var own = o.EmployeeUserId == me.UserId;
        var manage = IsHr && !own && o.Status == OnboardingStatus.InProgress;
        var completeBlocked = CompleteProblem(o, list);
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
                    t.CompletedAt, Name(t.CompletedByUserId), o.Status == OnboardingStatus.InProgress && CanUpdateTask(t, o, isHod),
                    t.Status == OnboardingTaskStatus.Open && t.DueDate < Today))
                .ToList(),
            CanManage = manage,
            CanEditStartDate = manage && !o.LetterAcknowledged,
            CanIssueLetter = me.IsInRole(Roles.HrManager) && !own && o.Status == OnboardingStatus.InProgress && !o.LetterAcknowledged,
            CanComplete = manage && completeBlocked is null,
            CompleteBlockedReason = manage ? completeBlocked : null
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

    /// <summary>The HR Manager issues the appointment letter (or revises it before it is signed). The new employee is emailed.</summary>
    public async Task<Result> IssueLetterAsync(int id, string? terms, CancellationToken ct = default)
    {
        if (!me.IsInRole(Roles.HrManager)) return Result.Failure("Only an HR Manager can issue the appointment letter.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null) return Result.Failure("Onboarding not found.");
        if (o.EmployeeUserId == MyId) return Result.Failure("This is your own appointment.");
        if (o.Status != OnboardingStatus.InProgress) return Result.Failure("This onboarding is no longer in progress.");
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
        app.RecordStep(ApplicationAction.LetterIssued, app.Status, MyId, scope.UserName, $"{o.LetterNumber}{(revised ? " (revised)" : "")}", now);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        await mailer.ToUserAsync(o.EmployeeUserId, revised ? $"Your revised appointment letter: {o.JobTitle}" : $"Your appointment letter: {o.JobTitle}",
            $"Your {(revised ? "revised " : "")}appointment letter for the position of <b>{WorkflowMailer.Enc(o.JobTitle)}</b> is ready. " +
            "Please read it and sign it in your portal. It also tells you when and where to report on your first day " +
            $"(<b>{o.StartDate.ToString("dddd d MMMM yyyy", Gb)}</b>) and what to bring.",
            links.MyApplication(o.JobApplicationId), "Read my appointment letter", ct);
        if (!revised)
            await mailer.TextAsync(app.Phone, $"GREL: your appointment letter for {o.JobTitle} is ready. Please sign in to GRSMS to read and sign it.", ct);
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
        if (o.Status != OnboardingStatus.InProgress || !o.LetterIssued) return Result.Failure("There is no appointment letter waiting for your signature.");
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
        if (o.Status != OnboardingStatus.InProgress) return Result.Failure("This onboarding is no longer in progress.");
        if (!CanUpdateTask(t, o, await IsHodOfAsync(o.DepartmentId, ct)))
            return Result.Failure("Only the person responsible, the Head of Department (department tasks) or HR can update this task.");
        notes = Clean(notes);
        if (status == OnboardingTaskStatus.NotNeeded && notes is null) return Result.Failure("Say why this task is not needed.");
        if (notes?.Length > 2000) return Result.Failure("Notes must be 2,000 characters or fewer.");

        t.Status = status;
        t.Notes = notes ?? (status == OnboardingTaskStatus.Open ? t.Notes : null);
        (t.CompletedAt, t.CompletedByUserId) = status == OnboardingTaskStatus.Open ? ((DateTime?)null, (string?)null) : (DateTime.UtcNow, MyId);
        // The record's Version is deliberately not changed, so two people ticking different items at once both succeed.
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

    public async Task<Result> AddTaskAsync(int id, OnboardingTeam team, string? title, DateOnly? dueDate, string? userId, CancellationToken ct = default)
    {
        var (o, error) = await LoadForHrAsync(id, ct);
        if (o is null) return Result.Failure(error!);
        title = Clean(title);
        if (title is null) return Result.Failure("Describe the task.");
        if (title.Length > 300) return Result.Failure("Keep the task under 300 characters.");
        var order = (await tasks.ListAsync(t => t.OnboardingRecordId == o.Id, ct)).Select(t => t.SortOrder).DefaultIfEmpty(0).Max() + 1;
        var t = new OnboardingTask { OnboardingRecordId = o.Id, Team = team, Title = title, SortOrder = order };
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

    // ===================================================================== Complete / cancel

    public async Task<Result> CompleteAsync(int id, CancellationToken ct = default)
    {
        var (o, error) = await LoadForHrAsync(id, ct);
        if (o is null) return Result.Failure(error!);
        if (CompleteProblem(o, await tasks.ListAsync(t => t.OnboardingRecordId == o.Id, ct)) is { } problem) return Result.Failure(problem);

        var app = (await applications.GetWithHistoryAsync(o.JobApplicationId, ct))!;
        var now = DateTime.UtcNow;
        o.Status = OnboardingStatus.Completed;
        o.StatusChangedAt = now;
        o.Version = Guid.NewGuid();
        app.RecordStep(ApplicationAction.OnboardingCompleted, app.Status, MyId, scope.UserName, o.Number, now);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
            foreach (var hod in await HodsAsync(o, ct))
                await mailer.ToUserAsync(hod, $"Onboarding {o.Number} completed",
                    $"The onboarding of <b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}) is complete.",
                    links.Onboarding(o.Id), "Open the onboarding", ct);
        return saved;
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
        o.Status = OnboardingStatus.Cancelled;
        o.StatusChangedAt = now;
        o.CancelReason = reason;
        o.Version = Guid.NewGuid();
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
        IEnumerable<OnboardingRecord> q = await onboardings.ListAsync(o =>
            visibility.SeeAll || hodDepts.Contains(o.DepartmentId) || myRecords.Contains(o.Id) || o.ContactPersonUserId == visibility.UserId, ct);

        if (filter.Tab == "progress") q = q.Where(o => o.Status == OnboardingStatus.InProgress);
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
        var records = (await onboardings.ListAsync(o => ids.Contains(o.Id) && o.Status == OnboardingStatus.InProgress, ct)).ToDictionary(o => o.Id);
        return mine.Where(t => records.ContainsKey(t.OnboardingRecordId))
            .Select(t => (Task: t, Record: records[t.OnboardingRecordId]))
            .OrderBy(x => x.Task.DueDate ?? x.Record.StartDate).ThenBy(x => x.Task.SortOrder)
            .Select(x => new MyOnboardingTaskDto(x.Task.Id, x.Record.Id, x.Record.Number, x.Record.EmployeeName, x.Record.JobTitle, x.Task.Team,
                x.Task.Title, x.Task.DueDate, x.Record.StartDate, x.Task.CreatedAt))
            .ToList();
    }

    public async Task<int> CountMyTasksAsync(CancellationToken ct = default) => (await MyTasksAsync(ct)).Count;

    // ===================================================================== Helpers

    private async Task<(JobApplication? Application, Offer? Offer, string? Error)> LoadStartableAsync(int applicationId, CancellationToken ct)
    {
        if (!IsHr) return (null, null, "Only a Recruitment Officer or HR Manager can start onboarding.");
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
        if (!IsHr) return (null, "Only a Recruitment Officer or HR Manager can do this.");
        var o = await onboardings.GetByIdAsync(id, ct);
        if (o is null) return (null, "Onboarding not found.");
        if (o.EmployeeUserId == MyId) return (null, "This is your own appointment.");
        return o.Status != OnboardingStatus.InProgress ? (null, "This onboarding is no longer in progress.") : (o, null);
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

    private static string? CompleteProblem(OnboardingRecord o, IReadOnlyCollection<OnboardingTask> list)
    {
        if (o.Status != OnboardingStatus.InProgress) return "This onboarding is no longer in progress.";
        if (!o.LetterAcknowledged) return o.LetterIssued ? "The new employee has not signed the appointment letter yet." : "Issue the appointment letter first.";
        if (o.StartDate > Today) return $"Onboarding can be completed from the start date ({o.StartDate:dd MMM yyyy}).";
        var open = list.Count(t => t.Status == OnboardingTaskStatus.Open);
        return open > 0 ? $"{open} checklist item(s) are still to do. Mark each one done or not needed." : null;
    }

    private bool CanUpdateTask(OnboardingTask t, OnboardingRecord o, bool isHod)
    {
        if (o.EmployeeUserId == me.UserId) return false;
        return IsHr || t.AssignedToUserId == me.UserId || isHod && t.Team == OnboardingTeam.Department;
    }

    /// <summary>Default owner per team: the vacancy's recruiter (HR), the department's HOD, and the Head of the IT department.</summary>
    private async Task<Dictionary<OnboardingTeam, string?>> DefaultOwnersAsync(string? recruiterUserId, JobApplication app, CancellationToken ct)
    {
        var dept = await departments.GetByIdAsync(app.DepartmentId, ct);
        var it = (await departments.ListAsync(d => d.Code == OnboardingWorkflow.ItDepartmentCode && d.IsActive, ct)).FirstOrDefault();
        string? NotTheEmployee(string? uid) => uid == app.ApplicantUserId ? null : uid;
        return new Dictionary<OnboardingTeam, string?>
        {
            [OnboardingTeam.HumanResources] = NotTheEmployee(recruiterUserId ?? MyId) ?? MyId,
            [OnboardingTeam.Department] = NotTheEmployee(dept?.HeadOfDepartmentUserId ?? app.RequestingHodUserId),
            [OnboardingTeam.InformationTechnology] = NotTheEmployee(it?.HeadOfDepartmentUserId),
            [OnboardingTeam.NewEmployee] = null
        };
    }

    private async Task<IReadOnlyList<string>> HodsAsync(OnboardingRecord o, CancellationToken ct) =>
        new[] { o.RequestingHodUserId, (await departments.GetByIdAsync(o.DepartmentId, ct))?.HeadOfDepartmentUserId }
            .OfType<string>().Distinct().Where(id => id != o.EmployeeUserId && id != me.UserId).ToList();

    private async Task<bool> IsHodOfAsync(int departmentId, CancellationToken ct) =>
        me.IsInRole(Roles.HeadOfDepartment) && (await scope.HodDepartmentIdsAsync(ct)).Contains(departmentId);

    /// <summary>Names match ignoring case and extra spaces.</summary>
    private static bool SameName(string a, string b) =>
        string.Equals(string.Join(' ', a.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            string.Join(' ', b.Split(' ', StringSplitOptions.RemoveEmptyEntries)), StringComparison.OrdinalIgnoreCase);

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
