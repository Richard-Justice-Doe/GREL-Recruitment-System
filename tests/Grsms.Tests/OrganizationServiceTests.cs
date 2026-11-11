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
    public async Task Seed_data_contains_two_divisions_17_grel_departments_11_roles_and_grel_sites()
    {
        using var scope = _db.NewScope();
        var db = Get<ApplicationDbContext>(scope);

        Assert.Equal(["Corporate", "Operations"], await db.Divisions.OrderBy(d => d.Id).Select(d => d.Name).ToListAsync());

        // 33 rows: GREL's 17 departments are active (9 Corporate, 8 Operations); the rest are kept but inactive.
        Assert.Equal(33, await db.Departments.CountAsync());
        var active = await db.Departments.Where(d => d.IsActive).ToListAsync();
        Assert.Equal(17, active.Count);
        Assert.Equal(9, active.Count(d => d.DivisionId == 1));
        Assert.Equal(8, active.Count(d => d.DivisionId == 2));
        Assert.Contains(active, d => d.Name == "Transport" && d.DivisionId == 1);
        Assert.Contains(active, d => d.Name == "Factory TBU" && d.DivisionId == 2);
        Assert.Contains(active, d => d.Name == "Nucleus Operations" && d.DivisionId == 2);
        Assert.DoesNotContain(active, d => d.Name is "Estates" or "Progress and Quality" or "Project");
        Assert.Equal(11, await db.Roles.CountAsync()); // 8 + Super Admin (Phase 14) + Admin + IT Manager (11 Nov 2026)

        Assert.Equal(14, await db.Sites.CountAsync());
        var hq = await db.Sites.SingleAsync(s => s.Id == 1);
        Assert.Equal(("HQ", "Corporate Headquarters", "Takoradi"), (hq.Code, hq.SiteType, hq.Location));
        Assert.Equal("Rubber Plantation Estate", (await db.Sites.SingleAsync(s => s.Name == "Subri")).SiteType);

        // GREL's 12 sites are active; Tsibu is the TBU factory, so the separate "TBU Factory" row (13) is inactive.
        Assert.Equal(12, await db.Sites.CountAsync(s => s.IsActive));
        Assert.StartsWith("Rubber Processing Factory", (await db.Sites.SingleAsync(s => s.Name == "Tsibu")).SiteType);
        Assert.True(await db.Sites.AnyAsync(s => s.Name == "Apeminim" && s.IsActive));
        Assert.False(await db.Sites.AnyAsync(s => s.Code == "TBU" && s.IsActive));
    }

    [Fact]
    public async Task A_department_needs_an_active_division()
    {
        using var scope = _db.NewScope();
        var departments = Get<IDepartmentService>(scope);

        Assert.False((await departments.CreateAsync(new DepartmentFormDto { Name = "Agronomy", Code = "AGR" })).Succeeded);
        Assert.False((await departments.CreateAsync(new DepartmentFormDto { Name = "Agronomy", Code = "AGR", DivisionId = 99 })).Succeeded);

        // Moving Finance (Corporate) to Operations is allowed and shows on the list.
        var form = (await departments.GetForEditAsync(3))!;
        form.DivisionId = 2;
        Assert.True((await departments.UpdateAsync(form)).Succeeded);
        Assert.Equal("Operations", (await departments.GetAllAsync()).Single(d => d.Id == 3).DivisionName);
    }

    [Fact]
    public async Task A_division_with_active_departments_cannot_be_deactivated()
    {
        using var scope = _db.NewScope();
        var divisions = Get<IDivisionService>(scope);

        Assert.False((await divisions.SetActiveAsync(1, false)).Succeeded);

        var created = await divisions.CreateAsync(new DivisionFormDto { Name = "Projects", Code = " proj " });
        Assert.True(created.Succeeded);
        Assert.True((await divisions.SetActiveAsync(created.Value, false)).Succeeded);
        Assert.False((await divisions.CreateAsync(new DivisionFormDto { Name = "Corporate", Code = "PROJ" })).Succeeded);

        // An inactive division cannot receive departments.
        Assert.False((await Get<IDepartmentService>(scope).CreateAsync(
            new DepartmentFormDto { Name = "Agronomy", Code = "AGR", DivisionId = created.Value })).Succeeded);
    }

    [Fact]
    public async Task Creating_a_department_fills_audit_fields_and_writes_an_audit_row()
    {
        using var scope = _db.NewScope();
        var result = await Get<IDepartmentService>(scope).CreateAsync(new DepartmentFormDto { Name = "Agronomy", Code = " agr ", DivisionId = 2 });

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
        var result = await Get<IDepartmentService>(scope).CreateAsync(new DepartmentFormDto { Name = "Finance", Code = "HSE", DivisionId = 1 });

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
        Assert.Equal(16, lookup.Count); // GREL's 17 active departments, minus the one just deactivated
        Assert.DoesNotContain(lookup, l => l.Value == "5");
    }
}
