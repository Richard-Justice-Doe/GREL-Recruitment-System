using System.IO.Compression;
using System.Text;
using Grsms.Infrastructure.Documents;

namespace Grsms.Tests;

/// <summary>docs/37 CR4: Word (.docx) to safe HTML for the preview: structure kept, text encoded, nothing executable, bad files refused.</summary>
public class DocxHtmlPreviewTests
{
    private static MemoryStream Docx(string bodyXml, string? rels = null, (string Name, byte[] Bytes)? media = null)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
                w.Write(text);
            }
            Add("word/document.xml",
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" " +
                "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><w:body>" + bodyXml + "</w:body></w:document>");
            if (rels is not null) Add("word/_rels/document.xml.rels", rels);
            if (media is { } m)
                using (var s = zip.CreateEntry("word/" + m.Name).Open()) s.Write(m.Bytes);
        }
        ms.Position = 0;
        return ms;
    }

    private const string P = "<w:p><w:r><w:t>{0}</w:t></w:r></w:p>";

    [Fact]
    public async Task Headings_paragraphs_formatting_lists_tables_and_pictures_become_safe_html()
    {
        var body =
            "<w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:t>Curriculum vitae</w:t></w:r></w:p>" +
            "<w:p><w:r><w:rPr><w:b/></w:rPr><w:t>Kofi</w:t></w:r><w:r><w:t xml:space=\"preserve\"> Mensah &lt;script&gt;alert(1)&lt;/script&gt;</w:t></w:r></w:p>" +
            "<w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/></w:numPr></w:pPr><w:r><w:t>Hydraulics</w:t></w:r></w:p>" +
            "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>2019</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Tema</w:t></w:r></w:p></w:tc></w:tr></w:tbl>" +
            "<w:p><w:r><w:drawing><a:blip r:embed=\"rId5\"/></w:drawing></w:r></w:p>";
        var rels = "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId5\" Target=\"media/image1.png\"/></Relationships>";
        var html = await new DocxHtmlPreview().ToHtmlAsync(Docx(body, rels, ("media/image1.png", [0x89, 0x50, 0x4E, 0x47, 1, 2, 3])));

        Assert.NotNull(html);
        Assert.Contains("<h2>Curriculum vitae</h2>", html);
        Assert.Contains("<strong>Kofi</strong>", html);
        Assert.Contains("&lt;script&gt;", html);             // shown as text, never as a tag
        Assert.DoesNotContain("<script", html);
        Assert.Contains("<ul><li>Hydraulics</li></ul>", html);
        Assert.Contains("<td><p>2019</p></td>", html);
        Assert.Contains("src=\"data:image/png;base64,", html);
    }

    [Fact]
    public async Task Files_that_are_not_word_documents_or_carry_a_dtd_are_refused()
    {
        Assert.Null(await new DocxHtmlPreview().ToHtmlAsync(new MemoryStream("%PDF-1.4"u8.ToArray())));
        var xxe = new MemoryStream();
        using (var zip = new ZipArchive(xxe, ZipArchiveMode.Create, leaveOpen: true))
        using (var w = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
            w.Write("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]><x>&e;</x>");
        xxe.Position = 0;
        Assert.Null(await new DocxHtmlPreview().ToHtmlAsync(xxe));
        _ = string.Format(P, "unused");
    }
}
