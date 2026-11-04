using Grsms.Application.Common.Interfaces;
using Grsms.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>The bell (Module 10): everyone's own notifications, newest first. Opening one marks it read and follows its link.</summary>
[Authorize]
public class NotificationsController(INotificationCenter notifications, ICurrentUserService me) : AppController
{
    private const int Shown = 100;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await notifications.ListAsync(me.UserId!, Shown, ct));

    /// <summary>Marks the notification read and goes to its page. Links are stored as paths inside GRSMS only.</summary>
    public async Task<IActionResult> Open(long id, CancellationToken ct)
    {
        var link = await notifications.OpenAsync(me.UserId!, id, ct);
        return link is not null && Url.IsLocalUrl(link) ? LocalRedirect(link) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(me.UserId!, ct);
        Success("All notifications marked as read.");
        return RedirectToAction(nameof(Index));
    }
}
