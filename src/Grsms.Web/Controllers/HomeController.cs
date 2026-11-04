using System.Diagnostics;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Users;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;
using Grsms.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// The home page after sign-in. In Phase 1 it shows organisation counts and the build roadmap;
/// the full recruitment dashboard (Module 1) replaces it in Phase 11.
/// </summary>
public class HomeController(
    IRepository<Department> departments,
    IRepository<Unit> units,
    IRepository<Site> sites,
    IUserAdminService users) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Candidates have their own (simpler) home page.
        if (User.IsInRole(Roles.Candidate)) return View("CandidateHome");

        var allUsers = await users.GetAllAsync(includeCandidates: true, ct);
        var model = new HomeViewModel
        {
            ActiveDepartments = await departments.CountAsync(d => d.IsActive, ct),
            Units = await units.CountAsync(u => u.IsActive, ct),
            Sites = await sites.CountAsync(s => s.IsActive, ct),
            StaffAccounts = allUsers.Count(u => !u.IsCandidate && u.IsActive),
            CandidateAccounts = allUsers.Count(u => u.IsCandidate)
        };
        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
