using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// Runs once when the application starts:
///   1. Optionally applies pending database migrations (setting "Database:ApplyMigrationsOnStartup").
///      In production a DBA usually runs the SQL scripts in /database instead, and this stays false.
///   2. Creates the FIRST HR Administrator account if none exists yet, using the "SeedAdmin" settings.
///      Without this, nobody could sign in to a brand-new installation.
///
/// Roles, departments and the Head Office site are created by the migration itself (seed data).
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));
        var db = sp.GetRequiredService<ApplicationDbContext>();

        if (config.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if ((await userManager.GetUsersInRoleAsync(Roles.HrAdministrator)).Count > 0)
            return; // already set up

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No HR Administrator exists and SeedAdmin:Email / SeedAdmin:Password are not set. " +
                "Set them (see docs/01-getting-started.md) and restart to create the first administrator.");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = config["SeedAdmin:FullName"] ?? "System Administrator",
            DepartmentId = 1, // Human Resources (seeded)
            SiteId = 1,       // Head Office (seeded)
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Could not create the first HR Administrator: {Errors}",
                string.Join(" ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, [Roles.HrAdministrator, Roles.Employee]);
        logger.LogInformation("Created the first HR Administrator account {Email}", email);
    }
}
