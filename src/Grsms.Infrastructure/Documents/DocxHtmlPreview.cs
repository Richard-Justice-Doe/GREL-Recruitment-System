using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ganss.Xss;
using Grsms.Application.Common.Interfaces;

namespace Grsms.Infrastructure.Documents;

/// <summary>
/// 14 November 2026 (docs/37 CR4): Word (.docx) to simple HTML for the preview, with no Word and no library: a .docx is a zip of XML
/// (as the CV reader already uses). Reads word/document.xml: headings (Heading1–6 / Title styles), paragraphs, bold / italic /
/// underline runs, line breaks, numbered and bulleted paragraphs (shown as list items), tables, and embedded pictures (PNG, JPEG,
/// GIF, as data: URIs, up to 5 MB in all). Every text is HTML-encoded and the result goes through the sanitizer, so nothing in a file
/// can run in the browser. DTDs and external entities are refused (XXE-safe), and a zip that expands beyond 50 MB is not read.
/// </summary>
public sealed class DocxHtmlPreview : IDocumentHtmlPreview
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const long MaxExpanded = 50L * 1024 * 1024, MaxImages = 5L * 1024 * 1024;

    private static readonly HtmlSanitizer Sanitizer = Build();

    private static HtmlSanitizer Build()
    {
        var s = new HtmlSanitizer();
        s.AllowedSchemes.Add("data");
        s.AllowedTags.Clear();
        foreach (var t in new[] { "h1", "h2", "h3", "h4", "h5", "h6", "p", "strong", "em", "u", "br", "ul", "li", "table", "tbody", "tr", "td", "img", "div" })
            s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        foreach (var a in new[] { "src", "alt", "colspan" }) s.AllowedAttributes.Add(a);
        s.AllowedCssProperties.Clear();
        return s;
    }

    public async Task<string?> ToHtmlAsync(Stream docx, CancellationToken ct = default)
    {
        try
        {
            using var buffer = new MemoryStream();
            await docx.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
            if (zip.Entries.Sum(e => e.Length) > MaxExpanded) return null;
            var body = Load(zip, "word/document.xml")?.Root?.Element(W + "body");
            if (body is null) return null;

            var images = new Dictionary<string, string>();
            if (Load(zip, "word/_rels/document.xml.rels")?.Root is { } rels)
            {
                long used = 0;
                foreach (var r in rels.Elements(Rel + "Relationship"))
                {
                    var target = (string?)r.Attribute("Target");
                    var id = (string?)r.Attribute("Id");
                    if (id is null || target is null || !target.StartsWith("media/", StringComparison.OrdinalIgnoreCase)) continue;
                    var mime = Path.GetExtension(target).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", _ => null };
                    var entry = zip.GetEntry("word/" + target);
                    if (mime is null || entry is null || used + entry.Length > MaxImages) continue;
                    using var s = entry.Open();
                    using var m = new MemoryStream();
                    await s.CopyToAsync(m, ct);
                    used += m.Length;
                    images[id] = $"data:{mime};base64,{Convert.ToBase64String(m.ToArray())}";
                }
            }

            var html = new StringBuilder();
            var inList = false;
            foreach (var block in body.Elements())
            {
                if (block.Name == W + "p")
                {
                    var isList = block.Element(W + "pPr")?.Element(W + "numPr") is not null;
                    if (isList && !inList) { html.Append("<ul>"); inList = true; }
                    if (!isList && inList) { html.Append("</ul>"); inList = false; }
                    Paragraph(block, html, images, isList);
                }
                else if (block.Name == W + "tbl")
                {
                    if (inList) { html.Append("</ul>"); inList = false; }
                    html.Append("<table><tbody>");
                    foreach (var row in block.Elements(W + "tr"))
                    {
                        html.Append("<tr>");
                        foreach (var cell in row.Elements(W + "tc"))
                        {
                            var span = (string?)cell.Element(W + "tcPr")?.Element(W + "gridSpan")?.Attribute(W + "val");
                            html.Append(span is { } c && int.TryParse(c, out var n) && n > 1 ? $"<td colspan=\"{n}\">" : "<td>");
                            foreach (var p in cell.Elements(W + "p")) Paragraph(p, html, images, false);
                            html.Append("</td>");
                        }
                        html.Append("</tr>");
                    }
                    html.Append("</tbody></table>");
                }
            }
            if (inList) html.Append("</ul>");
            return Sanitizer.Sanitize(html.ToString());
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static XDocument? Load(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        if (entry is null) return null;
        using var reader = XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static void Paragraph(XElement p, StringBuilder html, IReadOnlyDictionary<string, string> images, bool listItem)
    {
        var style = ((string?)p.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val") ?? "").ToLowerInvariant();
        var tag = listItem ? "li"
            : style == "title" ? "h1"
            : style.StartsWith("heading") && int.TryParse(style["heading".Length..], out var level) && level is >= 1 and <= 6 ? $"h{Math.Min(level + 1, 6)}"
            : "p";
        var inner = new StringBuilder();
        foreach (var r in p.Descendants(W + "r"))
        {
            var props = r.Element(W + "rPr");
            bool On(string name) => props?.Element(W + name) is { } e && (string?)e.Attribute(W + "val") is not ("0" or "false");
            foreach (var child in r.Elements())
            {
                if (child.Name == W + "t") inner.Append(Wrap(WebUtility.HtmlEncode(child.Value), On("b"), On("i"), On("u")));
                else if (child.Name == W + "tab") inner.Append(' ');
                else if (child.Name == W + "br" || child.Name == W + "cr") inner.Append("<br>");
                else if (child.Name == W + "drawing")
                    foreach (var blip in child.Descendants(A + "blip"))
                        if ((string?)blip.Attribute(R + "embed") is { } id && images.TryGetValue(id, out var src))
                            inner.Append($"<img src=\"{src}\" alt=\"Picture\">");
            }
        }
        if (inner.Length == 0 && tag == "p") { html.Append("<p><br></p>"); return; }
        html.Append('<').Append(tag).Append('>').Append(inner).Append("</").Append(tag).Append('>');
    }

    private static string Wrap(string text, bool bold, bool italic, bool underline)
    {
        if (underline) text = $"<u>{text}</u>";
        if (italic) text = $"<em>{text}</em>";
        if (bold) text = $"<strong>{text}</strong>";
        return text;
    }
}
