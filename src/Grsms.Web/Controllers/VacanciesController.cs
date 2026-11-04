using Grsms.Application.Organization;
using Grsms.Application.Vacancies;
using Grsms.Domain.Security;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// Vacancy screens (Module 3): list tabs, create from an approved request, edit, details,
/// submit, approve/return, close, reopen and archive. All rules live in <see cref="VacancyService"/>.
/// </summary>
[Authorize(Policy = Policies.Vacancies)]
public class VacanciesController(VacancyService service, Application.Adverts.AdvertService adverts,
    Application.Applications.ApplicationService applications) : AppController
{
    // ================================================================= List

    /// <param name="tab">"open", "pending", "awaiting" (approved requests without a vacancy) or "all".</param>
    public async Task<IActionResult> Index([FromQuery] VacancyFilter filter, CancellationToken ct)
    {
        var counts = await service.CountsAsync(ct);

        // HR Managers start on what needs their approval; Recruitment Officers on requests needing a vacancy.
        if (!Request.Query.ContainsKey("tab"))
            filter.Tab = User.IsInRole(Roles.HrManager) && counts.Pending > 0 ? "pending"
                : User.IsInRole(Roles.RecruitmentOfficer) && counts.AwaitingVacancy > 0 ? "awaiting"
                : "open";

        ViewBag.Filter = filter;
        ViewBag.OpenCount = counts.Open;
        ViewBag.PendingCount = counts.Pending;
        ViewBag.AwaitingCount = counts.AwaitingVacancy;
        ViewBag.CanCreate = User.IsInRole(Roles.RecruitmentOfficer);
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);

        if (filter.Tab == "awaiting")
        {
            ViewBag.ApprovedRequests = await service.GetApprovedRequestsWithoutVacancyAsync(ct);
            return View(new Application.Common.PagedResult<VacancyListItemDto>([], 1, filter.PageSize, 0));
        }
        return View(await service.SearchAsync(filter, ct));
    }

    // ================================================================= Create / edit

    /// <summary>New vacancy form, pre-filled from the approved request.</summary>
    public async Task<IActionResult> Create(int requestId, CancellationToken ct)
    {
        var result = await service.GetNewFormAsync(requestId, ct);
        if (!result.Succeeded)
        {
            Error(string.Join(" ", result.Errors));
            return RedirectToAction(nameof(Index), new { tab = "awaiting" });
        }
        await LoadListsAsync(result.Value!, ct);
        return View("Form", result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(VacancyFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await service.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Vacancy saved as a draft. Check it, then click 'Submit for approval'.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await service.RefreshReadOnlyFieldsAsync(form, ct);
        await LoadListsAsync(form, ct);
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await service.GetForEditAsync(id, ct);
        if (form is null) return NotFound();
        await LoadListsAsync(form, ct);
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, VacancyFormDto form, CancellationToken ct)
    {
        form.Id = id;
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
        await service.RefreshReadOnlyFieldsAsync(form, ct);
        await LoadListsAsync(form, ct);
        return View("Form", form);
    }

    // ================================================================= Details + actions

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await service.GetDetailsAsync(id, ct);
        if (model is null) return NotFound();

        // Phase 4: the vacancy's job adverts, and whether the user may create one now.
        ViewBag.Adverts = await adverts.ListForVacancyAsync(id, ct);
        ViewBag.CanAdvertise = await adverts.CanCreateForAsync(id, ct);

        // Phase 5: how many applications it has received.
        ViewBag.ApplicationCount = (await applications.SearchAsync(new Application.Applications.ApplicationFilter { Tab = "all", VacancyId = id, PageSize = 1 }, ct)).TotalCount;
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> Submit(int id, CancellationToken ct) =>
        Act(id, () => service.SubmitAsync(id, ct), "Submitted. The HR Managers have been notified.");

    [HttpPost]
    public async Task<IActionResult> Decide(int id, VacancyDecisionDto form, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await service.DecideAsync(id, form, ct)
            : Application.Common.Result.Failure("Choose Approve or Return for changes.");
        if (!result.Succeeded)
        {
            Error(string.Join(" ", result.Errors));
            return RedirectToAction(nameof(Details), new { id });
        }
        Success(form.Decision == VacancyDecision.Approve
            ? "Approved. The vacancy is now published and open for applications."
            : "Returned to the Recruitment Officer for changes.");
        return RedirectToAction(nameof(Index), new { tab = "pending" });
    }

    [HttpPost]
    public Task<IActionResult> Close(int id, string? reason, CancellationToken ct) =>
        Act(id, () => service.CloseAsync(id, reason, ct), "Vacancy closed. It no longer accepts applications.");

    [HttpPost]
    public Task<IActionResult> Reopen(int id, DateOnly? closingDate, CancellationToken ct) =>
        Act(id, () => service.ReopenAsync(id, closingDate, ct), "Vacancy reopened with the new closing date.");

    [HttpPost]
    public Task<IActionResult> Archive(int id, string? reason, CancellationToken ct) =>
        Act(id, () => service.ArchiveAsync(id, reason, ct), "Vacancy archived.");

    // ================================================================= Helpers

    /// <summary>Runs a workflow action and returns to the details page with a success or error banner.</summary>
    private async Task<IActionResult> Act(int id, Func<Task<Application.Common.Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task LoadListsAsync(VacancyFormDto form, CancellationToken ct)
    {
        ViewBag.UnitList = new SelectList(await service.GetUnitsAsync(form.DepartmentId, ct),
            nameof(LookupItem.Value), nameof(LookupItem.Text), form.UnitId?.ToString());
        ViewBag.SiteList = new SelectList(await service.GetSitesAsync(ct),
            nameof(LookupItem.Value), nameof(LookupItem.Text), form.SiteId?.ToString());
        ViewBag.ManagerList = new SelectList(await service.GetReportingManagersAsync(form.DepartmentId, ct),
            nameof(LookupItem.Value), nameof(LookupItem.Text), form.ReportingManagerUserId);
    }
}
