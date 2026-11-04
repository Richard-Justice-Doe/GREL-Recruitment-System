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
///   3. Demo mode only (Database:Provider = Sqlite, Development environment, Demo:SeedSampleData = true):
///      creates the demo database file and fills it with sample users and records (DemoDataSeeder).
///
/// Roles, departments and the Head Office site are created by the migration itself (seed data).
/// </summary>
public static class DbInitializer
{
    /// <param name="isDevelopment">Demo sample data is only ever created in the Development environment.</param>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, bool isDevelopment = false)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));
        var db = sp.GetRequiredService<ApplicationDbContext>();

        if (DependencyInjection.IsSqlite(config))
        {
            // Demo mode: the migrations are written for MySQL, so the SQLite file is created straight from the model.
            logger.LogWarning("DEMO MODE: using the local SQLite database file instead of MySQL. Do not use this for real data.");
            await db.Database.EnsureCreatedAsync();
        }
        else if (config.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
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

        await CreateFirstAdministratorAsync(sp, config, logger);

        if (isDevelopment && config.GetValue<bool>("Demo:SeedSampleData"))
            await DemoDataSeeder.SeedAsync(sp, config, logger);
    }

    /// <summary>Creates the first HR Administrator from the SeedAdmin settings, if none exists yet.</summary>
    private static async Task CreateFirstAdministratorAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
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
