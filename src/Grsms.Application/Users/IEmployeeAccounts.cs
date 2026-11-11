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

    /// <summary>
    /// Phase 28: the staff account of an existing employee HR registers. When an account with <paramref name="email"/>
    /// exists it is used (a candidate account is refused: candidates become employees through onboarding) and given the
    /// staff number, department and site; otherwise a staff account is created without a password (the person sets one
    /// with "Forgot password"). Either way the Employee role is added. Returns the account's id.
    /// </summary>
    Task<Result<string>> RegisterStaffAsync(string fullName, string email, string? phone, string staffNumber, int departmentId, int siteId,
        CancellationToken ct = default);

    /// <summary>Phase 28: moves the account to another department and site (what a Head of Department may see follows it).</summary>
    Task<Result> UpdatePlacementAsync(string userId, int departmentId, int siteId, CancellationToken ct = default);

    /// <summary>Phase 28: the account with this email address (any kind), or null.</summary>
    Task<EmployeeAccountInfo?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Phase 28: the account's name, contact details and whether it may sign in.</summary>
    Task<EmployeeAccountInfo?> GetAsync(string userId, CancellationToken ct = default);
}

/// <summary>Phase 28: what the employee screens show from the user account.</summary>
public record EmployeeAccountInfo(string Id, string FullName, string? Email, string? Phone, bool IsActive, bool IsCandidate);
