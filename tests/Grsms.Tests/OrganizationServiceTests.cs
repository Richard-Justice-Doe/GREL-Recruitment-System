using System.Text.Json;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Domain.Auditing;
using Grsms.Domain.Organization;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Business rules for departments, units and sites, plus the automatic audit fields,
/// soft delete and audit trail that every save goes through.
/// </summary>
public class OrganizationServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    public void Dispose() => _db.Dispose();

    private T Get<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    [Fact]
    public async Task Seed_data_contains_the_24_departments_8_roles_and_head_office()
    {
        using var scope = _db.NewScope();
        var db = Get<ApplicationDbContext>(scope);

        Assert.Equal(24, await db.Departments.CountAsync());
        Assert.Equal(8, await db.Roles.CountAsync());
        Assert.Contains(await db.Departments.Select(d => d.Name).ToListAsync(), n => n == "Health, Safety & Environment (HSE)");
        Assert.Equal("HQ", (await db.Sites.SingleAsync()).Code);
    }

    [Fact]
    public async Task Creating_a_department_fills_audit_fields_and_writes_an_audit_row()
    {
        using var scope = _db.NewScope();
        var result = await Get<IDepartmentService>(scope).CreateAsync(new DepartmentFormDto { Name = "Agronomy", Code = " agr " });

        Assert.True(result.Succeeded);
        var db = Get<ApplicationDbContext>(scope);
        var dept = await db.Departments.SingleAsync(d => d.Id == result.Value);
        Assert.Equal("AGR", dept.Code);                       // trimmed + upper-cased
        Assert.Equal("tester@grel.test", dept.CreatedBy);

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityName == "Department" && a.EntityId == dept.Id.ToString());
        Assert.Equal(AuditActions.Created, audit.Action);
        Assert.Equal("10.0.0.1", audit.IpAddress);
        Assert.Contains("Agronomy", audit.NewValues);
    }

    [Fact]
    public async Task Updating_a_department_audits_only_the_changed_fields()
    {
        using (var scope = _db.NewScope())
        {
            var service = Get<IDepartmentService>(scope);
            var form = (await service.GetForEditAsync(3))!; // Finance
            form.Description = "Money matters";
            Assert.True((await service.UpdateAsync(form)).Succeeded);
        }

        using (var scope = _db.NewScope())
        {
            var db = Get<ApplicationDbContext>(scope);
            var audit = await db.AuditLogs.SingleAsync(a => a.EntityName == "Department" && a.Action == AuditActions.Updated);
            var newValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(audit.NewValues!)!;
            Assert.Equal(["Description"], newValues.Keys);
            Assert.Equal("Money matters", newValues["Description"].GetString());
            Assert.Equal("tester@grel.test", (await db.Departments.FindAsync(3))!.UpdatedBy);
        }
    }

    [Fact]
    public async Task Duplicate_department_name_or_code_is_rejected()
    {
        using var scope = _db.NewScope();
        var result = await Get<IDepartmentService>(scope).CreateAsync(new DepartmentFormDto { Name = "Finance", Code = "HSE" });

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public async Task Removing_a_record_is_a_soft_delete()
    {
        int id;
        using (var scope = _db.NewScope())
        {
            var units = Get<IUnitService>(scope);
            id = (await units.CreateAsync(new UnitFormDto { DepartmentId = 3, Name = "Payroll", Code = "PAY" })).Value;

            var repo = Get<IRepository<Unit>>(scope);
            repo.Remove((await repo.GetByIdAsync(id))!);
            await Get<IUnitOfWork>(scope).SaveChangesAsync();
        }

        using (var scope = _db.NewScope())
        {
            var db = Get<ApplicationDbContext>(scope);
            Assert.Null(await Get<IRepository<Unit>>(scope).GetByIdAsync(id));     // hidden from normal queries
            var row = await db.Units.IgnoreQueryFilters().SingleAsync(u => u.Id == id); // but still in the table
            Assert.True(row.IsDeleted);
            Assert.Equal("tester@grel.test", row.DeletedBy);
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityName == "Unit" && a.Action == AuditActions.Deleted));
        }
    }

    [Fact]
    public async Task Unit_names_are_unique_only_within_their_department()
    {
        using var scope = _db.NewScope();
        var units = Get<IUnitService>(scope);

        Assert.True((await units.CreateAsync(new UnitFormDto { DepartmentId = 3, Name = "Admin", Code = "ADM" })).Succeeded);
        Assert.True((await units.CreateAsync(new UnitFormDto { DepartmentId = 2, Name = "Admin", Code = "ADM" })).Succeeded);
        Assert.False((await units.CreateAsync(new UnitFormDto { DepartmentId = 3, Name = "Admin", Code = "ADM2" })).Succeeded);
    }

    [Fact]
    public async Task Units_cannot_be_added_to_or_activated_in_an_inactive_department()
    {
        using var scope = _db.NewScope();
        var units = Get<IUnitService>(scope);
        var departments = Get<IDepartmentService>(scope);

        var unitId = (await units.CreateAsync(new UnitFormDto { DepartmentId = 4, Name = "Tenders", Code = "TEN" })).Value;
        await units.SetActiveAsync(unitId, false);
        await departments.SetActiveAsync(4, false);

        Assert.False((await units.SetActiveAsync(unitId, true)).Succeeded);
        Assert.False((await units.CreateAsync(new UnitFormDto { DepartmentId = 4, Name = "Stores", Code = "STO" })).Succeeded);
    }

    [Fact]
    public async Task Inactive_departments_are_left_out_of_drop_down_lists()
    {
        using var scope = _db.NewScope();
        var departments = Get<IDepartmentService>(scope);
        await departments.SetActiveAsync(5, false);

        var lookup = await departments.GetActiveLookupAsync();
        Assert.Equal(23, lookup.Count);
        Assert.DoesNotContain(lookup, l => l.Value == "5");
    }
}
