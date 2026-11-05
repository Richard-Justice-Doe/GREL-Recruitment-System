using Grsms.Application.Auditing;
using Grsms.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// "My audit trail" (5 November 2026): every signed-in user, staff and candidates alike, sees what they did and what
/// others did to their account. Read-only. The full trail stays on the Audit trail screen for those with that permission.
/// </summary>
[Authorize]
public class MyAuditTrailController(IAuditLogService audit, ICurrentUserService me) : AppController
{
    public async Task<IActionResult> Index([FromQuery] AuditLogQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        ViewBag.Me = me.UserName;
        return View(await audit.SearchMineAsync(me.UserId!, me.UserName, query, ct));
    }
}
