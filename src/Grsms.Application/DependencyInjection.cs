using Grsms.Application.Organization;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Application;

/// <summary>
/// Registers the Application layer's services with the dependency-injection container.
/// Called once from Program.cs: <c>builder.Services.AddApplication();</c>
///
/// "Scoped" means one instance per web request, which matches the database context's lifetime.
/// When a new module adds a service, register it here.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Master data (Phase 1)
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<ISiteService, SiteService>();

        return services;
    }
}
