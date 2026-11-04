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
using Grsms.Web.Controllers;
using Grsms.Web.Security;
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

// A relative email pickup folder (used until SMTP is configured) is placed inside the application folder.
builder.Services.PostConfigure<EmailOptions>(o =>
    o.PickupDirectory = Path.GetFullPath(o.PickupDirectory, builder.Environment.ContentRootPath));

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

// Optional "Sign in with Microsoft 365" (Entra ID). Switched on only when IT has supplied all three
// values in the AzureAd section; until then the button is simply not shown. See docs/04-security-and-roles.md.
var azureAd = builder.Configuration.GetSection("AzureAd");
if (!string.IsNullOrWhiteSpace(azureAd["TenantId"]) &&
    !string.IsNullOrWhiteSpace(azureAd["ClientId"]) &&
    !string.IsNullOrWhiteSpace(azureAd["ClientSecret"]))
{
    builder.Services.AddAuthentication().AddOpenIdConnect(AccountController.MicrosoftScheme, "Microsoft 365", options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme; // hand the result to Identity
        options.Authority = $"https://login.microsoftonline.com/{azureAd["TenantId"]}/v2.0";
        options.ClientId = azureAd["ClientId"];
        options.ClientSecret = azureAd["ClientSecret"];
        options.ResponseType = "code";
        options.CallbackPath = azureAd["CallbackPath"] ?? "/signin-oidc";
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
}

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

var app = builder.Build();

// ---------------------------------------------------------------- 5. HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // tell browsers to always use HTTPS for this site
}

// Basic security headers on every response. A full Content-Security-Policy is added in Phase 12.
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseHttpsRedirection();
app.UseSerilogRequestLogging(); // one log line per request, with timing
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ---------------------------------------------------------------- 6. Database start-up
await DbInitializer.InitializeAsync(app.Services, app.Configuration);

app.Run();

/// <summary>Makes the start-up class visible to the automated tests (tests/Grsms.Tests), which start the whole site in memory.</summary>
public partial class Program;
