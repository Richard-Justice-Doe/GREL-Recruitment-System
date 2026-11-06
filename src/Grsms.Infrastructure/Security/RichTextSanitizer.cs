using AngleSharp.Css.Dom;
using Ganss.Xss;
using Grsms.Application.Common;

namespace Grsms.Infrastructure.Security;

/// <summary>
/// <see cref="IRichTextSanitizer"/> built on the HtmlSanitizer library (a real HTML parser, so tricks such as broken tags
/// or encoded "javascript:" cannot slip through). Keeps only <see cref="RichText.AllowedTags"/>, and no attributes, CSS,
/// URLs or comments at all. Content of dropped dangerous tags (script, style, ...) is removed; other unknown tags
/// (span, div, font, ... pasted from Word) are unwrapped so their text is kept.
/// </summary>
public class RichTextSanitizer : IRichTextSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public RichTextSanitizer()
    {
        _sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(RichText.AllowedTags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(),
            AllowedCssProperties = new HashSet<string>(),
            AllowedSchemes = new HashSet<string>(),
            AllowedAtRules = new HashSet<CssRuleType>(),
            UriAttributes = new HashSet<string>(),
        });
        _sanitizer.KeepChildNodes = true; // unwrap <span>, <div>, <font>, ... instead of losing the text inside them

        // ... except for tags whose content is code or embedded content, never text a person typed: drop those whole.
        _sanitizer.RemovingTag += (_, e) =>
        {
            if (DroppedWithContent.Contains(e.Tag.LocalName)) e.Tag.TextContent = "";
        };
    }

    private static readonly HashSet<string> DroppedWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "noscript", "template", "textarea", "select", "svg", "math", "head", "title"
    };

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var clean = _sanitizer.Sanitize(RichText.FromPlainTextIfNeeded(html)).Trim();
        return RichText.CharacterCount(clean) == 0 ? "" : clean;
    }
}
