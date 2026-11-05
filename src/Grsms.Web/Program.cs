// =====================================================================================
//  GREL Recruitment & Selection Management System (GRSMS) - application start-up
//
//  This file wires everything together, in this order:
//    1. Logging (Serilog)
//    2. Application + Infrastructure layers (business services, MySQL, repositories)
//    3. Sign-in: ASP.NET Identity rules, cookie/session timeout, optional Microsoft 365
//    4. Authorization policies and global filters (anti-forgery, forced password change)
//    5. The HTTP pipeline (security headers, HTTPS, static files, routes)
//    6. Database start-up tasks (migrations, first administrator)
//  Every setting read here is listed in docs/06-configuration.md.
// =====================================================================================

using Grsms.Application;
using Grsms.Application.Common.Interfaces;
using Grsms.Infrastructure;
using Grsms.Infrastructure.Email;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Infrastructure.Storage;
using Grsms.Web.Controllers;
using Grsms.Web.Security;
using Grsms.Web.Workers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- 1. Logging
// Log levels and the console output come from the "Serilog" section of appsettings.json.
// The daily log file is added here so its folder ("Logs:Folder", default "logs") is always inside the
// application folder, whichever folder the app was started from.
builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.File(
        Path.Combine(
            Path.GetFullPath(context.Configuration["Logs:Folder"] ?? "logs", context.HostingEnvironment.ContentRootPath),
            "grsms-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

// ---------------------------------------------------------------- 2. Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<IAppLinks, AppLinks>();
builder.Services.AddScoped<Grsms.Web.Helpers.NavCounts>(); // sidebar badges + notification bell

// Background job: closes vacancies whose closing date has passed (see Workers/VacancyAutoCloseWorker.cs).
builder.Services.AddHostedService<VacancyAutoCloseWorker>();

// Relative folders for saved emails (used until SMTP is configured) and uploaded files are placed
// inside the application folder.
builder.Services.PostConfigure<EmailOptions>(o =>
    o.PickupDirectory = Path.GetFullPath(o.PickupDirectory, builder.Environment.ContentRootPath));
builder.Services.PostConfigure<Grsms.Infrastructure.Notifications.SmsOptions>(o =>
    o.PickupDirectory = Path.GetFullPath(o.PickupDirectory, builder.Environment.ContentRootPath)); // Phase 10: SMS saved to files while switched off
builder.Services.PostConfigure<StorageOptions>(o =>
    o.RootFolder = Path.GetFullPath(o.RootFolder, builder.Environment.ContentRootPath));

// Keys that protect cookies and password-reset links are saved to disk, so users are not signed
// out (and emailed links keep working) when the application restarts. The folder ("DataProtection:KeysFolder")
// holds secrets: keep it out of source control and readable only by the app's account.
builder.Services.AddDataProtection()
    .SetApplicationName("GRSMS")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(
        builder.Configuration["DataProtection:KeysFolder"] ?? "App_Data/keys", builder.Environment.ContentRootPath)));

// ---------------------------------------------------------------- 3. Sign-in (ASP.NET Core Identity)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password policy
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 4;

        // Account lockout: 5 wrong passwords lock the account for 15 minutes.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        // Every account needs its own email address. Candidates must confirm theirs before signing in;
        // staff accounts are created already confirmed.
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<AppSignInManager>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

// Session timeout: the sign-in cookie expires after 20 idle minutes ("sliding" = each request resets the clock).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Security:SessionTimeoutMinutes", 20));
    options.SlidingExpiration = true;
    options.Cookie.Name = "grsms.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Re-check each signed-in user against the database every 5 minutes, so deactivated users and
// role changes take effect quickly instead of when the cookie expires.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

// Optional "Sign in with Microsoft 365" (Entra ID). Phase 16: always registered, configured from System settings
// (falling back to the AzureAd section), and only offered once the tenant ID, client ID and secret are all set.
// See Security/MicrosoftSignIn.cs and docs/04-security-and-roles.md.
builder.Services.AddAuthentication().AddOpenIdConnect(AccountController.MicrosoftScheme, "Microsoft 365", _ => { });
builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>, MicrosoftSignIn>();

// ---------------------------------------------------------------- 4. Authorization + MVC
builder.Services.AddAuthorizationBuilder()
    .AddGrsmsPolicies()
    // Every page requires sign-in unless it is marked [AllowAnonymous] (login, register, ...).
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddControllersWithViews(options =>
{
    // Anti-forgery: every POST must carry the hidden token that <form> tags add automatically.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<MustChangePasswordFilter>();
});

// The anti-forgery cookie gets the same protection as the sign-in cookie.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "grsms.af";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

// Phase 12: rate limiting. The sign-in, registration, "resend confirmation", "forgot password" and "reset password"
// forms accept at most Security:SignInAttemptsPerMinute posts per minute from one network address (default 10).
// This slows password guessing across many accounts (account lockout only protects one account at a time) and
// stops the email-sending pages being used to flood mailboxes. Behind a reverse proxy, the real address comes from
// the forwarded headers set up in Phase 13.
builder.Services.AddRateLimiter(options =>
{
    var perMinute = Math.Max(1, builder.Configuration.GetValue("Security:SignInAttemptsPerMinute", 10));
    options.AddPolicy(RateLimits.SignIn, http => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = RateLimits.OnRejectedAsync;
});

// No "Server: Kestrel" header: there is no need to tell visitors what the site runs on.
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

// Phase 13: behind a reverse proxy (deploy/docker-compose.prod.yml uses Caddy), the proxy tells GRSMS the visitor's
// real address and that the original request was HTTPS. Those headers are ONLY believed when they come from the
// networks / addresses listed in ReverseProxy:KnownNetworks / KnownProxies; otherwise anyone could fake an address
// in the audit trail or dodge the sign-in rate limit.
var reverseProxy = builder.Configuration.GetValue("ReverseProxy:Enabled", false);
if (reverseProxy)
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        foreach (var cidr in (builder.Configuration["ReverseProxy:KnownNetworks"] ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
        foreach (var ip in (builder.Configuration["ReverseProxy:KnownProxies"] ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            o.KnownProxies.Add(System.Net.IPAddress.Parse(ip));
        if (o.KnownIPNetworks.Count == 0 && o.KnownProxies.Count == 0)
            throw new InvalidOperationException("ReverseProxy:Enabled is true, but neither ReverseProxy:KnownNetworks nor ReverseProxy:KnownProxies is set.");
    });
}

// Phase 13: "/health" answers Healthy (200) when the app can reach its database, Unhealthy (503) otherwise.
// For uptime monitoring; it shows no details.
builder.Services.AddHealthChecks().AddCheck<Grsms.Web.Helpers.DatabaseHealthCheck>("database");

var app = builder.Build();

// Phase 16: when the Super Admin saves System settings, the Microsoft 365 sign-in rebuilds its options on next use.
app.Services.GetRequiredService<Grsms.Application.Settings.ISettingsStore>().Changed += () =>
    app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitorCache<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>>()
        .TryRemove(AccountController.MicrosoftScheme);

// ---------------------------------------------------------------- 5. HTTP pipeline
// First, so every later step (HTTPS redirect, secure cookies, audit IP, rate limit) sees the real visitor (Phase 13).
if (reverseProxy) app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // tell browsers to always use HTTPS for this site
}

// Security headers on every response, including the Content-Security-Policy (Security/SecurityHeaders.cs, Phase 12).
app.UseGrsmsSecurityHeaders();

app.UseHttpsRedirection();
// One log line per request, with timing. It logs the path only, never the query string, so tokens in emailed links
// (password reset, email confirmation) never reach the log files.
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseRateLimiter(); // Phase 12: limits on the sign-in, registration and password pages (see section 4)
app.UseAuthentication();
app.UseAuthorization();

// CSS, JavaScript, fonts and images are public, so the sign-in page itself is styled.
// (Without this, the "sign-in required" fallback policy would block them too.)
app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ---------------------------------------------------------------- 6. Database start-up
await DbInitializer.InitializeAsync(app.Services, app.Configuration);

app.Run();

/// <summary>Makes the start-up class visible to the automated tests (tests/Grsms.Tests), which start the whole site in memory.</summary>
public partial class Program;
