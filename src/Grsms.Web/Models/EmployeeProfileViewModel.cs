using Grsms.Application.Auditing;
using Grsms.Application.Employees;
using Grsms.Application.Onboarding;

namespace Grsms.Web.Models;

/// <summary>
/// 17 November 2026 (employee profile workspace): everything the profile page shows. The parts from other modules are null when the
/// person may not see them (personal record and documents: onboarding.view; audit trail: audit.view), so each tab follows the
/// rules of the module that owns the data.
/// </summary>
public sealed record EmployeeProfileViewModel(
    EmployeeDetailsDto Employee,
    EmployeeProfileExtras Extras,
    IReadOnlyList<TimelineEntryDto> Timeline,
    PersonalRecordPageDto? Personal,
    DocumentChecklistDto? Documents,
    OnboardingJourneyDto? Journey,
    IReadOnlyList<AuditLogDto>? Audit,
    bool CanSeePersonal,
    bool CanSeeAudit);
