using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>Phase 16: Audit monitor: sign-in problems, configuration changes and activity at a glance (audit-trail readers).</summary>
[Authorize(Policy = Policies.AuditTrail)]
public class AuditMonitorController(Application.Auditing.IAuditLogService audit) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await audit.MonitorAsync(DateTime.UtcNow, ct));
}
