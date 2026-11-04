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
}

/// <summary>
/// User account management for the HR Administrator (Users screen).
/// </summary>
public interface IUserAdminService
{
    /// <summary>All users. Staff only by default; set <paramref name="includeCandidates"/> to also list portal candidates.</summary>
    Task<IReadOnlyList<UserListItemDto>> GetAllAsync(bool includeCandidates, CancellationToken ct = default);

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
