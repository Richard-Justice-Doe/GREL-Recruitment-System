using Grsms.Application.Settings;
using Grsms.Domain.Settings;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grsms.Web.Security;

/// <summary>
/// "Sign in with Microsoft 365" (Entra ID), configured from System settings (Phase 16) with the server configuration
/// ("AzureAd" section) as the fallback.
///
/// The sign-in method is always registered, but it is only OFFERED (button on the sign-in page) and only ACCEPTED
/// (AccountController.ExternalLogin) once the tenant ID, client ID and client secret are all set. Its options are built
/// from the settings in force; when a Super Admin saves new values, the cached options are dropped (see Program.cs),
/// so the next sign-in uses them without restarting GRSMS.
/// </summary>
public sealed class MicrosoftSignIn(ISettingsStore settings, IConfiguration config) : IConfigureNamedOptions<OpenIdConnectOptions>
{
    public static bool IsConfigured(ISettingsStore s) =>
        !string.IsNullOrWhiteSpace(s.Get(SettingCatalogue.AzureTenant)) &&
        !string.IsNullOrWhiteSpace(s.Get(SettingCatalogue.AzureClient)) &&
        !string.IsNullOrWhiteSpace(s.Get(SettingCatalogue.AzureSecret));

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (name != Controllers.AccountController.MicrosoftScheme) return;
        options.SignInScheme = IdentityConstants.ExternalScheme; // hand the result to Identity
        options.ResponseType = "code";
        options.CallbackPath = config["AzureAd:CallbackPath"] ?? "/signin-oidc";
        options.Scope.Add("email");
        options.Scope.Add("profile");

        // Until it is configured, harmless placeholders keep the handler valid; nothing is offered or accepted anyway.
        var tenant = settings.Get(SettingCatalogue.AzureTenant);
        options.Authority = $"https://login.microsoftonline.com/{(string.IsNullOrWhiteSpace(tenant) ? "common" : tenant.Trim())}/v2.0";
        options.ClientId = settings.Get(SettingCatalogue.AzureClient) is { Length: > 0 } id ? id.Trim() : "not-configured";
        options.ClientSecret = settings.Get(SettingCatalogue.AzureSecret);
    }

    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);
}
