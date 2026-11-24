using Grsms.Application.Dashboard;
using Grsms.Application.Process;
using Grsms.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// 13 November 2026 (docs/37 CR5, step C3): the Action Center: every pending action of the signed-in person, with its priority
/// and due date. Staff see their work across all modules; candidates and new employees see their next steps.
/// </summary>
[Authorize]
public class ActionCenterController(ActionCenterService actions, IAuthorizationService auth) : AppController
{
    /// <param name="priority">Optional filter: High, Medium or Low.</param>
    public async Task<IActionResult> Index(ActionPriority? priority, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Candidate)) return View("Candidate");
        var access = new DashboardAccess(
            Requests: (await auth.AuthorizeAsync(User, Security.Policies.RecruitmentRequests)).Succeeded,
            Vacancies: (await auth.AuthorizeAsync(User, Security.Policies.Vacancies)).Succeeded,
            Candidates: false);
        ViewBag.Priority = priority;
        return View(await actions.GetAsync(access, ct));
    }
}
