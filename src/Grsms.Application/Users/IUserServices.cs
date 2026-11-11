using Grsms.Application.Common;
using Grsms.Application.Organization;

namespace Grsms.Application.Users;

/// <summary>
/// Read-only questions about users that other modules need to ask
/// (e.g. "what is this HOD's name?", "who holds the HR Manager role?").
///
/// User accounts are stored by ASP.NET Core Identity, which lives in the Infrastructure layer,
/// so the implementation is there (Infrastructure/Identity/UserDirectory.cs).
/// </summary>
public interface IUserDirectory
{
    /// <summary>Maps user ids to display names. Unknown ids are left out of the result.</summary>
    Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default);

    /// <summary>True when the user exists, is active, and holds <paramref name="role"/>.</summary>
    Task<bool> IsActiveUserInRoleAsync(string userId, string role, CancellationToken ct = default);

    /// <summary>Active users holding <paramref name="role"/>, for drop-down lists (value = user id).</summary>
    Task<IReadOnlyList<LookupItem>> GetActiveUsersInRoleAsync(string role, CancellationToken ct = default);

    /// <summary>Name and email of every active user holding <paramref name="role"/> (for notification emails).</summary>
    Task<IReadOnlyList<UserContact>> GetContactsInRoleAsync(string role, CancellationToken ct = default);

    /// <summary>Name and email of one user, or null if not found.</summary>
    Task<UserContact?> GetContactAsync(string userId, CancellationToken ct = default);

    /// <summary>The user's home department (Users.DepartmentId), or null.</summary>
    Task<int?> GetDepartmentIdAsync(string userId, CancellationToken ct = default);

    /// <summary>Usability U4: the user's site (Users.SiteId), or null, to pre-fill forms.</summary>
    Task<int?> GetSiteIdAsync(string userId, CancellationToken ct = default);

    /// <summary>True for an active GREL staff account (not a candidate).</summary>
    Task<bool> IsActiveStaffAsync(string userId, CancellationToken ct = default);

    /// <summary>Active staff for drop-downs (value = user id). With a department: that department's staff only.</summary>
    Task<IReadOnlyList<LookupItem>> GetActiveStaffAsync(int? departmentId, CancellationToken ct = default);

    /// <summary>How many candidate accounts exist (home-page dashboard).</summary>
    Task<int> CountCandidatesAsync(CancellationToken ct = default);

    /// <summary>When each candidate account created on or after <paramref name="sinceUtc"/> was registered (dashboard chart).</summary>
    Task<IReadOnlyList<DateTime>> GetCandidateRegistrationDatesAsync(DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>The most recently registered candidates, newest first (dashboard "Latest candidates").</summary>
    Task<IReadOnlyList<CandidateSummary>> GetLatestCandidatesAsync(int count, CancellationToken ct = default);
}

/// <summary>A candidate account as shown on the dashboard.</summary>
public record CandidateSummary(string Id, string FullName, string Email, DateTime RegisteredAt, bool EmailConfirmed);

/// <summary>Who to email: id, display name and address.</summary>
public record UserContact(string Id, string FullName, string Email);

/// <summary>
/// User account management for the Super Admin and HR Administrator (Users screen).
/// </summary>
public interface IUserAdminService
{
    /// <summary>All users. Staff only by default; set <paramref name="includeCandidates"/> to also list portal candidates.</summary>
    Task<IReadOnlyList<UserListItemDto>> GetAllAsync(bool includeCandidates, CancellationToken ct = default);

    /// <summary>Phase 14: every role a staff account can be given (built-in staff roles, then custom roles).</summary>
    Task<IReadOnlyList<string>> GetAssignableRolesAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a CANDIDATE account for someone (e.g. while self-registration is closed for a trial). It has no password:
    /// the person chooses one through the welcome email's link. Returns the new user id.
    /// </summary>
    Task<Result<string>> CreateCandidateAsync(string fullName, string email, CancellationToken ct = default);

    /// <summary>Loads a staff account into the edit form, or null if not found.</summary>
    Task<UserFormDto?> GetForEditAsync(string id, CancellationToken ct = default);

    /// <summary>Creates a staff account with a temporary password. Returns the new user id.</summary>
    Task<Result<string>> CreateAsync(UserFormDto form, CancellationToken ct = default);

    /// <summary>Updates a staff account (details, roles, active flag, optional password reset).</summary>
    Task<Result> UpdateAsync(UserFormDto form, string actingUserId, CancellationToken ct = default);

    /// <summary>Activates or deactivates any account (staff or candidate). Deactivated users cannot sign in.</summary>
    Task<Result> SetActiveAsync(string id, bool isActive, string actingUserId, CancellationToken ct = default);

    /// <summary>Clears a lockout caused by too many wrong passwords.</summary>
    Task<Result> UnlockAsync(string id, CancellationToken ct = default);
}
