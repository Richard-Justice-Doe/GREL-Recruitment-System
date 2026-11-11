using Grsms.Application.Common;

namespace Grsms.Application.Users;

/// <summary>
/// Phase 18: turns a new employee's GRSMS account into a GREL staff account when their onboarding is activated.
/// Implemented in Infrastructure with ASP.NET Core Identity (Infrastructure/Identity/EmployeeAccounts.cs).
///
/// This is a workflow step, not user administration: the caller (OnboardingService) has already checked that the
/// person acting is HR and that the onboarding is at "Awaiting activation". Only the everyday Employee role is given,
/// never a privileged one, so it does not need a Super Admin.
/// </summary>
public interface IEmployeeAccounts
{
    /// <summary>
    /// Sets the staff number, department and site, marks the account as staff (no longer a candidate), adds the Employee
    /// role and removes the Candidate role. Fails if the staff number belongs to another account.
    /// </summary>
    Task<Result> ActivateAsStaffAsync(string userId, string staffNumber, int departmentId, int siteId, CancellationToken ct = default);
}
