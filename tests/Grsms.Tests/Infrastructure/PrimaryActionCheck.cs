using System.Text.RegularExpressions;

namespace Grsms.Tests.Infrastructure;

/// <summary>
/// docs/37 C6 "one primary button per page": every record page a web test opens (request, vacancy, advert, application,
/// interview, hiring case, onboarding, probation, employee, personnel file, the candidate's application) may show at most one
/// filled primary/success button. Jump links ("Go ↓") and secondary actions are outline buttons. Not counted: the per-document
/// buttons inside the folded checklist rows (each row is its own small task) and buttons inside pop-up dialogs.
/// Runs on every GET through <see cref="GrsmsWebFactory.NewClient"/>, so all the states the web tests reach are covered.
/// </summary>
public sealed partial class PrimaryActionCheck : DelegatingHandler
{
    [GeneratedRegex(@"^/(RecruitmentRequests|Vacancies|Adverts|Applications|Interviews|Onboarding|Probation|Employees|PersonnelFiles|MyApplications)/Details/\d+$|^/Hiring/Case/\d+$")]
    private static partial Regex RecordPage();

    [GeneratedRegex(@"<(a|button)\b[^>]*\bclass=""[^""]*\bbtn-(?:primary|success)\b[^""]*""[^>]*>(.*?)</\1>", RegexOptions.Singleline)]
    private static partial Regex FilledButton();

    [GeneratedRegex(@"<details\b|</details>")]
    private static partial Regex DetailsTag();

    [GeneratedRegex(@"<div class=""modal\b.*?<div class=""modal-footer[^>]*>.*?</div>", RegexOptions.Singleline)]
    private static partial Regex Modal();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);
        if (request.Method != HttpMethod.Get || !response.IsSuccessStatusCode || !RecordPage().IsMatch(request.RequestUri!.AbsolutePath)) return response;
        if (response.Content.Headers.ContentType?.MediaType != "text/html") return response;

        var html = await response.Content.ReadAsStringAsync(ct);
        var labels = FilledButtons(html);
        Assert.True(labels.Count <= 1,
            $"{request.RequestUri.AbsolutePath} shows {labels.Count} primary buttons ({string.Join("; ", labels)}); keep one, make the others outline.");
        return response;
    }

    /// <summary>The labels of the filled primary/success buttons in the page's main area, as a person sees them at first.</summary>
    public static List<string> FilledButtons(string html)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var end = html.LastIndexOf("</main>", StringComparison.Ordinal);
        if (start < 0 || end < start) return [];
        var main = Modal().Replace(WithoutChecklistRows(html[start..end]), "");
        return FilledButton().Matches(main)
            .Select(m => Regex.Replace(Regex.Replace(m.Groups[2].Value, "<[^>]+>", ""), @"\s+", " ").Trim())
            .ToList();
    }

    /// <summary>Removes each <c>&lt;details class="doc-item ..."&gt;</c> block (with any details nested inside it).</summary>
    private static string WithoutChecklistRows(string html)
    {
        var sb = new System.Text.StringBuilder();
        int pos = 0, depth = 0, cut = -1;
        foreach (Match tag in DetailsTag().Matches(html))
        {
            var opening = tag.Value != "</details>";
            if (cut < 0)
            {
                if (opening && html.AsSpan(tag.Index).StartsWith("<details class=\"doc-item"))
                {
                    sb.Append(html, pos, tag.Index - pos);
                    cut = tag.Index; depth = 1;
                }
            }
            else
            {
                depth += opening ? 1 : -1;
                if (depth == 0) { pos = tag.Index + tag.Length; cut = -1; }
            }
        }
        if (cut < 0) sb.Append(html, pos, html.Length - pos);
        return sb.ToString();
    }
}
