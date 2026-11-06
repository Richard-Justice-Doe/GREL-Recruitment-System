using Grsms.Application.Probation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 21: the employee's own probation: objectives, development plan and assessments, signing the initial meeting, and
/// the confirmation letter. Only the employee's own records are ever shown (checked by <see cref="ProbationService"/>).
/// </summary>
[Authorize]
public class MyProbationController(ProbationService probation) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await probation.GetMineAsync(ct));

    /// <summary>Sign the initial meeting record (objectives and development plan) by typing my full name.</summary>
    [HttpPost]
    public async Task<IActionResult> Sign(int id, string? fullName, bool confirm, CancellationToken ct)
    {
        if (!confirm) Error("Tick the box to confirm you agree the objectives and development plan.");
        else
        {
            var result = await probation.SignAsEmployeeAsync(id, fullName, ct);
            if (result.Succeeded) Success("Thank you. Your probation objectives are signed.");
            else Error(string.Join(" ", result.Errors));
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Letter(int id, CancellationToken ct)
    {
        var model = await probation.GetConfirmationLetterAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.BackUrl = Url.Action(nameof(Index));
        return View("~/Views/Probation/Letter.cshtml", model);
    }
}
