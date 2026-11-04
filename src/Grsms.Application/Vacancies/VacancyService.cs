using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Application.Recruitment;
using Grsms.Application.Users;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Vacancies;

/// <summary>
/// Vacancy Management (Module 3).
///
/// WHO MAY DO WHAT (checked here, on the server):
///   * Create (from an APPROVED request), edit, submit : Recruitment Officer; editing only while Draft.
///   * Approve (= publish) or return                    : HR Manager, but never the person who submitted it.
///   * Close, reopen, archive                           : Recruitment Officer or HR Manager.
///   * See                                              : RO, HR Manager, MD, HR Administrator (all);
///                                                        a HOD (their departments' vacancies).
///   * Automatic closing when the closing date passes   : the system (see <see cref="CloseExpiredAsync"/>).
/// </summary>
public class VacancyService(
    IVacancyRepository vacancies,
    IRecruitmentRequestRepository requests,
    IRepository<Department> departments,
    IRepository<Division> divisions,
    IRepository<Unit> units,
    IRepository<Site> sites,
    IUserDirectory users,
    INumberGenerator numbers,
    IUnitOfWork unitOfWork,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links)
{
    private const string ConflictMessage = "Someone else updated this vacancy at the same moment. Please reload the page and try again.";

    /// <summary>Most skills/certifications allowed in one list, and the longest single entry.</summary>
    private const int MaxListItems = 30, MaxItemLength = 100;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private bool IsRecruiter => scope.IsInRole(Roles.RecruitmentOfficer);
    private bool IsManager => VacancyWorkflow.ManagerRoles.Any(scope.IsInRole);

    // ===================================================================== Lists and counts

    /// <summary>The Vacancies list for the chosen tab ("open", "pending" or "all").</summary>
    public async Task<PagedResult<VacancyListItemDto>> SearchAsync(VacancyFilter filter, CancellationToken ct = default)
    {
        IReadOnlyCollection<VacancyStatus>? statuses = filter.Tab switch
        {
            "open" => [VacancyStatus.Published],
            "pending" => [VacancyStatus.PendingApproval],
            _ => null // "all": every status (the Status filter may narrow it)
        };
        return await vacancies.SearchAsync(filter, await scope.VisibilityAsync(ct), statuses, ct);
    }

    /// <summary>Approved requests that still need a vacancy ("Awaiting a vacancy" tab).</summary>
    public async Task<IReadOnlyList<ApprovedRequestDto>> GetApprovedRequestsWithoutVacancyAsync(CancellationToken ct = default) =>
        await vacancies.GetApprovedRequestsWithoutVacancyAsync(await scope.VisibilityAsync(ct), ct);

    /// <summary>Numbers for the tabs and the home page.</summary>
    public async Task<(int Open, int Pending, int AwaitingVacancy)> CountsAsync(CancellationToken ct = default)
    {
        var open = (await SearchAsync(new VacancyFilter { Tab = "open", PageSize = 1 }, ct)).TotalCount;
        var pending = (await SearchAsync(new VacancyFilter { Tab = "pending", PageSize = 1 }, ct)).TotalCount;
        var awaiting = (await GetApprovedRequestsWithoutVacancyAsync(ct)).Count;
        return (open, pending, awaiting);
    }

    // ===================================================================== Details

    public async Task<VacancyDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var v = await vacancies.GetWithDetailsAsync(id, ct);
        if (v is null || !AccessScope.CanSee(await scope.VisibilityAsync(ct), v.DepartmentId, v.RecruiterUserId)) return null;

        var request = await requests.GetByIdAsync(v.RecruitmentRequestId, ct);
        var dept = await departments.GetByIdAsync(v.DepartmentId, ct);
        var unit = v.UnitId is int uid ? await units.GetByIdAsync(uid, ct) : null;
        var site = await sites.GetByIdAsync(v.SiteId, ct);
        var names = await users.GetDisplayNamesAsync(new[] { v.RecruiterUserId, v.ReportingManagerUserId, v.RequestingHodUserId }.OfType<string>(), ct);
        var division = await divisions.GetByIdAsync(v.DivisionId, ct);

        var history = v.Actions.OrderBy(a => a.ActionAt).ThenBy(a => a.Id).ToList();
        var submitter = LastSubmitter(v);
        var isApprover = scope.IsInRole(VacancyWorkflow.ApproverRole);
        var pending = VacancyWorkflow.CanDecide(v.Status);
        var lastAction = history.LastOrDefault();

        return new VacancyDetailsDto
        {
            Id = v.Id,
            VacancyNumber = v.VacancyNumber,
            Status = v.Status,
            StatusChangedAt = v.StatusChangedAt,
            RecruitmentRequestId = v.RecruitmentRequestId,
            RequestNumber = request?.RequestNumber ?? "?",
            JobTitle = v.JobTitle,
            DepartmentId = v.DepartmentId,
            DivisionName = division?.Name ?? "?",
            DepartmentName = dept?.Name ?? "?",
            RequestingHodName = names.GetValueOrDefault(v.RequestingHodUserId) ?? "?",
            UnitName = unit?.Name,
            SiteName = site?.Name ?? "?",
            ReportingManagerName = v.ReportingManagerUserId is null ? null : names.GetValueOrDefault(v.ReportingManagerUserId),
            EmploymentType = v.EmploymentType,
            NumberOfPositions = v.NumberOfPositions,
            VacancyType = v.VacancyType,
            JobDescription = v.JobDescription,
            MinimumEducation = v.MinimumEducation,
            FieldOfStudy = v.FieldOfStudy,
            QualificationNotes = v.QualificationNotes,
            MinYearsExperience = v.MinYearsExperience,
            ExperienceNotes = v.ExperienceNotes,
            Requirements = v.Requirements
                .OrderBy(r => r.Type).ThenByDescending(r => r.IsEssential).ThenBy(r => r.Id)
                .Select(r => new VacancyRequirementDto(r.Type, r.Name, r.IsEssential))
                .ToList(),
            SalaryMin = v.SalaryMin,
            SalaryMax = v.SalaryMax,
            ShowSalary = v.ShowSalary,
            ClosingDate = v.ClosingDate,
            PublishedAt = v.PublishedAt,
            ClosedAt = v.ClosedAt,
            RecruiterName = names.GetValueOrDefault(v.RecruiterUserId) ?? "?",
            History = history.Select(a => new VacancyHistoryDto(a.Action, a.FromStatus, a.ToStatus, a.ActorName, a.Comments, a.ActionAt)).ToList(),
            IsOpenForApplications = VacancyWorkflow.IsOpenForApplications(v, Today),
            DaysToClosing = v.ClosingDate.DayNumber - Today.DayNumber,
            ReturnComments = v.Status == VacancyStatus.Draft && lastAction?.Action == VacancyAction.Returned ? lastAction.Comments : null,
            CanEdit = VacancyWorkflow.IsEditable(v.Status) && IsRecruiter,
            CanSubmit = VacancyWorkflow.CanSubmit(v.Status) && IsRecruiter,
            CanDecide = pending && isApprover && submitter != scope.UserId,
            DecisionBlockedReason = pending && isApprover && submitter == scope.UserId
                ? "You submitted this vacancy, so another HR Manager must approve it."
                : null,
            CanClose = VacancyWorkflow.CanClose(v.Status) && IsManager,
            CanReopen = VacancyWorkflow.CanReopen(v.Status) && IsManager,
            CanArchive = VacancyWorkflow.CanArchive(v.Status) && IsManager
        };
    }

    // ===================================================================== Form

    /// <summary>A new vacancy form pre-filled from an approved request.</summary>
    public async Task<Result<VacancyFormDto>> GetNewFormAsync(int recruitmentRequestId, CancellationToken ct = default)
    {
        var (request, error) = await LoadRequestForNewVacancyAsync(recruitmentRequestId, ct);
        if (request is null) return Result.Failure<VacancyFormDto>(error!);

        var dept = await departments.GetByIdAsync(request.DepartmentId, ct);
        var form = new VacancyFormDto
        {
            RecruitmentRequestId = request.Id,
            JobTitle = request.PositionTitle,
            UnitId = request.UnitId,
            SiteId = request.SiteId,
            ReportingManagerUserId = dept?.HeadOfDepartmentUserId,
            ClosingDate = Today.AddDays(14) // a common two-week advertising window; change as needed
        };
        FillReadOnlyFields(form, request, dept);
        return Result.Success(form);
    }

    /// <summary>Loads a Draft vacancy into the edit form.</summary>
    public async Task<VacancyFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        if (!IsRecruiter) return null;
        var v = await vacancies.GetWithDetailsAsync(id, ct);
        if (v is null || !VacancyWorkflow.IsEditable(v.Status)) return null;

        var request = (await requests.GetByIdAsync(v.RecruitmentRequestId, ct))!;
        var form = new VacancyFormDto
        {
            Id = v.Id,
            RecruitmentRequestId = v.RecruitmentRequestId,
            JobTitle = v.JobTitle,
            UnitId = v.UnitId,
            SiteId = v.SiteId,
            ReportingManagerUserId = v.ReportingManagerUserId,
            VacancyType = v.VacancyType,
            JobDescription = v.JobDescription,
            MinimumEducation = v.MinimumEducation,
            FieldOfStudy = v.FieldOfStudy,
            QualificationNotes = v.QualificationNotes,
            MinYearsExperience = v.MinYearsExperience,
            ExperienceNotes = v.ExperienceNotes,
            EssentialSkills = Lines(v, RequirementType.Skill, essential: true),
            DesirableSkills = Lines(v, RequirementType.Skill, essential: false),
            EssentialCertifications = Lines(v, RequirementType.Certification, essential: true),
            DesirableCertifications = Lines(v, RequirementType.Certification, essential: false),
            SalaryMin = v.SalaryMin,
            SalaryMax = v.SalaryMax,
            ShowSalary = v.ShowSalary,
            ClosingDate = v.ClosingDate
        };
        FillReadOnlyFields(form, request, await departments.GetByIdAsync(v.DepartmentId, ct));
        return form;
    }

    /// <summary>Refills the read-only parts of a posted form (they are never trusted from the browser).</summary>
    public async Task RefreshReadOnlyFieldsAsync(VacancyFormDto form, CancellationToken ct = default)
    {
        var request = await requests.GetByIdAsync(form.RecruitmentRequestId, ct);
        if (request is not null) FillReadOnlyFields(form, request, await departments.GetByIdAsync(request.DepartmentId, ct));
    }

    public async Task<IReadOnlyList<LookupItem>> GetUnitsAsync(int departmentId, CancellationToken ct = default) =>
        (await units.ListAsync(u => u.DepartmentId == departmentId && u.IsActive, ct))
            .OrderBy(u => u.Name).Select(u => new LookupItem(u.Id.ToString(), u.Name)).ToList();

    public async Task<IReadOnlyList<LookupItem>> GetSitesAsync(CancellationToken ct = default) =>
        (await sites.ListAsync(s => s.IsActive, ct))
            .OrderBy(s => s.Name).Select(s => new LookupItem(s.Id.ToString(), s.Name)).ToList();

    /// <summary>Possible reporting managers: the department's active staff, with its HOD always included.</summary>
    public async Task<IReadOnlyList<LookupItem>> GetReportingManagersAsync(int departmentId, CancellationToken ct = default)
    {
        var list = (await users.GetActiveStaffAsync(departmentId, ct)).ToList();
        var hodId = (await departments.GetByIdAsync(departmentId, ct))?.HeadOfDepartmentUserId;
        if (hodId is not null && list.All(l => l.Value != hodId))
        {
            var name = (await users.GetDisplayNamesAsync([hodId], ct)).GetValueOrDefault(hodId);
            if (name is not null) list.Insert(0, new LookupItem(hodId, name + " (Head of Department)"));
        }
        return list;
    }

    // ===================================================================== Create / edit / submit (Recruitment Officer)

    public async Task<Result<int>> CreateAsync(VacancyFormDto form, CancellationToken ct = default)
    {
        var (request, error) = await LoadRequestForNewVacancyAsync(form.RecruitmentRequestId, ct);
        if (request is null) return Result.Failure<int>(error!);

        Normalize(form);
        var errors = await ValidateAsync(form, request.DepartmentId, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);

        var now = DateTime.UtcNow;
        var number = await numbers.NextAsync($"VAC-{now.Year}", ct); // before Add: it saves its own change
        var v = new Vacancy
        {
            VacancyNumber = $"VAC-{now.Year}-{number:0000}",
            RecruitmentRequestId = request.Id,
            DivisionId = request.DivisionId,
            DepartmentId = request.DepartmentId,
            RequestingHodUserId = request.RequestedByUserId,
            EmploymentType = request.EmploymentType,
            NumberOfPositions = request.NumberOfPositions,
            RecruiterUserId = request.AssignedRecruiterUserId ?? scope.UserId,
            Status = VacancyStatus.Draft,
            StatusChangedAt = now
        };
        Apply(form, v);
        v.RecordStep(VacancyAction.Created, VacancyStatus.Draft, scope.UserId, scope.UserName, $"Created from request {request.RequestNumber}", now);

        vacancies.Add(v);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(v.Id);
    }

    public async Task<Result> UpdateAsync(VacancyFormDto form, CancellationToken ct = default)
    {
        if (!IsRecruiter) return Result.Failure("Only a Recruitment Officer can edit vacancies.");
        var v = await vacancies.GetWithDetailsAsync(form.Id, ct);
        if (v is null) return Result.Failure("Vacancy not found.");
        if (!VacancyWorkflow.IsEditable(v.Status)) return Result.Failure("Only draft vacancies can be edited.");

        Normalize(form);
        var errors = await ValidateAsync(form, v.DepartmentId, ct);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        Apply(form, v);
        v.Version = Guid.NewGuid();
        return await SaveAsync(ct);
    }

    public async Task<Result> SubmitAsync(int id, CancellationToken ct = default)
    {
        if (!IsRecruiter) return Result.Failure("Only a Recruitment Officer can submit vacancies.");
        var v = await vacancies.GetWithDetailsAsync(id, ct);
        if (v is null) return Result.Failure("Vacancy not found.");
        if (!VacancyWorkflow.CanSubmit(v.Status)) return Result.Failure("This vacancy has already been submitted.");
        if (v.ClosingDate <= Today) return Result.Failure("The closing date must be in the future. Edit the vacancy first.");

        v.RecordStep(VacancyAction.Submitted, VacancyStatus.PendingApproval, scope.UserId, scope.UserName, null, DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
            await mailer.ToRoleAsync(VacancyWorkflow.ApproverRole, [], $"Vacancy {v.VacancyNumber} needs your approval",
                $"{WorkflowMailer.Enc(scope.UserName)} submitted the vacancy <b>{WorkflowMailer.Enc(v.JobTitle)}</b> for approval. Approving it publishes it.",
                links.Vacancy(v.Id), $"Open vacancy {v.VacancyNumber}", ct);
        return saved;
    }

    // ===================================================================== Approve / return (HR Manager)

    public async Task<Result> DecideAsync(int id, VacancyDecisionDto dto, CancellationToken ct = default)
    {
        var v = await vacancies.GetWithDetailsAsync(id, ct);
        if (v is null || !AccessScope.CanSee(await scope.VisibilityAsync(ct), v.DepartmentId, v.RecruiterUserId))
            return Result.Failure("Vacancy not found.");
        if (v.Status != dto.ExpectedStatus)
            return Result.Failure($"This vacancy has already moved on (it is now '{v.Status}'). Please reload the page.");
        if (!VacancyWorkflow.CanDecide(v.Status)) return Result.Failure("This vacancy is not waiting for approval.");
        if (!scope.IsInRole(VacancyWorkflow.ApproverRole)) return Result.Failure("Only an HR Manager can approve vacancies.");
        if (LastSubmitter(v) == scope.UserId) return Result.Failure("You submitted this vacancy, so another HR Manager must approve it.");

        var comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
        var now = DateTime.UtcNow;
        var submitter = LastSubmitter(v);

        if (dto.Decision == VacancyDecision.Approve)
        {
            if (v.ClosingDate < Today)
                return Result.Failure("The closing date has already passed. Return the vacancy so the Recruitment Officer can set a new date.");

            v.PublishedAt = now;
            v.RecordStep(VacancyAction.Published, VacancyStatus.Published, scope.UserId, scope.UserName, comments, now);
            var saved = await SaveAsync(ct);
            if (saved.Succeeded)
            {
                var html = $"The vacancy <b>{WorkflowMailer.Enc(v.JobTitle)}</b> has been approved and published. It closes on {v.ClosingDate:dd MMM yyyy}.";
                var link = links.Vacancy(v.Id);
                await mailer.ToUserAsync(v.RecruiterUserId, $"{v.VacancyNumber} published", html, link, $"Open vacancy {v.VacancyNumber}", ct);
                if (submitter != v.RecruiterUserId)
                    await mailer.ToUserAsync(submitter, $"{v.VacancyNumber} published", html, link, $"Open vacancy {v.VacancyNumber}", ct);
                await mailer.ToUserAsync((await departments.GetByIdAsync(v.DepartmentId, ct))?.HeadOfDepartmentUserId,
                    $"{v.VacancyNumber} published", html, link, $"Open vacancy {v.VacancyNumber}", ct);
            }
            return saved;
        }

        if (dto.Decision == VacancyDecision.Return)
        {
            if (comments is null || comments.Length < 5)
                return Result.Failure("Please explain what needs changing in the comments.");

            v.RecordStep(VacancyAction.Returned, VacancyStatus.Draft, scope.UserId, scope.UserName, comments, now);
            var saved = await SaveAsync(ct);
            if (saved.Succeeded)
                await mailer.ToUserAsync(submitter, $"{v.VacancyNumber} was returned for changes",
                    $"The vacancy <b>{WorkflowMailer.Enc(v.JobTitle)}</b> was returned.<p><i>Comments:</i> {WorkflowMailer.Enc(comments)}</p>",
                    links.Vacancy(v.Id), $"Open vacancy {v.VacancyNumber}", ct);
            return saved;
        }

        return Result.Failure("Choose Approve or Return for changes.");
    }

    // ===================================================================== Close / reopen / archive (RO or HR Manager)

    public async Task<Result> CloseAsync(int id, string? reason, CancellationToken ct = default)
    {
        var (v, error) = await LoadForManagerAsync(id, VacancyWorkflow.CanClose, "Only published vacancies can be closed.", ct);
        if (v is null) return Result.Failure(error!);

        var now = DateTime.UtcNow;
        v.ClosedAt = now;
        v.RecordStep(VacancyAction.Closed, VacancyStatus.Closed, scope.UserId, scope.UserName, Clean(reason), now);
        return await SaveAsync(ct);
    }

    /// <summary>Reopens a closed vacancy with a new closing date (e.g. too few applicants).</summary>
    public async Task<Result> ReopenAsync(int id, DateOnly? newClosingDate, CancellationToken ct = default)
    {
        var (v, error) = await LoadForManagerAsync(id, VacancyWorkflow.CanReopen, "Only closed vacancies can be reopened.", ct);
        if (v is null) return Result.Failure(error!);
        if (newClosingDate is not { } date || date <= Today) return Result.Failure("Choose a new closing date in the future.");

        v.ClosingDate = date;
        v.ClosedAt = null;
        v.RecordStep(VacancyAction.Reopened, VacancyStatus.Published, scope.UserId, scope.UserName,
            $"New closing date {date:dd MMM yyyy}", DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    public async Task<Result> ArchiveAsync(int id, string? reason, CancellationToken ct = default)
    {
        var (v, error) = await LoadForManagerAsync(id, VacancyWorkflow.CanArchive, "Only draft or closed vacancies can be archived.", ct);
        if (v is null) return Result.Failure(error!);

        v.RecordStep(VacancyAction.Archived, VacancyStatus.Archived, scope.UserId, scope.UserName, Clean(reason), DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    // ===================================================================== Automatic closing (system)

    /// <summary>
    /// Closes every published vacancy whose closing date is before <paramref name="today"/>.
    /// Run regularly by the background worker in the web app. Returns how many were closed.
    /// The closing day itself still counts: a vacancy closing on 30 June accepts applications all of 30 June.
    /// </summary>
    public async Task<int> CloseExpiredAsync(DateOnly today, CancellationToken ct = default)
    {
        var expired = await vacancies.GetExpiredPublishedAsync(today, ct);
        if (expired.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var v in expired)
        {
            v.ClosedAt = now;
            v.RecordStep(VacancyAction.AutoClosed, VacancyStatus.Closed, null, "System", null, now);
        }

        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return 0; } // someone changed one at the same moment; the next run will retry

        foreach (var v in expired)
            await mailer.ToUserAsync(v.RecruiterUserId, $"{v.VacancyNumber} has closed",
                $"The closing date for <b>{WorkflowMailer.Enc(v.JobTitle)}</b> has passed, so it no longer accepts applications.",
                links.Vacancy(v.Id), $"Open vacancy {v.VacancyNumber}", ct);
        return expired.Count;
    }

    // ===================================================================== Helpers

    /// <summary>Checks a new vacancy may be created from this request: Recruitment Officer, request visible and approved, no vacancy yet.</summary>
    private async Task<(RecruitmentRequest? Request, string? Error)> LoadRequestForNewVacancyAsync(int requestId, CancellationToken ct)
    {
        if (!IsRecruiter) return (null, "Only a Recruitment Officer can create vacancies.");

        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null || !AccessScope.CanSee(await scope.VisibilityAsync(ct), request.DepartmentId, request.RequestedByUserId))
            return (null, "Recruitment request not found.");
        if (request.Status != RecruitmentRequestStatus.Approved)
            return (null, "A vacancy can only be created from a fully approved recruitment request.");
        if (await vacancies.FindForRequestAsync(requestId, ct) is { } existing)
            return (null, $"Vacancy {existing.Number} already exists for this request.");
        return (request, null);
    }

    /// <summary>Loads a vacancy for a close/reopen/archive action by a Recruitment Officer or HR Manager.</summary>
    private async Task<(Vacancy? Vacancy, string? Error)> LoadForManagerAsync(int id, Func<VacancyStatus, bool> allowed, string notAllowed, CancellationToken ct)
    {
        if (!IsManager) return (null, "Only a Recruitment Officer or HR Manager can do this.");
        var v = await vacancies.GetWithDetailsAsync(id, ct);
        if (v is null) return (null, "Vacancy not found.");
        return allowed(v.Status) ? (v, null) : (null, notAllowed);
    }

    /// <summary>Who sent the vacancy for approval most recently (they may not approve it themselves).</summary>
    private static string? LastSubmitter(Vacancy v) =>
        v.Actions.Where(a => a.Action == VacancyAction.Submitted).OrderBy(a => a.ActionAt).ThenBy(a => a.Id).LastOrDefault()?.ActorUserId;

    private static void FillReadOnlyFields(VacancyFormDto form, RecruitmentRequest request, Department? dept)
    {
        form.RequestNumber = request.RequestNumber;
        form.DepartmentId = request.DepartmentId;
        form.DepartmentName = dept?.Name;
        form.NumberOfPositions = request.NumberOfPositions;
        form.EmploymentType = request.EmploymentType;
    }

    private static void Normalize(VacancyFormDto form)
    {
        form.JobTitle = form.JobTitle.Trim();
        form.JobDescription = form.JobDescription.Trim();
        form.FieldOfStudy = Clean(form.FieldOfStudy);
        form.QualificationNotes = Clean(form.QualificationNotes);
        form.ExperienceNotes = Clean(form.ExperienceNotes);
        form.ReportingManagerUserId = Clean(form.ReportingManagerUserId);
    }

    private async Task<List<string>> ValidateAsync(VacancyFormDto form, int departmentId, CancellationToken ct)
    {
        var errors = new List<string>();

        if (form.ClosingDate is not { } closing || closing <= Today)
            errors.Add("The closing date must be after today.");

        if (form.SalaryMin is { } min && form.SalaryMax is { } max && min > max)
            errors.Add("The lowest salary cannot be more than the highest salary.");
        if (form.ShowSalary && form.SalaryMin is null && form.SalaryMax is null)
            errors.Add("Enter a salary range, or untick 'Show the salary range on adverts'.");

        if (form.UnitId is int unitId && !await units.AnyAsync(u => u.Id == unitId && u.DepartmentId == departmentId && u.IsActive, ct))
            errors.Add("The selected unit is not an active unit of this department.");
        if (form.SiteId is not int siteId || !await sites.AnyAsync(s => s.Id == siteId && s.IsActive, ct))
            errors.Add("Choose an active site.");
        if (form.ReportingManagerUserId is { } managerId && !await users.IsActiveStaffAsync(managerId, ct))
            errors.Add("The reporting manager must be an active GREL staff member.");

        foreach (var (label, text) in new[]
                 {
                     ("essential skills", form.EssentialSkills), ("desirable skills", form.DesirableSkills),
                     ("essential certifications", form.EssentialCertifications), ("desirable certifications", form.DesirableCertifications)
                 })
        {
            var items = SplitLines(text);
            if (items.Count > MaxListItems) errors.Add($"List at most {MaxListItems} {label}.");
            if (items.Any(i => i.Length > MaxItemLength)) errors.Add($"Each of the {label} must be {MaxItemLength} characters or fewer.");
        }

        return errors;
    }

    private static void Apply(VacancyFormDto form, Vacancy v)
    {
        v.JobTitle = form.JobTitle;
        v.UnitId = form.UnitId;
        v.SiteId = form.SiteId!.Value;
        v.ReportingManagerUserId = form.ReportingManagerUserId;
        v.VacancyType = form.VacancyType!.Value;
        v.JobDescription = form.JobDescription;
        v.MinimumEducation = form.MinimumEducation!.Value;
        v.FieldOfStudy = form.FieldOfStudy;
        v.QualificationNotes = form.QualificationNotes;
        v.MinYearsExperience = form.MinYearsExperience;
        v.ExperienceNotes = form.ExperienceNotes;
        v.SalaryMin = form.SalaryMin;
        v.SalaryMax = form.SalaryMax;
        v.ShowSalary = form.ShowSalary;
        v.ClosingDate = form.ClosingDate!.Value;

        // Replace the requirement lists as a whole. If something is listed as both essential and
        // desirable, it counts as essential.
        v.Requirements.Clear();
        AddRequirements(v, RequirementType.Skill, form.EssentialSkills, form.DesirableSkills);
        AddRequirements(v, RequirementType.Certification, form.EssentialCertifications, form.DesirableCertifications);
    }

    private static void AddRequirements(Vacancy v, RequirementType type, string? essential, string? desirable)
    {
        var essentialItems = SplitLines(essential);
        foreach (var name in essentialItems)
            v.Requirements.Add(new VacancyRequirement { Type = type, Name = name, IsEssential = true });
        foreach (var name in SplitLines(desirable).Where(d => !essentialItems.Contains(d, StringComparer.OrdinalIgnoreCase)))
            v.Requirements.Add(new VacancyRequirement { Type = type, Name = name, IsEssential = false });
    }

    /// <summary>"Excel\n  excel \n\nFirst aid" → ["Excel", "First aid"]: trimmed, blanks and duplicates removed (bullet characters too).</summary>
    private static List<string> SplitLines(string? text) =>
        (text ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim().TrimStart('-', '*', '•').Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Lines(Vacancy v, RequirementType type, bool essential) =>
        string.Join("\n", v.Requirements.Where(r => r.Type == type && r.IsEssential == essential).OrderBy(r => r.Id).Select(r => r.Name));

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

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
