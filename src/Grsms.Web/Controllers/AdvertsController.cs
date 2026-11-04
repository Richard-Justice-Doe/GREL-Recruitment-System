using Grsms.Application.Adverts;
using Grsms.Application.Common;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Job advert screens (Module 4): list tabs, create from a published vacancy, edit, details, publish / schedule,
/// cancel schedule, close, clone, and recording external channel postings. All rules live in <see cref="AdvertService"/>.
/// </summary>
[Authorize(Policy = Policies.Adverts)]
public class AdvertsController(AdvertService service) : AppController
{
    // ================================================================= List

    /// <param name="filter">Tab "live", "scheduled", "draft" or "all", plus the report filters.</param>
    public async Task<IActionResult> Index([FromQuery] AdvertFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        ViewBag.Counts = await service.CountsAsync(ct);
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        return View(await service.SearchAsync(filter, ct));
    }

    // ================================================================= Create / edit

    /// <summary>New advert form for a published vacancy, pre-filled from it.</summary>
    public async Task<IActionResult> Create(int vacancyId, CancellationToken ct)
    {
        var result = await service.GetNewFormAsync(vacancyId, ct);
        if (!result.Succeeded)
        {
            Error(string.Join(" ", result.Errors));
            return RedirectToAction("Details", "Vacancies", new { id = vacancyId });
        }
        return View("Form", result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AdvertFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await service.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Advert saved as a draft. Check it, then click 'Publish'.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await service.FillFormDetailsAsync(form, ct);
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await service.GetForEditAsync(id, ct);
        return form is null ? NotFound() : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AdvertFormDto form, CancellationToken ct)
    {
        form.Id = id; // the id in the address wins over anything posted in the form
        if (ModelState.IsValid)
        {
            var result = await service.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Changes saved.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await service.FillFormDetailsAsync(form, ct);
        return View("Form", form);
    }

    // ================================================================= Details + actions

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await service.GetDetailsAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public Task<IActionResult> Publish(int id, CancellationToken ct) =>
        Act(id, () => service.PublishAsync(id, ct), "Done. A live advert now shows on its automatic channels; a scheduled one goes live on its start date.");

    [HttpPost]
    public Task<IActionResult> Unschedule(int id, CancellationToken ct) =>
        Act(id, () => service.UnscheduleAsync(id, ct), "Schedule cancelled. The advert is a draft again.");

    [HttpPost]
    public Task<IActionResult> Close(int id, string? reason, CancellationToken ct) =>
        Act(id, () => service.CloseAsync(id, reason, ct), "Advert closed. Remember to take it down from any external channels.");

    [HttpPost]
    public async Task<IActionResult> Clone(int id, CancellationToken ct)
    {
        var result = await service.CloneAsync(id, null, ct);
        if (!result.Succeeded)
        {
            Error(string.Join(" ", result.Errors));
            return RedirectToAction(nameof(Details), new { id });
        }
        Success("Copy created as a new draft. Check the dates and channels, then publish it.");
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpPost]
    public Task<IActionResult> MarkPosted(int id, int channelId, string? reference, CancellationToken ct) =>
        Act(id, () => service.MarkPostedAsync(id, channelId, reference, ct), "Channel recorded as posted.");

    [HttpPost]
    public Task<IActionResult> MarkRemoved(int id, int channelId, CancellationToken ct) =>
        Act(id, () => service.MarkRemovedAsync(id, channelId, ct), "Channel recorded as taken down.");

    /// <summary>Runs an action and returns to the details page with a success or error banner.</summary>
    private async Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }
}
