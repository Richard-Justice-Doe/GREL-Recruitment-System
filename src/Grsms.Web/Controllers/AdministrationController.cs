using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>One administration screen, as shown on the Administration page.</summary>
public record AdminLink(string Section, string Title, string Description, string Icon, string Controller, string Action);

/// <summary>
/// Usability U1 (10 November 2026): one Administration page instead of 13 menu links. It lists, in five sections, only the
/// screens the signed-in person may open (each screen still checks its own permission).
/// </summary>
[Authorize(Policy = Policies.Staff)]
public class AdministrationController(IAuthorizationService auth) : AppController
{
    private static readonly (string Policy, AdminLink Link)[] Screens =
    [
        (Policies.MasterData, new("Organisation", "Divisions", "Corporate and Operations, and which departments belong to each.", "fa-building", "Divisions", "Index")),
        (Policies.MasterData, new("Organisation", "Departments", "GREL's departments and each one's Head of Department.", "fa-sitemap", "Departments", "Index")),
        (Policies.MasterData, new("Organisation", "Units", "Units inside departments.", "fa-diagram-project", "Units", "Index")),
        (Policies.MasterData, new("Organisation", "Sites", "Estates, factories and offices.", "fa-location-dot", "Sites", "Index")),
        (Policies.MasterData, new("Onboarding set-up", "Induction areas", "Who owns each induction area and its tasks.", "fa-list-check", "InductionAreas", "Index")),
        (Policies.MasterData, new("Onboarding set-up", "Onboarding documents", "The documents new employees upload.", "fa-file-circle-check", "OnboardingDocuments", "Index")),
        (Policies.UserAdmin, new("People", "Users & roles", "Add staff accounts, reset passwords, unlock, deactivate.", "fa-users-gear", "Users", "Index")),
        (Policies.Grades, new("People", "Grades", "GREL's salary grades, given to employees on their record.", "fa-layer-group", "Grades", "Index")), // Phase 28
        (Policies.SystemSettings, new("People", "Roles & permissions", "Which roles may open which screens.", "fa-user-shield", "Roles", "Index")),
        (Policies.SystemSettings, new("System", "Screening (ATS) settings", "Score weights, bands and automatic-rejection rules.", "fa-sliders", "Ats", "Settings")),
        (Policies.SystemSettings, new("System", "System settings", "Email server, Microsoft 365 sign-in, workflow settings.", "fa-gear", "SystemSettings", "Index")),
        (Policies.SystemSettings, new("System", "Email templates", "The wording of the emails candidates receive.", "fa-envelope", "EmailTemplates", "Index")),
        (Policies.Backups, new("System", "Backups", "See, start and download backups.", "fa-box-archive", "Backups", "Index")),
        (Policies.AuditTrail, new("Audit", "Audit monitor", "Sign-in problems and configuration changes at a glance.", "fa-shield-heart", "AuditMonitor", "Index")),
        (Policies.AuditTrail, new("Audit", "Audit trail", "Every change and sign-in recorded by the system.", "fa-clock-rotate-left", "AuditLogs", "Index"))
    ];

    /// <summary>The screens this person may open (for the page and for the menu: no link when the list is empty).</summary>
    public static async Task<IReadOnlyList<AdminLink>> VisibleAsync(IAuthorizationService auth, System.Security.Claims.ClaimsPrincipal user)
    {
        var list = new List<AdminLink>();
        foreach (var (policy, link) in Screens)
            if ((await auth.AuthorizeAsync(user, policy)).Succeeded) list.Add(link);
        return list;
    }

    public async Task<IActionResult> Index()
    {
        var links = await VisibleAsync(auth, User);
        return links.Count == 0 ? Forbid() : View(links);
    }
}
