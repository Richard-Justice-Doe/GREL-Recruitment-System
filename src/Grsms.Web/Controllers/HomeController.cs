using System.Diagnostics;
using System.Globalization;
using Grsms.Application.Common;
using Grsms.Application.Dashboard;
using Grsms.Application.Organization;
using Grsms.Domain.Security;
using Grsms.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// The home page after sign-in: the staff dashboard (KPI cards, my tasks, vacancy closing calendar,
/// requests in progress, latest candidates). Candidates get their own simpler home page.
/// </summary>
public class HomeController(
    DashboardService dashboard,
    IAuthorizationService auth,
    AccessScope scope,
    IDivisionService divisionService,
    IDepartmentService departmentService,
    Application.Workforce.WorkforceDashboardService workforce) : AppController
{
    /// <param name="month">Month shown in the vacancy closing calendar, as "yyyy-MM" (default: this month).</param>
    /// <param name="divisionId">Optional: only count records of this division.</param>
    /// <param name="departmentId">Optional: only count records of this department.</param>
    public async Task<IActionResult> Index(string? month, int? divisionId, int? departmentId, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Candidate)) return View("CandidateHome");

        DateOnly? shownMonth = DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
            ? m : null;

        var access = new DashboardAccess(
            Requests: (await auth.AuthorizeAsync(User, Security.Policies.RecruitmentRequests)).Succeeded,
            Vacancies: (await auth.AuthorizeAsync(User, Security.Policies.Vacancies)).Succeeded,
            Candidates: scope.SeesAllDepartments);

        var filter = new DashboardFilter(divisionId, departmentId);
        var showFilter = access.Requests || access.Vacancies;

        return View(new HomeViewModel
        {
            Dashboard = await dashboard.GetAsync(access, shownMonth, filter, ct),
            Workforce = await workforce.GetAsync(ct), // Phase 21
            Filter = filter,
            DivisionList = showFilter
                ? new SelectList(await divisionService.GetAllLookupAsync(ct), nameof(LookupItem.Value), nameof(LookupItem.Text), divisionId)
                : null,
            DepartmentList = showFilter
                ? new SelectList(await departmentService.GetGroupedLookupAsync(includeInactive: false, ct),
                    nameof(GroupedLookupItem.Value), nameof(GroupedLookupItem.Text), departmentId, nameof(GroupedLookupItem.Group))
                : null
        });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
