using System.Text;
using Grsms.Application.Common;
using Grsms.Application.Employees;
using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Domain.Employees;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 28 (docs/30): the employee master record: the list, one employee's record and job history, registering existing
/// staff (form or CSV import) and updating job details. Thin: every rule is in <see cref="EmployeeService"/>.
/// </summary>
[Authorize(Policy = Policies.Employees)]
public class EmployeesController(EmployeeService employees, GradeService grades, IDepartmentService departments, ISiteService sites, IUserDirectory users)
    : AppController
{
    private const long ImportLimitBytes = 2 * 1024 * 1024;

    /// <summary>Tabs: current (default), probation, active, left.</summary>
    public async Task<IActionResult> Index(string? tab, string? search, int? departmentId, int? gradeId, int page = 1, CancellationToken ct = default)
    {
        tab = tab is "probation" or "active" or "left" ? tab : "current";
        var filter = new EmployeeFilter
        {
            Search = search, DepartmentId = departmentId, GradeId = gradeId, Page = page,
            Status = tab switch { "probation" => EmployeeStatus.OnProbation, "active" => EmployeeStatus.Active, "left" => EmployeeStatus.Separated, _ => null }
        };
        ViewBag.Tab = tab;
        ViewBag.Search = search;
        ViewBag.DepartmentId = departmentId;
        ViewBag.GradeId = gradeId;
        ViewBag.Counts = await employees.CountsAsync(ct);
        ViewBag.CanManage = employees.MayManage;
        ViewBag.GradeList = new SelectList(await grades.GetActiveLookupAsync(ct), nameof(LookupItem.Value), nameof(LookupItem.Text), gradeId?.ToString());
        await LoadFilterListsAsync(null, departmentId, ct);
        return View(await employees.SearchAsync(filter, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await employees.GetAsync(id, ct);
        ViewBag.Timeline = model is null ? null : await employees.TimelineAsync(id, ct); // docs/34
        return model is null ? NotFound() : View(model);
    }

    // ------------------------------------------------------------------ registering existing staff

    [Authorize(Policy = Policies.ManageEmployees)]
    public async Task<IActionResult> Register(CancellationToken ct)
    {
        await LoadFormListsAsync(ct);
        return View(new RegisterEmployeeForm());
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageEmployees)]
    public async Task<IActionResult> Register(RegisterEmployeeForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await employees.RegisterAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"{form.FullName} was registered as an employee. They can sign in with {form.Email} after choosing a password with \"Forgot password\".");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await LoadFormListsAsync(ct);
        return View(form);
    }

    [Authorize(Policy = Policies.ManageEmployees)]
    public IActionResult Import() => View(new EmployeeImportResult(0, []));

    [HttpPost]
    [Authorize(Policy = Policies.ManageEmployees)]
    [RequestSizeLimit(ImportLimitBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportLimitBytes + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return View(new EmployeeImportResult(0, ["Choose the CSV file to import."]));
        if (file.Length > ImportLimitBytes) return View(new EmployeeImportResult(0, ["The file is larger than 2 MB. Import it in parts."]));
        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return View(new EmployeeImportResult(0, ["Save the spreadsheet as CSV (comma-separated, UTF-8) and import that file."]));
        await using var stream = file.OpenReadStream();
        var result = await employees.ImportAsync(stream, ct);
        if (result.Succeeded)
        {
            Success($"{result.Imported} employee{(result.Imported == 1 ? " was" : "s were")} imported.");
            return RedirectToAction(nameof(Index));
        }
        return View(result);
    }

    /// <summary>The CSV template, with the header row and one example line.</summary>
    [Authorize(Policy = Policies.ManageEmployees)]
    public IActionResult ImportTemplate() =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(EmployeeService.ImportTemplate())).ToArray(), "text/csv", "grsms-employee-import.csv");

    // ------------------------------------------------------------------ job details

    [Authorize(Policy = Policies.ManageEmployees)]
    public async Task<IActionResult> UpdateJob(int id, CancellationToken ct)
    {
        var form = await employees.GetUpdateFormAsync(id, ct);
        if (form is null) return NotFound();
        await LoadFormListsAsync(ct, form.GradeId);
        ViewBag.Employee = await employees.GetAsync(id, ct);
        return View(form);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageEmployees)]
    public async Task<IActionResult> UpdateJob(int id, UpdateJobForm form, CancellationToken ct)
    {
        form.EmployeeId = id;
        if (ModelState.IsValid)
        {
            var result = await employees.UpdateJobAsync(form, ct);
            if (result.Succeeded)
            {
                Success("The job details were updated and added to the job history.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await LoadFormListsAsync(ct, form.GradeId);
        ViewBag.Employee = await employees.GetAsync(id, ct);
        return View(form);
    }

    private async Task LoadFormListsAsync(CancellationToken ct, int? currentGradeId = null)
    {
        ViewBag.DepartmentList = new SelectList(await departments.GetGroupedLookupAsync(includeInactive: false, ct),
            nameof(GroupedLookupItem.Value), nameof(GroupedLookupItem.Text), null, nameof(GroupedLookupItem.Group));
        ViewBag.SiteList = new SelectList(await sites.GetActiveLookupAsync(ct), nameof(LookupItem.Value), nameof(LookupItem.Text));
        var gradeItems = (await grades.GetActiveLookupAsync(ct)).ToList();
        // Keep a grade that was deactivated since it was given, so saving the form does not silently remove it.
        if (currentGradeId is { } gid && gradeItems.All(g => g.Value != gid.ToString()) && await grades.GetForEditAsync(gid, ct) is { } old)
            gradeItems.Add(new LookupItem(gid.ToString(), $"{old.Code}: {old.Name} (inactive)"));
        ViewBag.GradeList = new SelectList(gradeItems, nameof(LookupItem.Value), nameof(LookupItem.Text));
        ViewBag.HasGrades = gradeItems.Count > 0 || currentGradeId is not null;
        ViewBag.StaffList = new SelectList(await users.GetActiveStaffAsync(null, ct), nameof(LookupItem.Value), nameof(LookupItem.Text));
    }
}
