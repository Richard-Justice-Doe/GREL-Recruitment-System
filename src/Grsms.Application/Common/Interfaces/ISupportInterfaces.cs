namespace Grsms.Application.Common.Interfaces;

/// <summary>
/// Hands out the next number in a named sequence, e.g. key "RR-HSE-2026" → 1, 2, 3, ...
/// Used for reference numbers (recruitment requests now; vacancies and offers later).
/// Safe when two people save at the same moment: they never receive the same number.
///
/// IMPORTANT: call it BEFORE adding new records to a repository, because it saves its own change immediately.
/// A number is used up even if the record that asked for it then fails to save, so gaps are possible (and harmless).
/// </summary>
public interface INumberGenerator
{
    Task<int> NextAsync(string sequenceKey, CancellationToken ct = default);
}

/// <summary>
/// Where uploaded files are physically kept (a local folder or a file-server share; see Storage settings).
/// Callers only ever deal with the opaque name returned by <see cref="SaveAsync"/>.
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores the content under a new random name and returns that name (e.g. "2026/11/ab12...ef.pdf").</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);

    /// <summary>Opens a stored file for reading, or returns null if it is missing.</summary>
    Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default);
}

/// <summary>
/// Builds full web addresses for links inside emails ("Open the request: https://.../RecruitmentRequests/Details/12").
/// Implemented in the Web project, which knows the site's address.
/// </summary>
public interface IAppLinks
{
    string RecruitmentRequest(int id);
    string Vacancy(int id);
    string Advert(int id);
    string Application(int id);
    string MyApplication(int id);

    /// <summary>A vacancy's shortlisting board.</summary>
    string Shortlist(int vacancyId);

    /// <summary>An interview or assessment session.</summary>
    string Interview(int id);

    /// <summary>The hiring case of an application (checks, recommendation, offer).</summary>
    string HiringCase(int applicationId);

    /// <summary>An onboarding (checklist, appointment letter, first day) for staff.</summary>
    string Onboarding(int id);

    /// <summary>A survey invitation, for the person invited to answer it.</summary>
    string Survey(int invitationId);

    /// <summary>The public careers page of one advert (used in copy-ready advert text).</summary>
    string CareersJob(string advertNumber);
}

/// <summary>
/// Thrown when someone else changed the same record between the moment it was loaded and the moment
/// it was saved (e.g. two approvers clicking "Approve" at the same time). Nothing is saved; the user
/// should reload the page and try again.
/// </summary>
public class ConcurrencyConflictException(Exception inner)
    : Exception("The record was changed by someone else while you were working on it.", inner);
