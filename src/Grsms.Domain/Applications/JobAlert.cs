using Grsms.Domain.Common;

namespace Grsms.Domain.Applications;

/// <summary>
/// 12 November 2026 (docs/34 quick win): a person's job alert. They chose it themselves on My profile (opt-in): when an
/// advert goes live on the public careers page and matches their keywords (or any vacancy, without keywords), they are emailed.
/// One per person.
/// </summary>
public class JobAlertSubscription : BaseEntity
{
    /// <summary>The person's account (Users.Id). Unique.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Words separated by commas, e.g. "engineer, accounting". Empty = every vacancy.</summary>
    public string? Keywords { get; set; }

    /// <summary>Switched off by the person (the row is kept, so their keywords come back if they switch it on again).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? LastSentAt { get; set; }

    /// <summary>The keywords as a list (lower case, trimmed, no empties).</summary>
    public IReadOnlyList<string> KeywordList =>
        (Keywords ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(k => k.ToLowerInvariant()).ToList();

    /// <summary>True when the text (job title, advert) contains one of the keywords, or when there are none.</summary>
    public bool Matches(string text)
    {
        var words = KeywordList;
        return words.Count == 0 || words.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
