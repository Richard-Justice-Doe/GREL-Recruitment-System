using Grsms.Application.Auditing;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// The read-only Audit Trail screen (Module 22). HR Administrator and HR Manager.
/// Filters are passed in the address (GET), so a filtered view can be bookmarked or shared.
/// </summary>
[Authorize(Policy = Policies.AuditTrail)]
public class AuditLogsController(IAuditLogService audit) : AppController
{
    public async Task<IActionResult> Index([FromQuery] AuditLogQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        ViewBag.EntityNames = await audit.GetEntityNamesAsync(ct);
        return View(await audit.SearchAsync(query, ct));
    }
}
