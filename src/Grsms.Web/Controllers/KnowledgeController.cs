using Grsms.Application.Knowledge;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// docs/36 AI-7: "Policies &amp; procedures" for GREL staff: the library of active documents, a question box (an AI answer with
/// citations when "Knowledge base answers" is on, otherwise the matching sections), and each document's sections. The file itself
/// can also be opened by a new employee when the document is marked for new employees (the service checks).
/// </summary>
[Authorize]
public class KnowledgeController(KnowledgeBaseService knowledge) : AppController
{
    [Authorize(Policy = Policies.Staff)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.AnswersOn = knowledge.AnswersOn;
        ViewBag.CanManage = knowledge.CanManage;
        return View(await knowledge.ListAsync(ct));
    }

    /// <summary>A question; the answer is shown on the library page (POST, so the question isn't kept in browser history or logs).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Staff)]
    public async Task<IActionResult> Ask(string? question, CancellationToken ct)
    {
        ViewBag.Answer = await knowledge.AskAsync(question, ct);
        ViewBag.AnswersOn = knowledge.AnswersOn;
        ViewBag.CanManage = knowledge.CanManage;
        return View(nameof(Index), await knowledge.ListAsync(ct));
    }

    [Authorize(Policy = Policies.Staff)]
    public async Task<IActionResult> Document(int id, CancellationToken ct)
    {
        var d = await knowledge.GetAsync(id, ct);
        return d is null ? NotFound() : View(d);
    }

    /// <summary>The document's file (preview or download). attachmentId: an older version, for HR.</summary>
    // Any signed-in user: new employees too, for documents marked for them (the service checks).
    public async Task<IActionResult> Open(int id, int? attachmentId, string? view, CancellationToken ct) =>
        await ServeFileAsync(await knowledge.OpenFileAsync(id, attachmentId, ct), view, ct);
}
