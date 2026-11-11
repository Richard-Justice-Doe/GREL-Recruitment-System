using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Domain.Documents;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;

namespace Grsms.Application.Recruitment;

/// <summary>
/// Recruitment Request Management (Module 2): the digital QF-81-11 Recruitment Request Form. Raising, editing,
/// submitting, the three approval stages (approve / reject / return for correction), cancelling, supporting documents
/// and notification emails.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Raise / edit / submit / cancel : anyone holding the "Raise recruitment requests" permission (by default every staff
///                                      member and every HOD), for their own department or one they head. Only the
///                                      person who raised it edits, submits or cancels it; editing only while Draft or
///                                      Returned.
///   * Stage 1, Department Head review: the Head of the request's department. If the HOD raised the request, submitting
///                                      it is their sign-off and the request goes straight to stage 2.
///   * Stages 2 and 3                 : the role in <see cref="RecruitmentWorkflow.ApproverRole"/> (HR Manager, then MD).
///   * Nobody decides on a request they raised.
///   * See a request                  : its requester; the HOD of its department; and everyone holding
///                                      "See all departments" (Recruitment Officers, HR Manager, MD, HR Administrator, ...).
///
/// Workflow participants are found automatically from the request's department (HOD) and the role holders
/// (HR Manager, MD), so nobody has to pick approvers by hand.
/// </summary>
public class RecruitmentRequestService(
    IRecruitmentRequestRepository requests,
    IRepository<Department> departments,
    IRepository<Division> divisions,
    IRepository<Unit> units,
    IRepository<Site> sites,
    IUserDirectory users,
    AttachmentService files,
    INumberGenerator numbers,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    WorkflowMailer mailer,
    IAppLinks links,
    AccessScope scope,
    IVacancyLookup vacancyLookup,
    IRichTextSanitizer sanitizer)
{
    private const string ConflictMessage = "Someone else updated this request at the same moment. Please reload the page and try again.";

    /// <summary>The supporting documents of form QF-81-11, in the order shown in the upload list.</summary>
    public static readonly IReadOnlyList<AttachmentCategory> DocumentCategories =
    [
        AttachmentCategory.JobDescription,
        AttachmentCategory.MainAccountabilities,
        AttachmentCategory.OrganisationalChart,
        AttachmentCategory.DepartmentJustification,
        AttachmentCategory.BudgetApproval
    ];

    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private string MyName => me.FullName ?? me.UserName ?? "Unknown";
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ===================================================================== Look-ups for the form

    /// <summary>
    /// Departments the current user may raise requests for: their own (home) department and the departments they head,
    /// if active, and only with the "Raise recruitment requests" permission.
    /// </summary>
    public async Task<IReadOnlyList<LookupItem>> GetCreatableDepartmentsAsync(CancellationToken ct = default)
    {
        if (!me.HasPermission(Permissions.RaiseRecruitmentRequests)) return [];

        var ids = await HodDepartmentIdsAsync(ct);
        if (await users.GetDepartmentIdAsync(MyId, ct) is int home) ids.Add(home);
        var list = await departments.ListAsync(d => ids.Contains(d.Id) && d.IsActive, ct);
        return list.OrderBy(d => d.Name).Select(d => new LookupItem(d.Id.ToString(), d.Name)).ToList();
    }

    /// <summary>Active units of a department (for the Unit drop-down, which refreshes when the department changes).</summary>
    public async Task<IReadOnlyList<LookupItem>> GetUnitsAsync(int departmentId, CancellationToken ct = default) =>
        (await units.ListAsync(u => u.DepartmentId == departmentId && u.IsActive, ct))
            .OrderBy(u => u.Name).Select(u => new LookupItem(u.Id.ToString(), u.Name)).ToList();

    /// <summary>Active sites.</summary>
    public async Task<IReadOnlyList<LookupItem>> GetSitesAsync(CancellationToken ct = default) =>
        (await sites.ListAsync(s => s.IsActive, ct))
            .OrderBy(s => s.Name).Select(s => new LookupItem(s.Id.ToString(), s.Name)).ToList();

    /// <summary>A blank form: request date today, one member of staff.</summary>
    public static RecruitmentRequestFormDto NewForm() => new() { RequestDate = Today };

    // ===================================================================== Reading

    /// <summary>The list page: requests the user may see, filtered and paged.</summary>
    public async Task<PagedResult<RecruitmentRequestListItemDto>> SearchAsync(RecruitmentRequestFilter filter, CancellationToken ct = default)
    {
        var visibility = await VisibilityAsync(ct);
        if (!filter.AwaitingMe)
            return await requests.SearchAsync(filter, visibility, null, ct);

        // "Awaiting my action" = sitting at a stage I decide, and not raised by me.
        var roleStages = RecruitmentWorkflow.DecisionStages
            .Where(s => s != RecruitmentRequestStatus.DepartmentHeadReview && me.IsInRole(RecruitmentWorkflow.ApproverRole(s)!))
            .ToList();
        var headIds = me.IsInRole(Roles.HeadOfDepartment) ? await HodDepartmentIdsAsync(ct) : [];
        if (roleStages.Count == 0 && headIds.Count == 0)
            return new PagedResult<RecruitmentRequestListItemDto>([], 1, filter.PageSize, 0);

        return await requests.SearchAsync(filter, visibility, new AwaitingDecision(roleStages, headIds, MyId), ct);
    }

    /// <summary>How many requests wait for the current user's decision (home page and list tab).</summary>
    public async Task<int> CountAwaitingMeAsync(CancellationToken ct = default) =>
        (await SearchAsync(new RecruitmentRequestFilter { AwaitingMe = true, PageSize = 1 }, ct)).TotalCount;

    /// <summary>The details page, or null if the request does not exist or the user may not see it.</summary>
    public async Task<RecruitmentRequestDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var r = await requests.GetWithHistoryAsync(id, ct);
        if (r is null || !CanView(r, await VisibilityAsync(ct))) return null;

        var dept = await departments.GetByIdAsync(r.DepartmentId, ct);
        var unit = r.UnitId is int uid ? await units.GetByIdAsync(uid, ct) : null;
        var site = await sites.GetByIdAsync(r.SiteId, ct);
        var names = await users.GetDisplayNamesAsync(
            new[] { r.RequestedByUserId, r.AssignedRecruiterUserId, r.BudgetValidatedByUserId, dept?.HeadOfDepartmentUserId }.OfType<string>(), ct);
        string? NameOf(string? userId) => userId is null ? null : names.GetValueOrDefault(userId);

        var attachments = await files.ListAsync(AttachmentOwners.RecruitmentRequest, r.Id, ct);
        var vacancy = await vacancyLookup.FindForRequestAsync(r.Id, ct);
        var isRequester = r.RequestedByUserId == MyId;
        var stageRole = RecruitmentWorkflow.ApproverRole(r.Status);
        var isMyStage = await IsMyStageAsync(r, ct);

        return new RecruitmentRequestDetailsDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            Status = r.Status,
            StatusChangedAt = r.StatusChangedAt,
            DepartmentId = r.DepartmentId,
            DivisionName = (await divisions.GetByIdAsync(r.DivisionId, ct))?.Name ?? "?",
            DepartmentName = dept?.Name ?? "?",
            UnitName = unit?.Name,
            SiteName = site?.Name ?? "?",
            PositionTitle = r.PositionTitle,
            NumberOfPositions = r.NumberOfPositions,
            EmploymentType = r.EmploymentType,
            StaffCategory = r.StaffCategory,
            VacancyReason = r.VacancyReason,
            PurposeOther = r.PurposeOther,
            ReplacedEmployee = r.ReplacedEmployee,
            Justification = r.Justification,
            RequestDate = r.RequestDate,
            ProposedStartDate = r.ProposedStartDate,
            PostDescriptionHtml = string.IsNullOrEmpty(r.PostDescription) ? null : sanitizer.Sanitize(r.PostDescription), // cleaned again: defence in depth
            EducationLevel = r.EducationLevel,
            QualificationCategory = r.QualificationCategory,
            TrainingLevel = r.TrainingLevel,
            CertificateRequired = r.CertificateRequired,
            MinYearsExperience = r.MinYearsExperience,
            ProfessionalExperience = r.ProfessionalExperience,
            DrivingLicence = r.DrivingLicence,
            DrivingLicenceClass = r.DrivingLicenceClass,
            Remarks = r.Remarks,
            BudgetCode = r.BudgetCode,
            EstimatedAnnualCost = r.EstimatedAnnualCost,
            BudgetValidated = r.BudgetValidated,
            BudgetValidatedByName = NameOf(r.BudgetValidatedByUserId),
            RequestedByUserId = r.RequestedByUserId,
            RequestedByName = NameOf(r.RequestedByUserId) ?? "?",
            HeadOfDepartmentName = NameOf(dept?.HeadOfDepartmentUserId),
            AssignedRecruiterName = NameOf(r.AssignedRecruiterUserId),
            CreatedAt = r.CreatedAt,
            DateRequested = r.DateRequested,
            ApprovedAt = r.ApprovedAt,
            History = r.Actions
                .OrderBy(a => a.ActionAt).ThenBy(a => a.Id)
                .Select(a => new RecruitmentRequestHistoryDto(a.Action, a.FromStatus, a.ToStatus, a.ActorName, a.Comments, a.ActionAt))
                .ToList(),
            Attachments = attachments,
            HasJobDescription = attachments.Any(a => a.Category == AttachmentCategory.JobDescription),
            CanEdit = isRequester && RecruitmentWorkflow.IsEditable(r.Status),
            CanSubmit = isRequester && RecruitmentWorkflow.IsEditable(r.Status),
            CanDecide = isMyStage && !isRequester,
            DecisionBlockedReason = isMyStage && isRequester
                ? $"You raised this request, so another {stageRole} must make this decision."
                : null,
            WaitingFor = r.Status == RecruitmentRequestStatus.DepartmentHeadReview
                ? $"{NameOf(dept?.HeadOfDepartmentUserId) ?? "the Head of Department"} (Head of {dept?.Name ?? "Department"})"
                : stageRole,
            CanCancel = isRequester && !RecruitmentWorkflow.IsFinal(r.Status),
            VacancyId = vacancy?.Id,
            VacancyNumber = vacancy?.Number,
            RequisitionNumber = vacancy?.RequisitionNumber,
            CanCreateVacancy = r.Status == RecruitmentRequestStatus.Approved && vacancy is null && me.IsInRole(Roles.RecruitmentOfficer)
        };
    }

    /// <summary>Loads a request into the edit form, or null if the user may not edit it.</summary>
    public async Task<RecruitmentRequestFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId || !RecruitmentWorkflow.IsEditable(r.Status)) return null;
        return ToForm(r);
    }

    // ===================================================================== Creating and editing (requester)

    /// <summary>Saves a new request as a Draft and gives it its request number.</summary>
    public async Task<Result<int>> CreateAsync(RecruitmentRequestFormDto form, CancellationToken ct = default)
    {
        if (!me.HasPermission(Permissions.RaiseRecruitmentRequests))
            return Result.Failure<int>("You do not have permission to raise recruitment requests.");

        Normalize(form);
        var errors = await ValidateFormAsync(form, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);

        // Numbering first (it saves its own change), then the request itself.
        var dept = (await departments.GetByIdAsync(form.DepartmentId!.Value, ct))!;
        var now = DateTime.UtcNow;
        var sequenceKey = $"RR-{dept.Code}-{now.Year}";
        var number = await numbers.NextAsync(sequenceKey, ct);

        var entity = new RecruitmentRequest
        {
            RequestNumber = $"{sequenceKey}-{number:0000}",
            RequestedByUserId = MyId,
            Status = RecruitmentRequestStatus.Draft,
            StatusChangedAt = now
        };
        Apply(form, entity);
        entity.DivisionId = dept.DivisionId;
        entity.RecordStep(WorkflowAction.Created, RecruitmentRequestStatus.Draft, MyId, MyName, null, now);

        requests.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    /// <summary>Saves changes to a Draft or Returned request.</summary>
    public async Task<Result> UpdateAsync(RecruitmentRequestFormDto form, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(form.Id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure("Request not found.");
        if (!RecruitmentWorkflow.IsEditable(r.Status)) return Result.Failure("This request can no longer be edited.");

        Normalize(form);
        var errors = await ValidateFormAsync(form, ct);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        Apply(form, r);
        r.DivisionId = (await departments.GetByIdAsync(r.DepartmentId, ct))!.DivisionId; // the department may have changed
        r.Version = Guid.NewGuid();
        return await SaveAsync(ct);
    }

    // ===================================================================== Workflow steps

    /// <summary>
    /// Sends a Draft (or Returned) request into the workflow, to stage 1 (Department Head review). When the requester heads
    /// the department, their submission is the stage-1 sign-off and the request goes on to the HR Manager.
    /// A job description must be attached first.
    /// </summary>
    public async Task<Result> SubmitAsync(int id, CancellationToken ct = default)
    {
        var r = await requests.GetWithHistoryAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure("Request not found.");
        if (!RecruitmentWorkflow.IsEditable(r.Status)) return Result.Failure("This request has already been submitted.");

        // Re-check the details: e.g. the department may have been deactivated since the draft was saved, or a draft
        // from before QF-81-11 may lack the newer fields.
        var form = ToForm(r);
        var errors = MissingFields(form);
        errors.AddRange(await ValidateFormAsync(form, ct));
        if (!await files.HasCategoryAsync(AttachmentOwners.RecruitmentRequest, r.Id, AttachmentCategory.JobDescription, ct))
            errors.Add("Attach the job description before submitting.");

        var dept = (await departments.GetByIdAsync(r.DepartmentId, ct))!;
        var requesterIsHead = (await HodDepartmentIdsAsync(ct)).Contains(r.DepartmentId);
        if (!requesterIsHead && dept.HeadOfDepartmentUserId is null)
            errors.Add($"No Head of Department is set for {dept.Name}, so nobody could review the request. Ask the HR Administrator to set one, then submit again.");
        if (errors.Count > 0) return Result.Failure([.. errors.Distinct()]);

        var now = DateTime.UtcNow;
        var action = r.Status == RecruitmentRequestStatus.Returned ? WorkflowAction.Resubmitted : WorkflowAction.Submitted;
        r.DateRequested ??= now;
        // Refresh the division copy: an HR Administrator may have moved the department since the draft was saved.
        r.DivisionId = dept.DivisionId;

        r.RecordStep(action, RecruitmentRequestStatus.DepartmentHeadReview, MyId, MyName, null, now);
        if (requesterIsHead)
            r.RecordStep(WorkflowAction.Approved, RecruitmentRequestStatus.HrApproval, MyId, MyName,
                "Department Head review: signed off by the Head of Department, who raised this request.", now);

        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        var verb = action == WorkflowAction.Resubmitted ? "resubmitted" : "submitted";
        if (requesterIsHead)
            await NotifyRoleAsync(Roles.HrManager, r,
                $"Recruitment request {r.RequestNumber} needs your review",
                $"{Enc(MyName)}, Head of {Enc(dept.Name)}, has {verb} a recruitment request for <b>{Enc(r.PositionTitle)}</b>. It is waiting for the HR Manager review.", ct);
        else
            await NotifyUserAsync(dept.HeadOfDepartmentUserId, r,
                $"Recruitment request {r.RequestNumber} needs your review",
                $"{Enc(MyName)} has {verb} a recruitment request for <b>{Enc(r.PositionTitle)}</b> in {Enc(dept.Name)}. As Head of Department, please approve it, return it for correction or reject it.", ct);
        return saved;
    }

    /// <summary>A reviewer's decision at the current approval stage: approve, return for correction, or reject.</summary>
    public async Task<Result> DecideAsync(int id, ReviewDecisionDto dto, CancellationToken ct = default)
    {
        var r = await requests.GetWithHistoryAsync(id, ct);
        if (r is null || !CanView(r, await VisibilityAsync(ct))) return Result.Failure("Request not found.");

        // Stale page: someone else already moved the request on.
        if (r.Status != dto.ExpectedStatus)
            return Result.Failure($"This request has already moved on (it is now '{EnumText.Label(r.Status)}'). Please reload the page.");

        var stageRole = RecruitmentWorkflow.ApproverRole(r.Status);
        if (stageRole is null) return Result.Failure("This request is not waiting for a decision.");
        if (!await IsMyStageAsync(r, ct))
            return Result.Failure(r.Status == RecruitmentRequestStatus.DepartmentHeadReview
                ? "Only the Head of this request's department can review it at this stage."
                : $"Only a {stageRole} can decide at this stage.");
        if (r.RequestedByUserId == MyId) return Result.Failure($"You raised this request, so another {stageRole} must decide.");

        var comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
        if (dto.Decision is ReviewDecision.Return or ReviewDecision.Reject && (comments is null || comments.Length < 5))
            return Result.Failure("Please explain your decision in the comments, so the requester knows what to do.");

        var now = DateTime.UtcNow;
        var stage = r.Status;

        switch (dto.Decision)
        {
            case ReviewDecision.Approve:
                // 11 Nov 2026 (docs/29, change 1): budget approval is not required for the HR Manager to approve; the budget
                // approval document is an optional upload. BudgetValidated is kept only for requests approved before then.
                var next = RecruitmentWorkflow.NextAfterApproval(stage);
                if (next == RecruitmentRequestStatus.Approved) r.ApprovedAt = now;
                r.RecordStep(WorkflowAction.Approved, next, MyId, MyName, comments, now);
                break;

            case ReviewDecision.Return:
                r.RecordStep(WorkflowAction.Returned, RecruitmentRequestStatus.Returned, MyId, MyName, comments, now);
                break;

            case ReviewDecision.Reject:
                r.RecordStep(WorkflowAction.Rejected, RecruitmentRequestStatus.Rejected, MyId, MyName, comments, now);
                break;

            default:
                return Result.Failure("Choose Approve, Return for correction, or Reject.");
        }

        var saved = await SaveAsync(ct);
        if (saved.Succeeded) await NotifyAfterDecisionAsync(r, stage, comments, ct);
        return saved;
    }

    /// <summary>The requester withdraws the request. Final.</summary>
    public async Task<Result> CancelAsync(int id, string? reason, CancellationToken ct = default)
    {
        var r = await requests.GetWithHistoryAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure("Request not found.");
        if (RecruitmentWorkflow.IsFinal(r.Status)) return Result.Failure("This request is already closed.");

        r.RecordStep(WorkflowAction.Cancelled, RecruitmentRequestStatus.Cancelled, MyId, MyName,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    // ===================================================================== Supporting documents

    /// <summary>
    /// Adds a supporting document to a Draft/Returned request (requester only). Only the QF-81-11 document types
    /// (<see cref="DocumentCategories"/>) and only PDF, DOCX and XLSX files are accepted.
    /// </summary>
    public async Task<Result<int>> AddAttachmentAsync(int id, AttachmentCategory category, string fileName, Stream content, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure<int>("Request not found.");
        if (!RecruitmentWorkflow.IsEditable(r.Status)) return Result.Failure<int>("Files can only be changed while the request is a draft or returned for correction.");
        if (!DocumentCategories.Contains(category)) return Result.Failure<int>("Choose the type of document.");

        return await files.AddAsync(AttachmentOwners.RecruitmentRequest, id, category, fileName, content, ct, AttachmentService.RequestDocumentTypes);
    }

    /// <summary>Removes a file from a Draft/Returned request (requester only).</summary>
    public async Task<Result> RemoveAttachmentAsync(int id, int attachmentId, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure("Request not found.");
        if (!RecruitmentWorkflow.IsEditable(r.Status)) return Result.Failure("Files can only be changed while the request is a draft or returned for correction.");

        return await files.RemoveAsync(AttachmentOwners.RecruitmentRequest, id, attachmentId, ct);
    }

    /// <summary>Opens a file for download, if the user may see the request.</summary>
    public async Task<FileDownload?> OpenAttachmentAsync(int id, int attachmentId, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(id, ct);
        if (r is null || !CanView(r, await VisibilityAsync(ct))) return null;
        return await files.OpenAsync(AttachmentOwners.RecruitmentRequest, id, attachmentId, ct);
    }

    // ===================================================================== Helpers: permissions
    // The department visibility rule is shared with other modules; see Application/Common/AccessScope.cs.

    private Task<HashSet<int>> HodDepartmentIdsAsync(CancellationToken ct) => scope.HodDepartmentIdsAsync(ct);

    private Task<RequestVisibility> VisibilityAsync(CancellationToken ct) => scope.VisibilityAsync(ct);

    private static bool CanView(RecruitmentRequest r, RequestVisibility v) =>
        AccessScope.CanSee(v, r.DepartmentId, r.RequestedByUserId);

    /// <summary>
    /// True when the current user decides at the request's current stage: the stage's role, and for the Department Head
    /// review also heading the request's department. (Whether they raised it themselves is checked separately.)
    /// </summary>
    private async Task<bool> IsMyStageAsync(RecruitmentRequest r, CancellationToken ct)
    {
        var role = RecruitmentWorkflow.ApproverRole(r.Status);
        if (role is null || !me.IsInRole(role)) return false;
        return r.Status != RecruitmentRequestStatus.DepartmentHeadReview || (await HodDepartmentIdsAsync(ct)).Contains(r.DepartmentId);
    }

    // ===================================================================== Helpers: validation and mapping

    private void Normalize(RecruitmentRequestFormDto form)
    {
        form.PositionTitle = form.PositionTitle.Trim();
        form.Justification = form.Justification.Trim();
        form.ReplacedEmployee = form.VacancyReason == VacancyReason.Replacement ? Clean(form.ReplacedEmployee) : null;
        form.PurposeOther = form.VacancyReason == VacancyReason.Other ? Clean(form.PurposeOther) : null;
        form.PostDescription = sanitizer.Sanitize(form.PostDescription);
        form.QualificationCategory = Clean(form.QualificationCategory);
        form.TrainingLevel = Clean(form.TrainingLevel);
        form.CertificateRequired = Clean(form.CertificateRequired);
        form.ProfessionalExperience = Clean(form.ProfessionalExperience);
        form.DrivingLicenceClass = form.DrivingLicence == DrivingLicenceRequirement.NotNeeded ? null : Clean(form.DrivingLicenceClass)?.ToUpperInvariant();
        form.Remarks = Clean(form.Remarks);
    }

    /// <summary>
    /// Rules the form attributes cannot check: valid department/unit/site for this user, the choices offered on QF-81-11,
    /// sensible dates and the length of the description.
    /// </summary>
    private async Task<List<string>> ValidateFormAsync(RecruitmentRequestFormDto form, CancellationToken ct)
    {
        var errors = new List<string>();

        var allowed = await GetCreatableDepartmentsAsync(ct);
        if (form.DepartmentId is not int deptId || allowed.All(d => d.Value != deptId.ToString()))
            errors.Add("You can only raise requests for your own department or a department you head, and it must be active.");
        else if (form.UnitId is int unitId &&
                 !await units.AnyAsync(u => u.Id == unitId && u.DepartmentId == deptId && u.IsActive, ct))
            errors.Add("The selected unit is not an active unit of this department.");

        if (form.SiteId is not int siteId || !await sites.AnyAsync(s => s.Id == siteId && s.IsActive, ct))
            errors.Add("Choose an active site.");

        if (form.EmploymentType is { } type && !RecruitmentWorkflow.FormEngagementTypes.Contains(type))
            errors.Add("Choose Permanent or Contract as the engagement type.");
        if (form.VacancyReason is { } purpose && !RecruitmentWorkflow.FormPurposes.Contains(purpose))
            errors.Add("Choose one of the purposes on the form.");
        if (form.VacancyReason == VacancyReason.Replacement && form.ReplacedEmployee is null)
            errors.Add("For a replacement, enter the name of the employee being replaced.");
        if (!Enum.IsDefined(form.DrivingLicence))
            errors.Add("Choose whether a driving licence is needed.");
        if (form.VacancyReason == VacancyReason.Other && form.PurposeOther is null)
            errors.Add("For 'Other', say what the purpose of the request is.");

        if (form.RequestDate is { } requestDate && requestDate > Today)
            errors.Add("The request date cannot be in the future.");
        if (form.ProposedStartDate is { } start)
        {
            if (start <= Today) errors.Add("The proposed starting date must be after today.");
            else if (form.RequestDate is { } rd && start < rd) errors.Add("The proposed starting date cannot be before the request date.");
        }

        if (!string.IsNullOrEmpty(form.PostDescription))
        {
            var chars = RichText.CharacterCount(form.PostDescription);
            if (chars < RecruitmentRequestFormDto.MinPostDescriptionChars)
                errors.Add($"Describe the post in at least {RecruitmentRequestFormDto.MinPostDescriptionChars} characters.");
            if (chars > RecruitmentRequestFormDto.MaxPostDescriptionChars)
                errors.Add($"The description of the post is {chars:N0} characters; the limit is {RecruitmentRequestFormDto.MaxPostDescriptionChars:N0}.");
        }


        return errors;
    }

    /// <summary>
    /// The required QF-81-11 fields that are empty. Saving the form checks these through its attributes; submitting checks
    /// them here as well, because drafts saved before the form was digitised do not have them.
    /// </summary>
    private static List<string> MissingFields(RecruitmentRequestFormDto form)
    {
        var missing = new List<string>();
        if (form.StaffCategory is null) missing.Add("Category");
        if (form.RequestDate is null) missing.Add("Request date");
        if (form.ProposedStartDate is null) missing.Add("Proposed starting date");
        if (string.IsNullOrEmpty(form.PostDescription)) missing.Add("Description of the post");
        if (form.EducationLevel is null) missing.Add("Educational level");
        return missing.Count == 0 ? [] : [$"Complete the form before submitting (click Edit): {string.Join(", ", missing)}."];
    }

    private static void Apply(RecruitmentRequestFormDto form, RecruitmentRequest r)
    {
        r.DepartmentId = form.DepartmentId!.Value;
        r.UnitId = form.UnitId;
        r.SiteId = form.SiteId!.Value;
        r.PositionTitle = form.PositionTitle;
        r.NumberOfPositions = form.NumberOfPositions;
        r.EmploymentType = form.EmploymentType!.Value;
        r.StaffCategory = form.StaffCategory;
        r.VacancyReason = form.VacancyReason!.Value;
        r.PurposeOther = form.PurposeOther;
        r.ReplacedEmployee = form.ReplacedEmployee;
        r.RequestDate = form.RequestDate;
        r.ProposedStartDate = form.ProposedStartDate;
        r.PostDescription = form.PostDescription;
        r.Justification = form.Justification;
        r.EducationLevel = form.EducationLevel;
        r.QualificationCategory = form.QualificationCategory;
        r.TrainingLevel = form.TrainingLevel;
        r.CertificateRequired = form.CertificateRequired;
        r.MinYearsExperience = form.MinYearsExperience;
        r.ProfessionalExperience = form.ProfessionalExperience;
        r.DrivingLicence = form.DrivingLicence;
        r.DrivingLicenceClass = form.DrivingLicenceClass;
        r.Remarks = form.Remarks;
    }

    private static RecruitmentRequestFormDto ToForm(RecruitmentRequest r) => new()
    {
        Id = r.Id, DepartmentId = r.DepartmentId, UnitId = r.UnitId, SiteId = r.SiteId,
        PositionTitle = r.PositionTitle, NumberOfPositions = r.NumberOfPositions,
        EmploymentType = r.EmploymentType, StaffCategory = r.StaffCategory,
        VacancyReason = r.VacancyReason, PurposeOther = r.PurposeOther, ReplacedEmployee = r.ReplacedEmployee,
        RequestDate = r.RequestDate, ProposedStartDate = r.ProposedStartDate,
        PostDescription = r.PostDescription, Justification = r.Justification,
        EducationLevel = r.EducationLevel, QualificationCategory = r.QualificationCategory, TrainingLevel = r.TrainingLevel,
        CertificateRequired = r.CertificateRequired, MinYearsExperience = r.MinYearsExperience,
        ProfessionalExperience = r.ProfessionalExperience, DrivingLicence = r.DrivingLicence,
        DrivingLicenceClass = r.DrivingLicenceClass, Remarks = r.Remarks
    };

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Saves, turning a simultaneous-edit clash into a friendly message.</summary>
    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);

    // ===================================================================== Helpers: notification emails

    /// <summary>After a decision: tell the next approvers, and keep the requester (and their HOD) informed.</summary>
    private async Task NotifyAfterDecisionAsync(RecruitmentRequest r, RecruitmentRequestStatus decidedStage, string? comments, CancellationToken ct)
    {
        var commentHtml = comments is null ? "" : $"<p><i>Comments:</i> {Enc(comments)}</p>";
        var stageName = RecruitmentWorkflow.StageName(decidedStage);

        // The department's HOD hears about later decisions on requests their staff raised (not about their own stage).
        var hodId = (await departments.GetByIdAsync(r.DepartmentId, ct))?.HeadOfDepartmentUserId;
        var informHod = hodId is not null && hodId != r.RequestedByUserId && decidedStage != RecruitmentRequestStatus.DepartmentHeadReview;

        switch (r.Status)
        {
            case RecruitmentRequestStatus.HrApproval or RecruitmentRequestStatus.MdApproval:
                await NotifyRoleAsync(RecruitmentWorkflow.ApproverRole(r.Status)!, r,
                    $"Recruitment request {r.RequestNumber} needs your approval",
                    $"The request for <b>{Enc(r.PositionTitle)}</b> passed {stageName} and is waiting for your decision.{commentHtml}", ct);
                await NotifyUserAsync(r.RequestedByUserId, r,
                    $"{r.RequestNumber} passed {stageName}",
                    $"Your request for <b>{Enc(r.PositionTitle)}</b> passed {stageName} and moved to the next approval.{commentHtml}", ct);
                break;

            case RecruitmentRequestStatus.Approved:
                var body = $"The request for <b>{Enc(r.PositionTitle)}</b> has received final approval from the Managing Director. Recruitment can now begin.{commentHtml}";
                await NotifyUserAsync(r.RequestedByUserId, r, $"{r.RequestNumber} approved", body, ct);
                if (hodId is not null && hodId != r.RequestedByUserId) await NotifyUserAsync(hodId, r, $"{r.RequestNumber} approved", body, ct);
                await NotifyRoleAsync(Roles.HrManager, r, $"{r.RequestNumber} approved", body, ct);
                await NotifyRoleAsync(Roles.RecruitmentOfficer, r, $"{r.RequestNumber} approved: please convert it into a vacancy",
                    body + "<p>Open the request and click <b>Convert into a job vacancy</b>.</p>", ct);
                break;

            case RecruitmentRequestStatus.Returned:
                var returned = $"The request for <b>{Enc(r.PositionTitle)}</b> was returned for correction at {stageName}.{commentHtml}";
                await NotifyUserAsync(r.RequestedByUserId, r, $"{r.RequestNumber} was returned for correction",
                    returned + "<p>Please make the corrections and resubmit it.</p>", ct);
                if (informHod) await NotifyUserAsync(hodId, r, $"{r.RequestNumber} was returned for correction", returned, ct);
                break;

            case RecruitmentRequestStatus.Rejected:
                var rejected = $"The request for <b>{Enc(r.PositionTitle)}</b> was rejected at {stageName}.{commentHtml}";
                await NotifyUserAsync(r.RequestedByUserId, r, $"{r.RequestNumber} was rejected", rejected, ct);
                if (informHod) await NotifyUserAsync(hodId, r, $"{r.RequestNumber} was rejected", rejected, ct);
                break;
        }
    }

    // Sending is shared with other modules (Application/Common/WorkflowMailer.cs).

    /// <summary>Emails a role's members. The requester is skipped because they get their own message.</summary>
    private Task NotifyRoleAsync(string role, RecruitmentRequest r, string subject, string html, CancellationToken ct) =>
        mailer.ToRoleAsync(role, [r.RequestedByUserId], subject, html, links.RecruitmentRequest(r.Id), $"Open request {r.RequestNumber}", ct);

    private Task NotifyUserAsync(string? userId, RecruitmentRequest r, string subject, string html, CancellationToken ct) =>
        mailer.ToUserAsync(userId, subject, html, links.RecruitmentRequest(r.Id), $"Open request {r.RequestNumber}", ct);

    private static string Enc(string text) => WorkflowMailer.Enc(text);
}
