using Grsms.Domain.Applications;

namespace Grsms.Application.Applications;

/// <summary>
/// Phase 15: turns an uploaded CV file (PDF or Word .docx) into plain text, on GREL's own server.
/// Implemented in Infrastructure/Ats/CvTextExtractor.cs.
/// </summary>
public interface ICvTextExtractor
{
    /// <summary>The CV's text, or null when the file type is not supported or no text could be read (e.g. a scanned image).</summary>
    Task<string?> ExtractAsync(Stream content, string fileName, CancellationToken ct = default);
}

/// <summary>
/// Phase 15: the OPTIONAL Claude AI CV reader. Used only when the Super Admin has switched it on in the ATS settings
/// AND an API key is configured (Ats:Ai:ApiKey). When used, the CV's TEXT is sent to Anthropic. Any failure returns
/// null and GRSMS falls back to its own rule-based reader (<see cref="CvParser"/>), so applying never fails because of it.
/// </summary>
public interface IAiCvReader
{
    /// <summary>True when an API key is configured on the server.</summary>
    bool IsConfigured { get; }

    Task<ParsedCv?> ReadAsync(string cvText, DateOnly today, CancellationToken ct = default);
}
