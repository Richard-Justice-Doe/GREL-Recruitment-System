using Grsms.Application.Applications;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// 12 November 2026 (docs/34 quick win): the talent pool, a tab under Candidates. Thin: the rules are in
/// <see cref="TalentPoolService"/>.
/// </summary>
[Authorize(Policy = Policies.Applications)]
public class TalentPoolController(TalentPoolService pool) : AppController
{
    public async Task<IActionResult> Index([FromQuery] TalentPoolFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        ViewBag.MayInvite = pool.MayInvite;
        ViewBag.Vacancies = await pool.InvitableVacanciesAsync(ct);
        return View(await pool.SearchAsync(filter, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Invite(string applicantUserId, int vacancyId, string? returnUrl, CancellationToken ct)
    {
        var result = await pool.InviteAsync(applicantUserId, vacancyId, ct);
        if (result.Succeeded) Success("Invitation sent: they have been emailed a link to the vacancy.");
        else Fail(result);
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }
}
