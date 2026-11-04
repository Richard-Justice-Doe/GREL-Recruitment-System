using Grsms.Application.Auditing;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Users;
using Grsms.Infrastructure.Auditing;
using Grsms.Infrastructure.Email;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Infrastructure;

/// <summary>
/// Registers the Infrastructure layer (database, repositories, email, user/audit services).
/// Called once from Program.cs: <c>builder.Services.AddInfrastructure(builder.Configuration);</c>
///
/// ASP.NET Identity itself (password rules, cookies) is configured in Program.cs because those
/// are web settings.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not set. See docs/01-getting-started.md, step 3.");

        // The audit interceptor is scoped because it needs the current user of THIS web request.
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) => options
            .UseMySQL(connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        // The DbContext is also the unit of work.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // One generic repository class for every entity type (IRepository<Department>, IRepository<Site>, ...).
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
