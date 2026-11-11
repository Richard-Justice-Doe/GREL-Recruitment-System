using Grsms.Application.Employees;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>Phase 28 (docs/30): GREL's salary grades (Administration page). Holders of "Maintain grades".</summary>
[Authorize(Policy = Policies.Grades)]
public class GradesController(GradeService grades) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await grades.GetAllAsync(ct));

    public IActionResult Create() => View("Form", new GradeFormDto());

    [HttpPost]
    public async Task<IActionResult> Create(GradeFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await grades.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Grade {form.Code} was added.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await grades.GetForEditAsync(id, ct);
        return form is null ? NotFound() : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, GradeFormDto form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await grades.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Grade {form.Code} was saved.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        var result = await grades.SetActiveAsync(id, active, ct);
        if (result.Succeeded) Success(active ? "Grade activated." : "Grade deactivated.");
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }
}
