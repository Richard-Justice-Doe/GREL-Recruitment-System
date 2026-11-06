using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Probation;
using Grsms.Domain.Security;

namespace Grsms.Application.Probation;

/// <summary>One count on the Home page's onboarding and probation panel, with where clicking it goes.</summary>
public record WorkforceWidgetDto(string Title, int Count, string Icon, string Controller, string Action, IReadOnlyDictionary<string, string> Route, bool Attention);

/// <summary>The panel: HR's widgets and the HOD's widgets (either may be empty).</summary>
public record WorkforcePanelDto(IReadOnlyList<WorkforceWidgetDto> Hr, IReadOnlyList<WorkforceWidgetDto> Hod)
{
    public bool IsEmpty => Hr.Count == 0 && Hod.Count == 0;
}

/// <summary>
/// Phase 21: the onboarding and probation widgets on the Home dashboard.
///   * HR (Recruitment Officer, HR Manager): employees onboarding, documents awaiting verification, incomplete onboarding
///     cases (past the expected completion date), probation reviews due, probation extensions, confirmations pending.
///   * Head of Department (their departments): new employees assigned, objectives pending, assessments due, probation cases.
/// </summary>
public class WorkforceDashboardService(
    IRepository<OnboardingRecord> onboardings,
    IRepository<EmployeeDocument> documents,
    IRepository<ProbationRecord> probations,
    IRepository<ProbationAssessment> assessments,
    ICurrentUserService me,
    AccessScope scope)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly IReadOnlyDictionary<string, string> NoRoute = new Dictionary<string, string>();
    private static IReadOnlyDictionary<string, string> Tab(string tab) => new Dictionary<string, string> { ["tab"] = tab };

    public async Task<WorkforcePanelDto> GetAsync(CancellationToken ct = default)
    {
        var isHr = OnboardingWorkflow.HrRoles.Any(me.IsInRole);
        var hodDepts = me.IsInRole(Roles.HeadOfDepartment) ? await scope.HodDepartmentIdsAsync(ct) : [];
        if (!isHr && hodDepts.Count == 0) return new([], []);

        var active = (await onboardings.ListAsync(o => o.Status != OnboardingStatus.Completed && o.Status != OnboardingStatus.Cancelled, ct)).ToList();
        var probs = (await probations.ListAsync(p => p.Status != ProbationStatus.Confirmed && p.Status != ProbationStatus.NotConfirmed, ct)).ToList();
        var firstDone = (await assessments.ListAsync(a => a.Kind == ProbationAssessmentKind.First, ct)).Select(a => a.ProbationRecordId).ToHashSet();
        bool ReviewDue(ProbationRecord p)
        {
            var (_, due) = ProbationService.NextStep(p, firstDone.Contains(p.Id));
            return due is { } d && d <= Today.AddDays(ProbationWorkflow.AssessmentWindowDays);
        }

        var hr = new List<WorkforceWidgetDto>();
        if (isHr)
        {
            var verifying = active.Where(o => o.Status == OnboardingStatus.AwaitingDocumentVerification).Select(o => o.Id).ToList();
            var docsWaiting = verifying.Count == 0 ? 0 : await documents.CountAsync(d => verifying.Contains(d.OnboardingRecordId) &&
                (d.Status == EmployeeDocumentStatus.Submitted || (d.Status == EmployeeDocumentStatus.NotApplicable && d.VerifiedAt == null)), ct);
            hr.Add(new("Employees onboarding", active.Count, "fa-door-open", "Onboarding", "Index", Tab("progress"), false));
            hr.Add(new("Documents awaiting verification", docsWaiting, "fa-file-circle-check", "Onboarding", "Index", Tab("progress"), docsWaiting > 0));
            var late = active.Count(o => o.ExpectedCompletionDate is { } e && e < Today);
            hr.Add(new("Incomplete onboarding cases", late, "fa-hourglass-half", "Onboarding", "Index", Tab("progress"), late > 0));
            var due = probs.Count(ReviewDue);
            hr.Add(new("Probation reviews due", due, "fa-clipboard-check", "Probation", "Index", Tab("due"), due > 0));
            hr.Add(new("Probation extensions", probs.Count(p => p.Status == ProbationStatus.Extended), "fa-calendar-plus", "Probation", "Index", Tab("active"), false));
            var pending = probs.Count(p => p.Status == ProbationStatus.AwaitingHrDecision);
            hr.Add(new("Confirmations pending", pending, "fa-stamp", "Probation", "Index", Tab("decision"), pending > 0));
        }

        var hod = new List<WorkforceWidgetDto>();
        if (hodDepts.Count > 0)
        {
            var mine = probs.Where(p => hodDepts.Contains(p.DepartmentId)).ToList();
            hod.Add(new("New employees assigned", active.Count(o => hodDepts.Contains(o.DepartmentId)), "fa-user-plus", "Onboarding", "Index", Tab("progress"), false));
            var objectives = mine.Count(p => p.Status == ProbationStatus.InitialMeeting && p.HodSignedAt == null);
            hod.Add(new("Objectives pending", objectives, "fa-bullseye", "Probation", "Index", Tab("due"), objectives > 0));
            var assessmentsDue = mine.Count(p => ProbationWorkflow.CanBeAssessed(p.Status) && ReviewDue(p));
            hod.Add(new("Assessments due", assessmentsDue, "fa-clipboard-check", "Probation", "Index", Tab("due"), assessmentsDue > 0));
            hod.Add(new("Probation cases", mine.Count, "fa-user-clock", "Probation", "Index", Tab("active"), false));
        }
        return new(hr, hod);
    }
}
