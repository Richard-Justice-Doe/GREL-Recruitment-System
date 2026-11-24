using Grsms.Application.Celebrations;
using Grsms.Application.Common;
using Grsms.Application.Employees;
using Grsms.Application.Users;
using Grsms.Application.Organization;
using Grsms.Application.Settings;
using Grsms.Domain.Celebrations;
using Grsms.Domain.Settings;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// 17 November 2026: birthdays and work anniversaries for GREL staff: today's celebrations with colleagues' messages, the next seven
/// days, the month calendar (filters), and HR's settings and moderation. Every rule is in <see cref="CelebrationService"/>.
/// </summary>
[Authorize(Policy = Policies.Staff)]
public class CelebrationsController(CelebrationService celebrations, EmployeePhotoService photos, ISettingsStore store, ISiteService sites,
    IAuthorizationService auth) : AppController
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await celebrations.OverviewAsync(Today, ct);
        var feed = new Dictionary<(int, CelebrationKind), IReadOnlyList<CelebrationMessageDto>>();
        foreach (var c in model.Today) feed[(c.EmployeeId, c.Kind)] = await celebrations.MessagesAsync(c.EmployeeId, c.Kind, c.Date.Year, ct);
        ViewBag.Feed = feed;
        ViewBag.Mine = await celebrations.MineAsync(ct); // 22 Nov 2026: "Add your birthday"
        ViewBag.CanManage = (await auth.AuthorizeAsync(User, Policies.ManageEmployees)).Succeeded;
        return View(model);
    }

    /// <summary>The month calendar, optionally for one department or site.</summary>
    public async Task<IActionResult> Calendar(int? year, int? month, int? departmentId, int? siteId, CancellationToken ct)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : Today.Month;
        ViewBag.Year = y;
        ViewBag.Month = m;
        ViewBag.SiteId = siteId;
        ViewBag.DepartmentId = departmentId;
        ViewBag.SiteFilterList = new SelectList(await sites.GetActiveLookupAsync(ct), nameof(LookupItem.Value), nameof(LookupItem.Text), siteId?.ToString());
        await LoadFilterListsAsync(null, departmentId, ct);
        return View(await celebrations.MonthAsync(y, m, departmentId, siteId, ct));
    }

    /// <summary>A celebrated colleague's photo (current staff who have not opted out); 404 = initials.</summary>
    public async Task<IActionResult> Photo(int id, CancellationToken ct)
    {
        var file = await photos.OpenForCelebrationAsync(id, ct);
        if (file is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(file.Content, file.ContentType);
    }

    [HttpPost]
    public Task<IActionResult> Post(int employeeId, CelebrationKind kind, string? text, CancellationToken ct) =>
        RunAsync(() => celebrations.PostAsync(employeeId, kind, text, Today, ct), "Your message was added.",
            () => Redirect(Url.Action(nameof(Index))! + $"#c-{employeeId}-{kind}"));

    [HttpPost]
    [Authorize(Policy = Policies.ManageEmployees)]
    public Task<IActionResult> Hide(int id, bool hidden, CancellationToken ct) =>
        RunAsync(() => celebrations.SetHiddenAsync(id, hidden, ct), hidden ? "Message hidden." : "Message shown again.", () => RedirectToAction(nameof(Index)));

    /// <summary>22 Nov 2026: the employee adds (or removes) their own date of birth, for birthdays.</summary>
    [HttpPost]
    public Task<IActionResult> MyBirthday(DateOnly? dateOfBirth, CancellationToken ct) =>
        RunAsync(() => celebrations.SetMineAsync(dateOfBirth, ct),
            dateOfBirth is null ? "Your birthday was removed." : "Thank you: colleagues will see your birthday (the day and month only).",
            () => RedirectToAction(nameof(Index)));

    /// <summary>22 Nov 2026: HR enters an employee's date of birth when there is no personal record (from the employee profile).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageEmployees)]
    public Task<IActionResult> Birthday(int employeeId, DateOnly? dateOfBirth, CancellationToken ct) =>
        RunAsync(() => celebrations.SetForAsync(employeeId, dateOfBirth, ct),
            dateOfBirth is null ? "Date of birth removed." : "Date of birth saved. Their birthday now shows in celebrations.",
            () => RedirectToAction("Details", "Employees", new { id = employeeId }));

    /// <summary>HR records that the employee does not want their celebrations shown (from the employee profile).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageEmployees)]
    public Task<IActionResult> OptOut(int employeeId, bool optOut, CancellationToken ct) =>
        RunAsync(() => celebrations.SetOptOutAsync(employeeId, optOut, ct),
            optOut ? "Their birthday and work anniversary will no longer be shown or announced." : "Their birthday and work anniversary will be celebrated again.",
            () => RedirectToAction("Details", "Employees", new { id = employeeId }));

    // ------------------------------------------------------------------ HR's settings

    [Authorize(Policy = Policies.ManageEmployees)]
    public IActionResult Settings() =>
        View(SettingCatalogue.All.Where(d => d.Group == SettingCatalogue.CelebrationsGroup)
            .Select(d => (Definition: d, Value: store.Get(d.Key) ?? d.Default)).ToList());

    [HttpPost]
    [Authorize(Policy = Policies.ManageEmployees)]
    public async Task<IActionResult> Settings(IFormCollection form, CancellationToken ct)
    {
        var values = new Dictionary<string, string?>();
        foreach (var d in SettingCatalogue.All.Where(d => d.Group == SettingCatalogue.CelebrationsGroup))
        {
            var raw = form[d.Key].ToString().Trim();
            if (d.Kind == SettingKind.Switch) values[d.Key] = raw.Contains("true") ? "true" : "false";
            else if (d.Kind == SettingKind.Number)
            {
                if (!int.TryParse(raw, out var n) || n < d.Min || n > d.Max)
                {
                    Error($"{d.Label}: enter a whole number from {d.Min} to {d.Max}.");
                    return RedirectToAction(nameof(Settings));
                }
                values[d.Key] = n.ToString();
            }
        }
        await store.SaveAsync(values, ct);
        Success("Celebration settings saved.");
        return RedirectToAction(nameof(Settings));
    }
}
