using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// Used ONLY by the "dotnet ef" command-line tool (creating migrations and SQL scripts),
/// never by the running application.
///
/// Creating a migration or a SQL script does not connect to MySQL, so a placeholder connection
/// string is fine. To run "dotnet ef database update" against a real server, set the environment
/// variable GRSMS_CONNECTION first (see docs/03-database.md).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("GRSMS_CONNECTION")
                         ?? "server=localhost;port=3306;database=grsms;user=grsms;password=placeholder";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySQL(connection, mysql => mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options);
    }
}
