using System.Net;
using System.Text.RegularExpressions;

namespace Grsms.Application.Common;

/// <summary>
/// Cleans formatted text typed in the rich-text editor (e.g. "Description of the post" on a recruitment request) so it is
/// safe to show as HTML: only <see cref="RichText.AllowedTags"/> are kept, with no attributes at all, so no script, link,
/// style or event handler can survive. Implemented in Infrastructure with the HtmlSanitizer library.
/// </summary>
public interface IRichTextSanitizer
{
    /// <summary>The cleaned HTML, or an empty string when nothing visible is left.</summary>
    string Sanitize(string? html);
}

/// <summary>Helpers for the small, safe HTML subset the rich-text editor produces.</summary>
public static partial class RichText
{
    /// <summary>The only tags kept by <see cref="IRichTextSanitizer"/>. Attributes are always removed.</summary>
    public static readonly IReadOnlyList<string> AllowedTags =
        ["p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "h3", "h4", "blockquote"];

    /// <summary>
    /// Text typed without the editor (e.g. JavaScript switched off) has no tags: each line becomes a paragraph so the
    /// line breaks survive. HTML is returned unchanged.
    /// </summary>
    public static string FromPlainTextIfNeeded(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        if (TagPattern().IsMatch(input)) return input;
        return string.Concat(input.Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Trim().Length > 0)
            .Select(l => $"<p>{WebUtility.HtmlEncode(l.Trim())}</p>"));
    }

    /// <summary>
    /// The text without formatting: paragraphs and headings on their own lines, list items as "• item". Used for the
    /// character limit and to pre-fill a vacancy's plain-text job description.
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = BlockEndPattern().Replace(html, "\n");
        text = ListItemPattern().Replace(text, "• ");
        text = TagPattern().Replace(text, "");
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// How many characters of text (not formatting) the content holds. Counted exactly like the editor's on-screen
    /// counter (the element's textContent without line breaks), so the limit the user sees is the limit enforced.
    /// </summary>
    public static int CharacterCount(string? html) =>
        string.IsNullOrEmpty(html) ? 0 : WebUtility.HtmlDecode(TagPattern().Replace(html, "")).Replace("\r", "").Replace("\n", "").Length;

    [GeneratedRegex("<[a-zA-Z/][^>]*>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"<br\s*/?>|</(p|h3|h4|li|blockquote|ul|ol)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndPattern();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemPattern();
}
