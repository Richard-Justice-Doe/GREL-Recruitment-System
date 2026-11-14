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
        services.AddSingleton<SlowQueryInterceptor>(); // docs/32: logs database commands slower than Database:SlowQueryMilliseconds

        // MySQL. (The automated tests swap in SQLite themselves; see tests/Grsms.Tests/Infrastructure/TestDatabase.cs.)
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseMySQL(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<SlowQueryInterceptor>());
        });

        // The DbContext is also the unit of work.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // One generic repository class for every entity type (IRepository<Department>, IRepository<Site>, ...).
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IEmployeeAccounts, EmployeeAccounts>(); // Phase 18: onboarding activation
        services.AddScoped<Application.Onboarding.IPersonalRecordRepository, PersonalRecordRepository>(); // Phase 19
        services.AddScoped<Application.Probation.IProbationRepository, ProbationRepository>(); // Phase 21
        // Phase 14: roles and permissions. The store is one shared in-memory copy of who holds which permission.
        services.AddSingleton<IPermissionStore, PermissionStore>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddScoped<EmailService>();
        services.AddScoped<IEmailService>(sp => sp.GetRequiredService<EmailService>());
        // 11 November 2026: emails to a mail server are sent in the background, so nobody waits for the server.
        services.AddSingleton<EmailQueue>();
        services.AddHostedService<EmailSendingWorker>();

        // Phase 10: the bell, reminder keys, and text messages (written to files until "Sms:Enabled" is true)
        services.AddScoped<Application.Notifications.INotificationCenter, Notifications.NotificationCenter>();
        services.Configure<Notifications.SmsOptions>(config.GetSection("Sms"));
        services.AddScoped<Application.Notifications.ISmsSender, Notifications.SmsSender>();

        // Phase 2: recruitment requests, reference numbers, file storage
        services.AddScoped<IRecruitmentRequestRepository, RecruitmentRequestRepository>();
        services.AddScoped<INumberGenerator, NumberGenerator>();
        services.AddSingleton<Application.Common.IRichTextSanitizer, Security.RichTextSanitizer>(); // QF-81-11 "Description of the post"
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.Configure<UploadOptions>(config.GetSection("Uploads"));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IVirusScanner, Storage.NoVirusScanner>(); // Phase 27: placeholder, every file passes
        services.AddSingleton<IPdfRenderer, Documents.ChromiumPdfRenderer>(); // 13 Nov 2026: PDF letters through headless Chromium
        services.AddSingleton<IDocumentHtmlPreview, Documents.DocxHtmlPreview>(); // docs/37 CR4: Word preview
        services.AddSingleton<Application.Ai.IAiClient>(_ => new Ai.AiClient()); // docs/36: AI providers (Anthropic, OpenAI, Azure OpenAI)
        services.AddSingleton<Application.Ai.IAiLogStore, Ai.AiLogStore>();

        // Phase 3: vacancies. The same repository also answers "is there a vacancy for this request?".
        services.AddScoped<IVacancyRepository, VacancyRepository>();
        services.AddScoped<Application.Employees.IEmployeeRepository, EmployeeRepository>(); // docs/32
        services.AddScoped<Application.Adverts.IAdvertRepository, AdvertRepository>();
        services.AddScoped<Application.Applications.IApplicationRepository, ApplicationRepository>();
        services.AddScoped<Application.Interviews.IInterviewRepository, InterviewRepository>();
        services.AddScoped<IVacancyLookup>(sp => sp.GetRequiredService<IVacancyRepository>());

        // Phase 15: ATS. CV text is read on this server; the Claude reader is optional and off without Ats:Ai:ApiKey.
        services.AddSingleton<Application.Applications.ICvTextExtractor, Ats.CvTextExtractor>();
        services.Configure<Ats.AtsAiOptions>(config.GetSection("Ats:Ai"));
        services.AddSingleton<Ats.ClaudeCvReader>(); // the earlier reader, kept as the fallback (docs/36 AI-5)
        services.AddScoped<Application.Applications.IAiCvReader, Ats.GatewayCvReader>();

        // Phase 16: system settings (database first, then configuration), backups folder. Both read the configuration
        // given here (the web app has already registered it; the tests have not).
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, config);
        services.AddSingleton<Application.Settings.ISettingsStore, Settings.SettingsStore>();
        services.AddSingleton<Application.Settings.IBackupStore, Settings.BackupStore>();

        return services;
    }
}
