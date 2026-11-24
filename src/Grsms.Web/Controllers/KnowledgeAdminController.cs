using Grsms.Application.Knowledge;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>docs/36 AI-7: Administration → Knowledge base. Holders of "Maintain the knowledge base" (HR) add and keep the documents.</summary>
[Authorize(Policy = Policies.KnowledgeBase)]
public class KnowledgeAdminController(KnowledgeBaseService knowledge) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.AnswersOn = knowledge.AnswersOn;
        return View(await knowledge.ListAsync(ct));
    }

    public IActionResult Create() => View("Form", new KnowledgeDocumentForm());

    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Create(KnowledgeDocumentForm form, IFormFile? file, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            await using var stream = file?.OpenReadStream();
            var result = await knowledge.UploadAsync(form, file?.FileName, stream, ct);
            if (result.Succeeded)
            {
                var d = await knowledge.GetAsync(result.Value, ct);
                if (d?.Document.ReadProblem is { } problem) Error($"\"{form.Title}\" was added, but: {problem}");
                else Success($"\"{form.Title}\" was added: {d?.Document.SectionCount} sections can now be searched.");
                return RedirectToAction(nameof(Edit), new { id = result.Value });
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await knowledge.GetFormAsync(id, ct);
        if (form is null) return NotFound();
        ViewBag.Details = await knowledge.GetAsync(id, ct);
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, KnowledgeDocumentForm form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await knowledge.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"\"{form.Title}\" was saved.");
                return RedirectToAction(nameof(Edit), new { id });
            }
            AddErrors(result);
        }
        ViewBag.Details = await knowledge.GetAsync(id, ct);
        return View("Form", form);
    }

    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Replace(int id, IFormFile? file, string? version, CancellationToken ct)
    {
        await using var stream = file?.OpenReadStream();
        var result = await knowledge.ReplaceFileAsync(id, file?.FileName, stream, version, ct);
        if (!result.Succeeded) Fail(result);
        else if ((await knowledge.GetAsync(id, ct))?.Document is { } d)
        {
            if (d.ReadProblem is { } problem) Error($"The new file was saved, but: {problem}");
            else Success($"The new file was saved and read again: {d.SectionCount} sections.");
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public Task<IActionResult> SetActive(int id, bool active, CancellationToken ct) =>
        RunAsync(() => knowledge.SetActiveAsync(id, active, ct), active ? "The document is back in use." : "The document was archived: it is no longer searched or used in answers.",
            () => RedirectToAction(nameof(Edit), new { id }));

    [HttpPost]
    public Task<IActionResult> Delete(int id, CancellationToken ct) =>
        RunAsync(() => knowledge.DeleteAsync(id, ct), "The document was deleted.", () => RedirectToAction(nameof(Index)));
}
