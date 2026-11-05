using Grsms.Application;
using Grsms.Application.Common.Interfaces;
using Grsms.Infrastructure;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grsms.Tests.Infrastructure;

/// <summary>
/// Pretends to be the signed-in user during service tests, so CreatedBy/UpdatedBy and the
/// audit trail have someone to record.
/// </summary>
public class TestCurrentUser : ICurrentUserService
{
    public string? UserId { get; set; } = "test-user-id";
    public string? UserName { get; set; } = "tester@grel.test";
    public string? FullName { get; set; } = "Test User";
    public string? IpAddress { get; set; } = "10.0.0.1";

    /// <summary>The roles the pretend user holds. Tests change this to act as different people.</summary>
    public HashSet<string> Roles { get; set; } = [];

    public bool IsInRole(string role) => Roles.Contains(role);

    /// <summary>Service tests use each role's default permissions (the web tests use the real database grants).</summary>
    public bool HasPermission(string permission) =>
        UserId is not null && Grsms.Domain.Security.Permissions.Holds(Roles, permission,
            r => Grsms.Domain.Security.Permissions.Defaults.GetValueOrDefault(r) ?? []);

    /// <summary>Switches the pretend signed-in user (id, name and roles) in one call.</summary>
    public void SignInAs(string userId, string name, params string[] roles)
    {
        UserId = userId;
        UserName = name;
        FullName = name;
        Roles = [.. roles];
    }
}

/// <summary>
/// Builds the real Application + Infrastructure services, but with an in-memory SQLite database
/// instead of MySQL, so tests run anywhere in seconds without a database server.
/// Everything else (repositories, audit interceptor, Identity, business rules) is the production code.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    public ServiceProvider Services { get; }
    public TestCurrentUser CurrentUser { get; } = new();

    /// <summary>Temporary folder for this test's uploaded files and saved emails; deleted afterwards.</summary>
    public string TempFolder { get; } = Path.Combine(Path.GetTempPath(), "grsms-test-" + Guid.NewGuid().ToString("N"));

    public string MailFolder => Path.Combine(TempFolder, "mail");

    public TestDatabase(int maxUploadMb = 10)
    {
        // An in-memory SQLite database lives only while this connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "unused",
                ["Storage:RootFolder"] = Path.Combine(TempFolder, "uploads"),
                ["Email:PickupDirectory"] = MailFolder,
                ["Sms:PickupDirectory"] = Path.Combine(TempFolder, "sms"),
                ["Uploads:MaxFileSizeMb"] = maxUploadMb.ToString(),
                ["Email:FromName"] = "GREL Recruitment (tests)", // Phase 16: a server-configuration value, to test the fallback
                ["Backups:Folder"] = Path.Combine(TempFolder, "backups")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddApplication();
        services.AddInfrastructure(config);
        UseSqlite(services, _connection);
        services.AddSingleton<ICurrentUserService>(CurrentUser);
        services.AddSingleton<IAppLinks, TestAppLinks>();
        services.AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        Services = services.BuildServiceProvider();
        CreateSchema(_connection);
    }

    /// <summary>Swaps the MySQL registration from AddInfrastructure for SQLite (keeping the audit interceptor).</summary>
    public static void UseSqlite(IServiceCollection services, SqliteConnection connection)
    {
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>((sp, o) => o
            .UseSqlite(connection)
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
    }

    /// <summary>Creates the tables and seed rows (roles, departments, Head Office) from the EF model.</summary>
    public static void CreateSchema(SqliteConnection connection)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
    }

    /// <summary>A fresh scope = one simulated web request (new DbContext, new interceptor).</summary>
    public IServiceScope NewScope() => Services.CreateScope();

    /// <summary>Creates an active, email-confirmed user with the given roles and returns their id.</summary>
    public async Task<string> CreateUserAsync(string email, int? departmentId, params string[] roles)
    {
        using var scope = NewScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FullName = email.Split('@')[0], DepartmentId = departmentId, CreatedAt = DateTime.UtcNow
        };
        var created = await userManager.CreateAsync(user, "Test#Pass2026");
        if (!created.Succeeded) throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));
        await userManager.AddToRolesAsync(user, roles);
        return user.Id;
    }

    /// <summary>The .eml files saved so far (emails are written to files in tests).</summary>
    public IReadOnlyList<string> SentEmails() =>
        Directory.Exists(MailFolder) ? Directory.GetFiles(MailFolder, "*.eml").Select(File.ReadAllText).ToList() : [];

    /// <summary>The text messages saved so far (SMS is switched off in tests, so they are written to files).</summary>
    public IReadOnlyList<string> SentTexts()
    {
        var folder = Path.Combine(TempFolder, "sms");
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.txt").Select(File.ReadAllText).ToList() : [];
    }

    public void Dispose()
    {
        Services.Dispose();
        _connection.Dispose();
        try { if (Directory.Exists(TempFolder)) Directory.Delete(TempFolder, recursive: true); }
        catch (IOException) { /* a file still open on a slow machine; the OS temp cleaner will remove it */ }
    }
}

/// <summary>Email links in tests point at a fixed fake address.</summary>
public class TestAppLinks : IAppLinks
{
    public string RecruitmentRequest(int id) => $"https://grsms.test/RecruitmentRequests/Details/{id}";
    public string Vacancy(int id) => $"https://grsms.test/Vacancies/Details/{id}";
    public string Advert(int id) => $"https://grsms.test/Adverts/Details/{id}";
    public string Application(int id) => $"https://grsms.test/Applications/Details/{id}";
    public string MyApplication(int id) => $"https://grsms.test/MyApplications/Details/{id}";
    public string Shortlist(int vacancyId) => $"https://grsms.test/Shortlisting/Vacancy/{vacancyId}";
    public string AtsVacancy(int vacancyId) => $"https://grsms.test/Ats/Vacancy/{vacancyId}";
    public string Interview(int id) => $"https://grsms.test/Interviews/Details/{id}";
    public string HiringCase(int applicationId) => $"https://grsms.test/Hiring/Case/{applicationId}";
    public string Onboarding(int id) => $"https://grsms.test/Onboarding/Details/{id}";
    public string Survey(int invitationId) => $"https://grsms.test/Surveys/Take/{invitationId}";
    public string CareersJob(string advertNumber) => $"https://grsms.test/Careers/Details/{advertNumber}";
}
