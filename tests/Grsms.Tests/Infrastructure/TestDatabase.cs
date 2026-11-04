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
    public string? IpAddress { get; set; } = "10.0.0.1";
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

    public TestDatabase()
    {
        // An in-memory SQLite database lives only while this connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "unused" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddApplication();
        services.AddInfrastructure(config);
        UseSqlite(services, _connection);
        services.AddSingleton<ICurrentUserService>(CurrentUser);
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

    public void Dispose()
    {
        Services.Dispose();
        _connection.Dispose();
    }
}
