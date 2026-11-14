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

/// <summary>The answer of a virus scan.</summary>
/// <param name="Clean">False = the file must be refused.</param>
/// <param name="Threat">What was found (shown to the uploader), when not clean.</param>
public record VirusScanResult(bool Clean, string? Threat = null)
{
    public static readonly VirusScanResult Passed = new(true);
}

/// <summary>
/// Phase 27: scans every uploaded file before it is stored. The built-in implementation (Infrastructure/Storage/NoVirusScanner)
/// lets every file through; GREL IT can plug in a real scanner (e.g. ClamAV or Microsoft Defender) by registering another
/// implementation. A file that is not clean is refused and never written to disk.
/// </summary>
public interface IVirusScanner
{
    Task<VirusScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default);
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

    /// <summary>Phase 15: a vacancy's ATS screening dashboard.</summary>
    string AtsVacancy(int vacancyId);

    /// <summary>An interview or assessment session.</summary>
    string Interview(int id);

    /// <summary>The hiring case of an application (checks, recommendation, offer).</summary>
    string HiringCase(int applicationId);

    /// <summary>An onboarding (checklist, appointment letter, first day) for staff.</summary>
    string Onboarding(int id);

    /// <summary>Phase 21: a probation (staff page).</summary>
    string Probation(int id);

    /// <summary>Phase 21: the employee's own probation page.</summary>
    string MyProbation();

    /// <summary>12 November 2026: an employee record (staff page).</summary>
    string Employee(int id);

    /// <summary>12 November 2026: the home page (for people with no page of their own to open).</summary>
    string Home();

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

/// <summary>
/// 13 November 2026 (docs/26 Phase 24, docs/35 Wave 1): turns a complete HTML page into a PDF. The built-in implementation
/// (Infrastructure/Documents/ChromiumPdfRenderer) runs headless Chromium (Chrome or Edge on Windows, the "chromium" package in
/// the Docker image), so the PDF looks exactly like the HTML letter.
/// </summary>
public interface IPdfRenderer
{
    /// <summary>False when no Chromium browser is found on this server: the PDF buttons are hidden and the HTML letters still work.</summary>
    bool IsAvailable { get; }

    /// <summary>The PDF bytes, or null if rendering failed (logged).</summary>
    Task<byte[]?> RenderAsync(string html, CancellationToken ct = default);
}

/// <summary>
/// 14 November 2026 (docs/37 CR4): a Word (.docx) document as simple, safe HTML for the in-page preview (headings, paragraphs, bold and
/// italic, lists, tables, embedded pictures). Layout is approximate; the original file is unchanged and can still be downloaded.
/// Implemented in Infrastructure/Documents/DocxHtmlPreview.
/// </summary>
public interface IDocumentHtmlPreview
{
    /// <summary>The HTML (a fragment, no scripts), or null when the file can't be read as a Word document.</summary>
    Task<string?> ToHtmlAsync(Stream docx, CancellationToken ct = default);
}
