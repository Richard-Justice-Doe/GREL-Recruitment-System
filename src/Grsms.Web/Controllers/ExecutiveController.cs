using Grsms.Application.Dashboard;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>13 November 2026 (docs/36 AI-3): the executive dashboard ("executive.view": MD and HR Managers by default).</summary>
[Authorize(Policy = Policies.ExecutiveDashboard)]
public class ExecutiveController(ExecutiveDashboardService dashboard) : AppController
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<IActionResult> Index(CancellationToken ct) => View(await dashboard.GetAsync(Today, ct));

    /// <summary>The AI summary for the panel (js/ai-panel.js); read-only, nothing to copy into a form.</summary>
    [HttpPost]
    public async Task<IActionResult> Summary(bool regenerate, CancellationToken ct)
    {
        var r = await dashboard.SummaryAsync(Today, regenerate, ct);
        if (!r.Succeeded)
            return Json(new AiController.PanelResponse(false, r.Problem, false, null, null, [], new Dictionary<string, string>()));
        var s = r.Value!;
        static string Bullets(IEnumerable<string> l) => string.Join("\n", l.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => "• " + x.Trim()));
        var sections = new List<AiController.PanelSection> { new("In one sentence", s.Headline.Trim()) };
        if (s.Highlights.Count > 0) sections.Add(new("Highlights", Bullets(s.Highlights)));
        if (s.Risks.Count > 0) sections.Add(new("Needs attention", Bullets(s.Risks)));
        if (s.Actions.Count > 0) sections.Add(new("Suggested next steps", Bullets(s.Actions)));
        return Json(new AiController.PanelResponse(true, null, r.FromCache, "High", "Every number comes from GRSMS's figures on this page; the wording is AI's.",
            sections, new Dictionary<string, string>()));
    }
}
