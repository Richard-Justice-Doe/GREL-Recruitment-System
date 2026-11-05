using Grsms.Application.Auditing;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Recruitment;
using Grsms.Application.Users;
using Grsms.Application.Vacancies;
using Grsms.Infrastructure.Storage;
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

        // MySQL. (The automated tests swap in SQLite themselves; see tests/Grsms.Tests/Infrastructure/TestDatabase.cs.)
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseMySQL(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        // The DbContext is also the unit of work.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // One generic repository class for every entity type (IRepository<Department>, IRepository<Site>, ...).
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        // Phase 14: roles and permissions. The store is one shared in-memory copy of who holds which permission.
        services.AddSingleton<IPermissionStore, PermissionStore>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddScoped<IEmailService, EmailService>();

        // Phase 10: the bell, reminder keys, and text messages (written to files until "Sms:Enabled" is true)
        services.AddScoped<Application.Notifications.INotificationCenter, Notifications.NotificationCenter>();
        services.Configure<Notifications.SmsOptions>(config.GetSection("Sms"));
        services.AddScoped<Application.Notifications.ISmsSender, Notifications.SmsSender>();

        // Phase 2: recruitment requests, reference numbers, file storage
        services.AddScoped<IRecruitmentRequestRepository, RecruitmentRequestRepository>();
        services.AddScoped<INumberGenerator, NumberGenerator>();
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.Configure<UploadOptions>(config.GetSection("Uploads"));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Phase 3: vacancies. The same repository also answers "is there a vacancy for this request?".
        services.AddScoped<IVacancyRepository, VacancyRepository>();
        services.AddScoped<Application.Adverts.IAdvertRepository, AdvertRepository>();
        services.AddScoped<Application.Applications.IApplicationRepository, ApplicationRepository>();
        services.AddScoped<Application.Interviews.IInterviewRepository, InterviewRepository>();
        services.AddScoped<IVacancyLookup>(sp => sp.GetRequiredService<IVacancyRepository>());

        return services;
    }
}
