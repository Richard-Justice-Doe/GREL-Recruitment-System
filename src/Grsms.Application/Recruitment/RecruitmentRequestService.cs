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
/// Recruitment Request Management (Module 2): raising, editing, submitting, approving, returning,
/// rejecting and cancelling requests, plus their attachments and notification emails.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Raise / edit / submit / cancel : the Head of Department who raised it, for a department they head.
///                                      Editing only while Draft or Returned.
///   * Decide at each stage           : the role named in <see cref="RecruitmentWorkflow.ApproverRole"/>,
///                                      and never the person who raised the request.
///   * See a request                  : its requester; the HOD of its department; and every
///                                      Recruitment Officer, HR Manager, Managing Director and HR Administrator.
///
/// Workflow participants are found automatically from the request's department (HOD) and the role holders
/// (Recruitment Officers, HR Manager, MD), so nobody has to pick approvers by hand.
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
    IVacancyLookup vacancyLookup)
{
    private const string ConflictMessage = "Someone else updated this request at the same moment. Please reload the page and try again.";

    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private string MyName => me.FullName ?? me.UserName ?? "Unknown";

    // ===================================================================== Look-ups for the form

    /// <summary>Departments the current user may raise requests for: the active departments they head.</summary>
    public async Task<IReadOnlyList<LookupItem>> GetCreatableDepartmentsAsync(CancellationToken ct = default)
    {
        var ids = await HodDepartmentIdsAsync(ct);
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

    /// <summary>Active Recruitment Officers, for "Assign recruiter" at the Recruitment Review stage.</summary>
    public Task<IReadOnlyList<LookupItem>> GetRecruitersAsync(CancellationToken ct = default) =>
        users.GetActiveUsersInRoleAsync(Roles.RecruitmentOfficer, ct);

    // ===================================================================== Reading

    /// <summary>The list page: requests the user may see, filtered and paged.</summary>
    public async Task<PagedResult<RecruitmentRequestListItemDto>> SearchAsync(RecruitmentRequestFilter filter, CancellationToken ct = default)
    {
        var visibility = await VisibilityAsync(ct);
        if (!filter.AwaitingMe)
            return await requests.SearchAsync(filter, visibility, null, null, ct);

        // "Awaiting my action" = sitting at a stage my role decides, and not raised by me.
        var myStages = RecruitmentWorkflow.ApprovalStages
            .Where(s => me.IsInRole(RecruitmentWorkflow.ApproverRole(s)!))
            .ToList();
        if (myStages.Count == 0)
            return new PagedResult<RecruitmentRequestListItemDto>([], 1, filter.PageSize, 0);

        return await requests.SearchAsync(filter, visibility, myStages, MyId, ct);
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
        var isMyStage = stageRole is not null && me.IsInRole(stageRole);

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
            VacancyReason = r.VacancyReason,
            ReplacedEmployee = r.ReplacedEmployee,
            Justification = r.Justification,
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
            CanCancel = isRequester && !RecruitmentWorkflow.IsFinal(r.Status),
            VacancyId = vacancy?.Id,
            VacancyNumber = vacancy?.Number,
            CanCreateVacancy = r.Status == RecruitmentRequestStatus.Approved && vacancy is null && me.IsInRole(Roles.RecruitmentOfficer)
        };
    }

    /// <summary>Loads a request into the edit form, or null if the user may not edit it.</summary>
    public async Task<RecruitmentRequestFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId || !RecruitmentWorkflow.IsEditable(r.Status)) return null;

        return new RecruitmentRequestFormDto
        {
            Id = r.Id,
            DepartmentId = r.DepartmentId,
            UnitId = r.UnitId,
            SiteId = r.SiteId,
            PositionTitle = r.PositionTitle,
            NumberOfPositions = r.NumberOfPositions,
            EmploymentType = r.EmploymentType,
            VacancyReason = r.VacancyReason,
            ReplacedEmployee = r.ReplacedEmployee,
            Justification = r.Justification,
            BudgetCode = r.BudgetCode,
            EstimatedAnnualCost = r.EstimatedAnnualCost
        };
    }

    // ===================================================================== Creating and editing (HOD)

    /// <summary>Saves a new request as a Draft and gives it its request number.</summary>
    public async Task<Result<int>> CreateAsync(RecruitmentRequestFormDto form, CancellationToken ct = default)
    {
        if (!me.IsInRole(Roles.HeadOfDepartment))
            return Result.Failure<int>("Only a Head of Department can raise a recruitment request.");

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
    /// Sends a Draft (or Returned) request into the workflow, to Recruitment Review.
    /// A job description must be attached first.
    /// </summary>
    public async Task<Result> SubmitAsync(int id, CancellationToken ct = default)
    {
        var r = await requests.GetWithHistoryAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure("Request not found.");
        if (!RecruitmentWorkflow.IsEditable(r.Status)) return Result.Failure("This request has already been submitted.");

        // Re-check the details: e.g. the department may have been deactivated since the draft was saved.
        var errors = await ValidateFormAsync(ToForm(r), ct);
        if (!await files.HasCategoryAsync(AttachmentOwners.RecruitmentRequest, r.Id, AttachmentCategory.JobDescription, ct))
            errors.Add("Attach the job description before submitting.");
        if (errors.Count > 0) return Result.Failure([.. errors]);

        var now = DateTime.UtcNow;
        var action = r.Status == RecruitmentRequestStatus.Returned ? WorkflowAction.Resubmitted : WorkflowAction.Submitted;
        r.DateRequested ??= now;
        // Refresh the division copy: an HR Administrator may have moved the department since the draft was saved.
        r.DivisionId = (await departments.GetByIdAsync(r.DepartmentId, ct))!.DivisionId;

        // Details may have changed since an earlier budget check, so the HR Manager validates again.
        r.BudgetValidated = false;
        r.BudgetValidatedByUserId = null;
        r.RecordStep(action, RecruitmentRequestStatus.RecruitmentReview, MyId, MyName, null, now);

        var saved = await SaveAsync(ct);
        if (saved.Succeeded)
            await NotifyRoleAsync(Roles.RecruitmentOfficer, r,
                $"Recruitment request {r.RequestNumber} needs your review",
                $"{MyName} has {(action == WorkflowAction.Resubmitted ? "resubmitted" : "submitted")} a recruitment request for <b>{Enc(r.PositionTitle)}</b>. It is waiting for Recruitment Review.", ct);
        return saved;
    }

    /// <summary>A reviewer's decision at the current approval stage: approve, return for changes, or reject.</summary>
    public async Task<Result> DecideAsync(int id, ReviewDecisionDto dto, CancellationToken ct = default)
    {
        var r = await requests.GetWithHistoryAsync(id, ct);
        if (r is null || !CanView(r, await VisibilityAsync(ct))) return Result.Failure("Request not found.");

        // Stale page: someone else already moved the request on.
        if (r.Status != dto.ExpectedStatus)
            return Result.Failure($"This request has already moved on (it is now '{r.Status}'). Please reload the page.");

        var stageRole = RecruitmentWorkflow.ApproverRole(r.Status);
        if (stageRole is null) return Result.Failure("This request is not waiting for a decision.");
        if (!me.IsInRole(stageRole)) return Result.Failure($"Only a {stageRole} can decide at this stage.");
        if (r.RequestedByUserId == MyId) return Result.Failure($"You raised this request, so another {stageRole} must decide.");

        var comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
        if (dto.Decision is ReviewDecision.Return or ReviewDecision.Reject && (comments is null || comments.Length < 5))
            return Result.Failure("Please explain your decision in the comments, so the Head of Department knows what to do.");

        var now = DateTime.UtcNow;
        var stage = r.Status;

        switch (dto.Decision)
        {
            case ReviewDecision.Approve:
                if (stage == RecruitmentRequestStatus.RecruitmentReview)
                {
                    // The reviewing Recruitment Officer assigns who will run this recruitment (themselves by default).
                    var recruiter = string.IsNullOrWhiteSpace(dto.AssignedRecruiterUserId) ? MyId : dto.AssignedRecruiterUserId;
                    if (!await users.IsActiveUserInRoleAsync(recruiter, Roles.RecruitmentOfficer, ct))
                        return Result.Failure("The assigned recruiter must be an active Recruitment Officer.");
                    r.AssignedRecruiterUserId = recruiter;
                }
                if (stage == RecruitmentRequestStatus.HrApproval)
                {
                    if (!dto.BudgetValidated)
                        return Result.Failure("Tick 'Budget validated' to confirm the budget code and estimated cost before approving.");
                    r.BudgetValidated = true;
                    r.BudgetValidatedByUserId = MyId;
                }

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
                return Result.Failure("Choose Approve, Return for changes, or Reject.");
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

    // ===================================================================== Attachments

    /// <summary>Adds a file to a Draft/Returned request (requester only).</summary>
    public async Task<Result<int>> AddAttachmentAsync(int id, AttachmentCategory category, string fileName, Stream content, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure<int>("Request not found.");
        if (!RecruitmentWorkflow.IsEditable(r.Status)) return Result.Failure<int>("Files can only be changed while the request is a draft or returned for changes.");

        return await files.AddAsync(AttachmentOwners.RecruitmentRequest, id, category, fileName, content, ct);
    }

    /// <summary>Removes a file from a Draft/Returned request (requester only).</summary>
    public async Task<Result> RemoveAttachmentAsync(int id, int attachmentId, CancellationToken ct = default)
    {
        var r = await requests.GetByIdAsync(id, ct);
        if (r is null || r.RequestedByUserId != MyId) return Result.Failure("Request not found.");
        if (!RecruitmentWorkflow.IsEditable(r.Status)) return Result.Failure("Files can only be changed while the request is a draft or returned for changes.");

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

    // ===================================================================== Helpers: validation and mapping

    private static void Normalize(RecruitmentRequestFormDto form)
    {
        form.PositionTitle = form.PositionTitle.Trim();
        form.Justification = form.Justification.Trim();
        form.BudgetCode = form.BudgetCode.Trim().ToUpperInvariant();
        form.ReplacedEmployee = string.IsNullOrWhiteSpace(form.ReplacedEmployee) ? null : form.ReplacedEmployee.Trim();
    }

    /// <summary>Rules the form attributes cannot check: the department, unit and site must be valid choices for this user.</summary>
    private async Task<List<string>> ValidateFormAsync(RecruitmentRequestFormDto form, CancellationToken ct)
    {
        var errors = new List<string>();

        var allowed = await GetCreatableDepartmentsAsync(ct);
        if (form.DepartmentId is not int deptId || allowed.All(d => d.Value != deptId.ToString()))
            errors.Add("You can only raise requests for an active department you head.");
        else if (form.UnitId is int unitId &&
                 !await units.AnyAsync(u => u.Id == unitId && u.DepartmentId == deptId && u.IsActive, ct))
            errors.Add("The selected unit is not an active unit of this department.");

        if (form.SiteId is not int siteId || !await sites.AnyAsync(s => s.Id == siteId && s.IsActive, ct))
            errors.Add("Choose an active site.");

        if (form.VacancyReason == VacancyReason.Replacement && form.ReplacedEmployee is null)
            errors.Add("For a replacement, enter the name of the employee being replaced.");

        return errors;
    }

    private static void Apply(RecruitmentRequestFormDto form, RecruitmentRequest r)
    {
        r.DepartmentId = form.DepartmentId!.Value;
        r.UnitId = form.UnitId;
        r.SiteId = form.SiteId!.Value;
        r.PositionTitle = form.PositionTitle;
        r.NumberOfPositions = form.NumberOfPositions;
        r.EmploymentType = form.EmploymentType!.Value;
        r.VacancyReason = form.VacancyReason!.Value;
        r.ReplacedEmployee = form.VacancyReason == VacancyReason.Replacement ? form.ReplacedEmployee : null;
        r.Justification = form.Justification;
        r.BudgetCode = form.BudgetCode;
        r.EstimatedAnnualCost = form.EstimatedAnnualCost!.Value;
    }

    private static RecruitmentRequestFormDto ToForm(RecruitmentRequest r) => new()
    {
        Id = r.Id, DepartmentId = r.DepartmentId, UnitId = r.UnitId, SiteId = r.SiteId,
        PositionTitle = r.PositionTitle, NumberOfPositions = r.NumberOfPositions,
        EmploymentType = r.EmploymentType, VacancyReason = r.VacancyReason, ReplacedEmployee = r.ReplacedEmployee,
        Justification = r.Justification, BudgetCode = r.BudgetCode, EstimatedAnnualCost = r.EstimatedAnnualCost
    };

    /// <summary>Saves, turning a simultaneous-edit clash into a friendly message.</summary>
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

    // ===================================================================== Helpers: notification emails

    /// <summary>After a decision: tell the next approvers, and keep the requester informed.</summary>
    private async Task NotifyAfterDecisionAsync(RecruitmentRequest r, RecruitmentRequestStatus decidedStage, string? comments, CancellationToken ct)
    {
        var commentHtml = comments is null ? "" : $"<p><i>Comments:</i> {Enc(comments)}</p>";
        var stageName = decidedStage switch
        {
            RecruitmentRequestStatus.RecruitmentReview => "Recruitment Review",
            RecruitmentRequestStatus.HrApproval => "HR Approval",
            _ => "MD Approval"
        };

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
                var body = $"The request for <b>{Enc(r.PositionTitle)}</b> has received final approval from the Managing Director. A vacancy can now be created.{commentHtml}";
                await NotifyUserAsync(r.RequestedByUserId, r, $"{r.RequestNumber} approved", body, ct);
                await NotifyUserAsync(r.AssignedRecruiterUserId, r, $"{r.RequestNumber} approved: please create the vacancy", body, ct);
                await NotifyRoleAsync(Roles.HrManager, r, $"{r.RequestNumber} approved", body, ct);
                break;

            case RecruitmentRequestStatus.Returned:
                await NotifyUserAsync(r.RequestedByUserId, r,
                    $"{r.RequestNumber} was returned for changes",
                    $"Your request for <b>{Enc(r.PositionTitle)}</b> was returned at {stageName}. Please make the changes and resubmit it.{commentHtml}", ct);
                break;

            case RecruitmentRequestStatus.Rejected:
                await NotifyUserAsync(r.RequestedByUserId, r,
                    $"{r.RequestNumber} was rejected",
                    $"Your request for <b>{Enc(r.PositionTitle)}</b> was rejected at {stageName}.{commentHtml}", ct);
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
