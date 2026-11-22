using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Grsms.Application.Auditing;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Auditing;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Web.Models;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Grsms.Web.Controllers;

/// <summary>
/// Everything to do with signing in and out:
///   * Sign in / sign out (password, or "Sign in with Microsoft 365" for staff)
///   * Candidate registration + email confirmation (public candidate portal)
///   * Forgot / reset password, change password
///
/// Every sign-in success, failure and lockout is written to the audit trail.
/// Messages never reveal whether an email address has an account (to stop people "fishing" for accounts).
/// </summary>
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditLogService audit,
    IEmailService email,
    ILogger<AccountController> logger,
    Grsms.Application.Settings.ISettingsStore settings) : Controller
{
    /// <summary>Internal name of the Microsoft 365 sign-in scheme (registered in Program.cs).</summary>
    public const string MicrosoftScheme = "Microsoft";

    // ================================================================= Sign in / out

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocal(returnUrl);

        // Remove any half-finished Microsoft sign-in so the page starts clean.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await LoadExternalProvidersAsync();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimits.SignIn)] // Phase 12
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        await LoadExternalProvidersAsync();
        if (!ModelState.IsValid) return View(model);

        // lockoutOnFailure: true → wrong passwords count towards the 5-attempt lockout.
        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        var user = await userManager.FindByEmailAsync(model.Email);

        if (result.Succeeded && user is not null)
        {
            await RecordSuccessfulSignInAsync(user, "password");
            return RedirectToLocal(model.ReturnUrl);
        }

        // 22 Nov 2026 (docs/41, H2): the password was right and two-step sign-in is on: ask for the code.
        if (result.RequiresTwoFactor)
            return RedirectToAction(nameof(LoginWith2fa), new { rememberMe = model.RememberMe, returnUrl = model.ReturnUrl });

        if (result.IsLockedOut)
        {
            await audit.LogAsync(AuditActions.LockedOut, user?.Id, model.Email);
            ModelState.AddModelError("", "This account is locked after too many wrong passwords. Try again in 15 minutes or ask HR to unlock it.");
            return View(model);
        }

        // Identity checks "may this account sign in?" (active, email confirmed) BEFORE the password, so "not allowed" says
        // nothing about the password. Only explain the reason when the password is right; otherwise answer exactly as for
        // a wrong password and count the failure, so nobody can find out whether an email has an account (Phase 12 review).
        if (result.IsNotAllowed && user is not null && !await userManager.CheckPasswordAsync(user, model.Password))
        {
            await userManager.AccessFailedAsync(user);
            await audit.LogAsync(AuditActions.SignInFailed, user.Id, model.Email);
            ModelState.AddModelError("", "Incorrect email or password.");
            return View(model);
        }

        if (result.IsNotAllowed && user is not null)
        {
            // The password was right but a rule blocked sign-in; say which rule.
            await audit.LogAsync(AuditActions.SignInFailed, user.Id, model.Email, "Sign-in not allowed (inactive or unconfirmed)");
            if (!user.IsActive)
                ModelState.AddModelError("", "This account has been deactivated. Please contact HR.");
            else
            {
                ViewBag.ShowResend = true;
                ModelState.AddModelError("", "Please confirm your email address first. Check your inbox for the link we sent.");
            }
            return View(model);
        }

        await audit.LogAsync(AuditActions.SignInFailed, user?.Id, model.Email);
        ModelState.AddModelError("", "Incorrect email or password.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = userManager.GetUserId(User);
        await audit.LogAsync(AuditActions.SignedOut, userId, User.Identity?.Name);
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ================================================================= Microsoft 365 (Entra ID)

    /// <summary>Step 1: send the browser to Microsoft's sign-in page.</summary>
    [AllowAnonymous]
    [HttpPost]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        // Phase 16: the method is always registered, but only used once System settings (or AzureAd) are complete.
        if (provider != MicrosoftScheme || !MicrosoftSignIn.IsConfigured(settings))
            return LoginError("Sign-in with Microsoft 365 is not available.");
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl });
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    /// <summary>
    /// Step 2: Microsoft sends the user back here.
    ///   * Known Microsoft account          → sign in.
    ///   * Unknown, but a staff account with
    ///     the same email exists             → link the two, then sign in.
    ///   * Completely new GREL staff member  → create an Employee account automatically
    ///                                         (only people in GREL's own Microsoft 365 tenant can get here).
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError is not null)
            return LoginError($"Microsoft sign-in failed: {remoteError}");

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return LoginError("Microsoft sign-in did not complete. Please try again.");

        var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        var user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        if (result.Succeeded && user is not null)
        {
            await RecordSuccessfulSignInAsync(user, "Microsoft 365");
            return RedirectToLocal(returnUrl);
        }
        if (result.IsLockedOut) return LoginError("This account is locked. Please contact HR.");
        if (result.IsNotAllowed) return LoginError("This account has been deactivated. Please contact HR.");

        // First Microsoft sign-in for this person: find or create their GRSMS account.
        var mail = info.Principal.FindFirstValue(ClaimTypes.Email)
                   ?? info.Principal.FindFirstValue("preferred_username");
        if (string.IsNullOrWhiteSpace(mail))
            return LoginError("Your Microsoft account did not provide an email address. Please contact IT.");

        user = await userManager.FindByEmailAsync(mail);
        if (user is { IsCandidate: true })
            return LoginError("This email is registered as a candidate account. Please sign in with your password.");

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = mail,
                Email = mail,
                EmailConfirmed = true,
                FullName = info.Principal.FindFirstValue("name") ?? mail,
                CreatedAt = DateTime.UtcNow
            };
            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
                return LoginError(string.Join(" ", created.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, Roles.Employee);
            logger.LogInformation("Created staff account {Email} from first Microsoft 365 sign-in", mail);
            // Every new account gets a welcome email (template "New account (Microsoft 365)").
            await HttpContext.RequestServices.GetRequiredService<Application.Common.WorkflowMailer>().TemplateToUserAsync(
                user.Id, Domain.Settings.EmailTemplateCatalogue.AccountCreatedMicrosoft,
                new Dictionary<string, string?> { ["SignInEmail"] = mail, ["Roles"] = Roles.Employee },
                Url.Action("Index", "Home", null, Request.Scheme)!, includeMe: true);
        }

        if (!user.IsActive) return LoginError("This account has been deactivated. Please contact HR.");

        await userManager.AddLoginAsync(user, info);
        await signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
        await RecordSuccessfulSignInAsync(user, "Microsoft 365 (first link)");
        return RedirectToLocal(returnUrl);
    }

    // ================================================================= Candidate registration

    /// <summary>True unless the Super Admin closed self-registration on System settings.</summary>
    private bool RegistrationOpen => settings.GetBool(Domain.Settings.SettingCatalogue.AllowSelfRegistration);

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() =>
        RegistrationOpen ? View(new RegisterViewModel()) : LoginError("New accounts are created by GREL's recruitment team. Please contact them for access.");

    [AllowAnonymous]
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimits.SignIn)] // Phase 12
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Checked on the post too, so a saved form or a script can't get round a closed registration.
        if (!RegistrationOpen) return LoginError("New accounts are created by GREL's recruitment team. Please contact them for access.");
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            PhoneNumber = model.PhoneNumber?.Trim(),
            IsCandidate = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            // Do not reveal that the email is already registered; send that person a note instead.
            if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                return RedirectToAction(nameof(RegisterConfirmation));

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        await userManager.AddToRoleAsync(user, Roles.Candidate);
        await SendConfirmationEmailAsync(user);
        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult RegisterConfirmation() => View();

    /// <summary>The link in the confirmation email lands here.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string code)
    {
        var user = await userManager.FindByIdAsync(userId);
        var ok = false;
        if (user is not null)
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            ok = (await userManager.ConfirmEmailAsync(user, decoded)).Succeeded;
        }
        ViewBag.Confirmed = ok;
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendConfirmation() => View(new EmailOnlyViewModel());

    [AllowAnonymous]
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimits.SignIn)] // Phase 12
    public async Task<IActionResult> ResendConfirmation(EmailOnlyViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is { EmailConfirmed: false, IsActive: true })
            await SendConfirmationEmailAsync(user);

        // Same message whether or not the account exists.
        return RedirectToAction(nameof(RegisterConfirmation));
    }

    // ================================================================= Forgot / reset password

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new EmailOnlyViewModel());

    [AllowAnonymous]
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimits.SignIn)] // Phase 12
    public async Task<IActionResult> ForgotPassword(EmailOnlyViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is { EmailConfirmed: true, IsActive: true })
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = Url.Action(nameof(ResetPassword), "Account", new { code, email = user.Email }, Request.Scheme)!;

            await email.SendAsync(user.Email!, "Reset your GREL Recruitment password",
                $"""
                <p>Dear {HtmlEncoder.Default.Encode(user.FullName)},</p>
                <p>We received a request to reset your password. Click the link below to choose a new one:</p>
                <p><a href="{HtmlEncoder.Default.Encode(link)}">Reset my password</a></p>
                <p>If you did not ask for this, you can ignore this email; your password will not change.</p>
                """);
        }

        // Always the same answer, so nobody can use this page to discover which emails have accounts.
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? code = null, string? email = null) =>
        code is null
            ? LoginError("The password reset link is incomplete. Please request a new one.")
            : View(new ResetPasswordViewModel { Code = code, Email = email ?? "" });

    [AllowAnonymous]
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimits.SignIn)] // Phase 12
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null) return RedirectToAction(nameof(ResetPasswordConfirmation)); // do not reveal

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code)); }
        catch (FormatException) { token = ""; }

        var result = await userManager.ResetPasswordAsync(user, token, model.Password);
        if (result.Succeeded)
        {
            if (user.MustChangePassword)
            {
                user.MustChangePassword = false; // they just chose their own password
                await userManager.UpdateAsync(user);
            }
            await userManager.SetLockoutEndDateAsync(user, null); // a successful reset also clears a lockout
            await audit.LogAsync(AuditActions.PasswordReset, user.Id, user.Email);
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Code == "InvalidToken"
                ? "This reset link has expired or was already used. Please request a new one."
                : error.Description);
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    // ================================================================= Change password (signed in)

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ChangePassword()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        // Microsoft 365 users without a GRSMS password manage their password at Microsoft.
        if (!await userManager.HasPasswordAsync(user))
        {
            TempData["Info"] = "You sign in with Microsoft 365, so your password is managed by GREL IT.";
            return RedirectToAction("Index", "Home");
        }
        return View(new ChangePasswordViewModel { IsForced = user.MustChangePassword });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        model.IsForced = user.MustChangePassword;
        if (!ModelState.IsValid) return View(model);

        if (model.NewPassword == model.CurrentPassword)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Choose a password different from the current one.");
            return View(model);
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Code == "PasswordMismatch" ? "The current password is not correct." : error.Description);
            return View(model);
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await audit.LogAsync(AuditActions.PasswordChanged, user.Id, user.Email);

        // Re-issue the sign-in cookie so it no longer carries the "must change password" flag.
        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Your password has been changed.";
        return RedirectToAction("Index", "Home");
    }

    // ================================================================= Two-step sign-in (22 Nov 2026, docs/41 H2)

    /// <summary>The second step: the password was right; now the code from the authenticator app (or a recovery code).</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl = null, bool recovery = false)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null) return RedirectToAction(nameof(Login));
        return View(new LoginWith2faViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl, UseRecoveryCode = recovery });
    }

    [AllowAnonymous]
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimits.SignIn)]
    public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null) return LoginError("Your sign-in took too long. Please sign in again.");
        if (!ModelState.IsValid) return View(model);

        var result = model.UseRecoveryCode
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(model.Code.Replace(" ", ""))
            : await signInManager.TwoFactorAuthenticatorSignInAsync(AuthenticatorCode(model.Code), model.RememberMe, rememberClient: false);

        if (result.Succeeded)
        {
            await RecordSuccessfulSignInAsync(user, model.UseRecoveryCode ? "password + recovery code" : "password + authenticator code");
            if (model.UseRecoveryCode && await userManager.CountRecoveryCodesAsync(user) <= 3)
                TempData["Info"] = "You have few recovery codes left. Make new ones under Two-step sign-in.";
            return RedirectToLocal(model.ReturnUrl);
        }
        if (result.IsLockedOut)
        {
            await audit.LogAsync(AuditActions.LockedOut, user.Id, user.Email, "Too many wrong two-step codes");
            return LoginError("This account is locked after too many wrong codes. Try again in 15 minutes or ask HR to unlock it.");
        }
        // Identity has already counted the wrong code towards the 5-attempt lockout, like a wrong password.
        await audit.LogAsync(AuditActions.SignInFailed, user.Id, user.Email, "Wrong two-step code");
        ModelState.AddModelError("", model.UseRecoveryCode
            ? "That recovery code is not right, or it was already used."
            : "That code is not right. Use the newest code in the app, and check that your phone's clock is correct.");
        return View(model);
    }

    /// <summary>The "Two-step sign-in" page: set it up (QR code), see how many recovery codes are left, make new ones.</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> TwoStep()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        if (!await userManager.HasPasswordAsync(user))
        {
            TempData["Info"] = "You sign in with Microsoft 365, so two-step sign-in is managed by Microsoft (GREL IT).";
            return RedirectToAction("Index", "Home");
        }
        return View(await TwoStepModelAsync(user));
    }

    /// <summary>Turns two-step sign-in on: the first code from the app proves it is set up. Shows the recovery codes once.</summary>
    [Authorize]
    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimits.SignIn)]
    public async Task<IActionResult> TwoStep(string? code)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        if (await userManager.GetTwoFactorEnabledAsync(user)) return RedirectToAction(nameof(TwoStep));

        if (string.IsNullOrWhiteSpace(code) || !await userManager.VerifyTwoFactorTokenAsync(user,
                userManager.Options.Tokens.AuthenticatorTokenProvider, AuthenticatorCode(code)))
        {
            ModelState.AddModelError("", "That code is not right. Scan the QR code again, then type the 6-digit code the app shows now.");
            return View(await TwoStepModelAsync(user));
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToList() ?? [];
        await audit.LogAsync(AuditActions.TwoStepEnabled, user.Id, user.Email);
        // This session has now passed the two-step check (turning it on changed the security stamp, so sign in again).
        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim(TwoStepRequiredFilter.AmrClaim, "mfa")]);
        var model = await TwoStepModelAsync(user, codes);
        return View(model);
    }

    /// <summary>New recovery codes (the old ones stop working), shown once.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RecoveryCodes()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        if (!await userManager.GetTwoFactorEnabledAsync(user)) return RedirectToAction(nameof(TwoStep));
        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToList() ?? [];
        await audit.LogAsync(AuditActions.RecoveryCodesRenewed, user.Id, user.Email);
        return View(nameof(TwoStep), await TwoStepModelAsync(user, codes));
    }

    /// <summary>Turns two-step sign-in off; only for people whose role does not require it.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> TurnOffTwoStep()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        if (TwoStepIsRequiredFor(User))
        {
            TempData["Error"] = "Your role must use two-step sign-in, so it cannot be turned off. If you changed phone, ask an administrator to reset it.";
            return RedirectToAction(nameof(TwoStep));
        }
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await audit.LogAsync(AuditActions.TwoStepDisabled, user.Id, user.Email);
        await signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Two-step sign-in is off.";
        return RedirectToAction(nameof(TwoStep));
    }

    private bool TwoStepIsRequiredFor(ClaimsPrincipal principal) =>
        settings.GetBool(Grsms.Domain.Settings.SettingCatalogue.RequireTwoStep) && Roles.TwoStepRequired.Any(principal.IsInRole);

    private async Task<TwoStepViewModel> TwoStepModelAsync(ApplicationUser user, IReadOnlyList<string>? newCodes = null)
    {
        var enabled = await userManager.GetTwoFactorEnabledAsync(user);
        var model = new TwoStepViewModel
        {
            Enabled = enabled,
            Required = TwoStepIsRequiredFor(User),
            MustSetUp = !enabled && TwoStepRequiredFilter.Required(User, settings),
            RecoveryCodesLeft = enabled ? await userManager.CountRecoveryCodesAsync(user) : 0,
            NewRecoveryCodes = newCodes
        };
        if (!enabled)
        {
            var key = await userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await userManager.ResetAuthenticatorKeyAsync(user);
                key = await userManager.GetAuthenticatorKeyAsync(user);
            }
            model.SharedKey = string.Join(" ", Enumerable.Range(0, (key!.Length + 3) / 4).Select(i => key.Substring(i * 4, Math.Min(4, key.Length - i * 4)))).ToLowerInvariant();
            var issuer = Uri.EscapeDataString("GREL GRSMS");
            var uri = $"otpauth://totp/{issuer}:{Uri.EscapeDataString(user.Email ?? user.UserName ?? "")}?secret={key}&issuer={issuer}&digits=6";
            using var qr = QRCoder.QRCodeGenerator.GenerateQrCode(uri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            model.QrDataUri = "data:image/png;base64," + Convert.ToBase64String(new QRCoder.PngByteQRCode(qr).GetGraphic(6));
        }
        return model;
    }

    /// <summary>"123 456" or "123-456" → "123456".</summary>
    private static string AuthenticatorCode(string code) => new(code.Where(char.IsDigit).ToArray());

    // ================================================================= Helpers

    /// <summary>Updates "last signed in" and writes the audit row.</summary>
    private async Task RecordSuccessfulSignInAsync(ApplicationUser user, string method)
    {
        user.LastSignInAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        await audit.LogAsync(AuditActions.SignedIn, user.Id, user.Email, $"Method: {method}");
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, code }, Request.Scheme)!;

        await email.SendAsync(user.Email!, "Confirm your GREL Careers account",
            $"""
            <p>Dear {HtmlEncoder.Default.Encode(user.FullName)},</p>
            <p>Thank you for registering on the GREL Careers portal. Please confirm your email address:</p>
            <p><a href="{HtmlEncoder.Default.Encode(link)}">Confirm my email</a></p>
            <p>If you did not register, you can ignore this email.</p>
            """);
    }

    /// <summary>Fills ViewBag.ExternalProviders so the login page shows "Sign in with Microsoft 365" only when configured.</summary>
    private async Task LoadExternalProvidersAsync() =>
        ViewBag.ExternalProviders = (await signInManager.GetExternalAuthenticationSchemesAsync())
            .Where(s => s.Name != MicrosoftScheme || MicrosoftSignIn.IsConfigured(settings)).ToList();

    /// <summary>Back to the login page with an error message.</summary>
    private IActionResult LoginError(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Only follow return addresses inside this site, so the login page cannot be used to send people to a fake site.</summary>
    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
}
