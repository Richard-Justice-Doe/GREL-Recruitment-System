using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Domain.Security;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// User account screens (permission "Manage user accounts": the Super Admin and, by default, the HR Administrator):
/// list staff and candidates, create staff accounts, edit roles, deactivate, unlock, and reset passwords.
/// Phase 14: privileged roles and accounts can only be changed by a Super Admin (enforced in UserAdminService).
/// </summary>
[Authorize(Policy = Policies.UserAdmin)]
public class UsersController(
    IUserAdminService users,
    IDepartmentService departments,
    ISiteService sites,
    IPasswordValidator<Infrastructure.Identity.ApplicationUser> passwordValidator,
    UserManager<Infrastructure.Identity.ApplicationUser> userManager,
    Application.Common.WorkflowMailer mailer) : AppController
{
    /// <param name="candidates">True to show candidate (portal) accounts instead of staff.</param>
    public async Task<IActionResult> Index(bool candidates, CancellationToken ct)
    {
        ViewBag.ShowCandidates = candidates;
        var all = await users.GetAllAsync(includeCandidates: true, ct);
        return View(all.Where(u => u.IsCandidate == candidates).ToList());
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadListsAsync(ct);
        return View("Form", new UserFormDto { SelectedRoles = [Roles.Employee] });
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserFormDto form, CancellationToken ct)
    {
        await CheckPasswordStrengthAsync(form);
        if (ModelState.IsValid)
        {
            var result = await users.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                await SendWelcomeAsync(result.Value!, form, ct);
                Success($"Account for {form.FullName} was created and {form.Email} has been emailed a link to choose their password. " +
                        "The temporary password also works; they must change it at first sign-in.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        await LoadListsAsync(ct);
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var form = await users.GetForEditAsync(id, ct);
        if (form is null) return NotFound();
        await LoadListsAsync(ct);
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, UserFormDto form, CancellationToken ct)
    {
        form.Id = id;
        await CheckPasswordStrengthAsync(form);
        if (ModelState.IsValid)
        {
            var result = await users.UpdateAsync(form, CurrentUserId, ct);
            if (result.Succeeded)
            {
                Success($"Account for {form.FullName} was saved.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        await LoadListsAsync(ct);
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(string id, bool active, bool candidates, CancellationToken ct)
    {
        var result = await users.SetActiveAsync(id, active, CurrentUserId, ct);
        if (result.Succeeded) Success(active ? "Account activated." : "Account deactivated. The user will be signed out within 5 minutes.");
        else Fail(result);
        return RedirectToAction(nameof(Index), new { candidates });
    }

    [HttpPost]
    public async Task<IActionResult> Unlock(string id, bool candidates, CancellationToken ct)
    {
        var result = await users.UnlockAsync(id, ct);
        if (result.Succeeded) Success("Account unlocked.");
        else Fail(result);
        return RedirectToAction(nameof(Index), new { candidates });
    }

    private string CurrentUserId => userManager.GetUserId(User)!;

    /// <summary>
    /// Every new staff account gets a welcome email (template "New staff account"): how to sign in, their roles, and a
    /// link to choose their own password (the same 24-hour link as "Forgot password"). The temporary password is never
    /// emailed. A mail problem is logged by the mailer and never undoes the account.
    /// </summary>
    private async Task SendWelcomeAsync(string userId, UserFormDto form, CancellationToken ct)
    {
        if (await ChoosePasswordLinkAsync(userId) is not { } choose) return;
        var (email, link) = choose;
        await mailer.TemplateToUserAsync(userId, Domain.Settings.EmailTemplateCatalogue.AccountCreated, new Dictionary<string, string?>
        {
            ["SignInEmail"] = email,
            ["Roles"] = string.Join(", ", form.SelectedRoles),
            ["CreatedBy"] = User.FindFirst(AppClaimsPrincipalFactory.FullNameClaim)?.Value ?? User.Identity?.Name
        }, link, ct: ct);
    }

    /// <summary>The 24-hour "choose your password" link (the same as "Forgot password") for a new account.</summary>
    private async Task<(string? Email, string Link)?> ChoosePasswordLinkAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var code = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
        return (user.Email, Url.Action("ResetPassword", "Account", new { code, email = user.Email }, Request.Scheme)!);
    }

    // ================================================================= Candidate accounts (5 November 2026)

    /// <summary>Add a candidate account for someone (useful while self-registration is closed).</summary>
    public IActionResult CreateCandidate() => View();

    [HttpPost]
    public async Task<IActionResult> CreateCandidate(string? fullName, string? email, CancellationToken ct)
    {
        var result = await users.CreateCandidateAsync(fullName ?? "", email ?? "", ct);
        if (!result.Succeeded)
        {
            AddErrors(result);
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            return View();
        }
        if (await ChoosePasswordLinkAsync(result.Value!) is { } choose)
            await mailer.TemplateToUserAsync(result.Value!, Domain.Settings.EmailTemplateCatalogue.AccountCreatedCandidate,
                new Dictionary<string, string?> { ["SignInEmail"] = choose.Email }, choose.Link, ct: ct);
        Success($"Candidate account for {fullName} was created and {email} has been emailed a link to choose their password.");
        return RedirectToAction(nameof(Index), new { candidates = true });
    }

    /// <summary>
    /// Checks the temporary password against the password policy up front, so the administrator sees
    /// "needs a digit" etc. next to the field instead of after the account is half-saved.
    /// </summary>
    private async Task CheckPasswordStrengthAsync(UserFormDto form)
    {
        if (string.IsNullOrEmpty(form.TemporaryPassword)) return;
        var check = await passwordValidator.ValidateAsync(userManager, new(), form.TemporaryPassword);
        foreach (var e in check.Errors)
            ModelState.AddModelError(nameof(form.TemporaryPassword), e.Description);
    }

    /// <summary>Drop-downs and role tick-boxes for the form.</summary>
    private async Task LoadListsAsync(CancellationToken ct)
    {
        ViewBag.DepartmentList = new SelectList(await departments.GetActiveLookupAsync(ct), nameof(LookupItem.Value), nameof(LookupItem.Text));
        ViewBag.SiteList = new SelectList(await sites.GetActiveLookupAsync(ct), nameof(LookupItem.Value), nameof(LookupItem.Text));
        ViewBag.AllRoles = await users.GetAssignableRolesAsync(ct);
        // Phase 14: only a Super Admin may tick roles other than Employee / Interview Panel Member (checked again by the service).
        ViewBag.IsSuperAdmin = User.IsInRole(Roles.SuperAdmin);
    }
}
