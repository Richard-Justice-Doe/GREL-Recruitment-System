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
///   2. Otherwise refuses to start if the database is missing an update (Phase 13).
///   3. Gives permissions that are new in this version to their default roles, once (Phase 14).
///   4. Makes sure a Super Admin exists, using the "SeedAdmin" settings (until Phase 14 this created the first
///      HR Administrator). Without this, nobody could sign in to a brand-new installation.
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
        else if (db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true && // the migrations are written for MySQL
                 (await db.Database.GetPendingMigrationsAsync()).ToList() is { Count: > 0 } pending)
        {
            // Phase 13: the tables are older than this version of GRSMS. Stop now with a clear message, rather than start
            // and fail on the first page that needs a new table.
            logger.LogCritical("The database is missing {Count} update(s): {Migrations}. Run the matching scripts in the database folder " +
                               "(see docs/03-database.md) or set Database:ApplyMigrationsOnStartup to true, then start GRSMS again.",
                pending.Count, string.Join(", ", pending));
            throw new InvalidOperationException($"The database needs {pending.Count} update(s) before this version of GRSMS can start: {string.Join(", ", pending)}.");
        }

        await PermissionSeeder.ApplyNewDefaultsAsync(db, logger);
        await EnsureSuperAdminAsync(sp, config, logger);

        // Phase 28 (docs/30): an employee record for everyone activated before it existed (does nothing once they all have one).
        try
        {
            using var backfillScope = services.CreateScope();
            await backfillScope.ServiceProvider.GetRequiredService<Application.Employees.EmployeeLifecycle>().BackfillAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating the employee records of already-activated employees failed; GRSMS starts anyway and retries at the next start-up.");
        }
    }

    /// <summary>
    /// Makes sure at least one active Super Admin exists (Phase 14), using the SeedAdmin settings:
    ///   * a brand-new installation: creates the SeedAdmin account as Super Admin (and Employee);
    ///   * an installation from before Phase 14: the existing SeedAdmin account (until then the first HR Administrator)
    ///     is given the Super Admin role and keeps its other roles.
    /// Does nothing once an active Super Admin exists.
    /// </summary>
    private static async Task EnsureSuperAdminAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if ((await userManager.GetUsersInRoleAsync(Roles.SuperAdmin)).Any(u => u.IsActive))
            return; // already set up

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && await userManager.FindByEmailAsync(email) is { IsCandidate: false } existing)
        {
            await userManager.AddToRoleAsync(existing, Roles.SuperAdmin);
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                await userManager.UpdateAsync(existing);
            }
            logger.LogInformation("Gave the Super Admin role to the existing administrator account {Email}", email);
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No Super Admin exists and SeedAdmin:Email / SeedAdmin:Password are not set. " +
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
            logger.LogError("Could not create the first Super Admin: {Errors}",
                string.Join(" ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, [Roles.SuperAdmin, Roles.Employee]);
        logger.LogInformation("Created the first Super Admin account {Email}", email);
    }
}
