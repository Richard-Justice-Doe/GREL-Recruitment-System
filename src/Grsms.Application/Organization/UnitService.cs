using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Organization;

namespace Grsms.Application.Organization;

/// <summary>Business operations for units (sub-sections of departments).</summary>
public interface IUnitService
{
    /// <summary>All units, optionally only those of one department, sorted by department then name.</summary>
    Task<IReadOnlyList<UnitListItemDto>> GetAllAsync(int? departmentId = null, CancellationToken ct = default);

    /// <summary>Loads a unit into the edit form, or null if not found.</summary>
    Task<UnitFormDto?> GetForEditAsync(int id, CancellationToken ct = default);

    /// <summary>Active units of one department, for drop-down lists.</summary>
    Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(int departmentId, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(UnitFormDto form, CancellationToken ct = default);
    Task<Result> UpdateAsync(UnitFormDto form, CancellationToken ct = default);
    Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default);
}

/// <inheritdoc cref="IUnitService"/>
public class UnitService(
    IRepository<Unit> units,
    IRepository<Department> departments,
    IUnitOfWork unitOfWork) : IUnitService
{
    public async Task<IReadOnlyList<UnitListItemDto>> GetAllAsync(int? departmentId = null, CancellationToken ct = default)
    {
        var list = departmentId is null
            ? await units.ListAsync(ct: ct)
            : await units.ListAsync(u => u.DepartmentId == departmentId, ct);
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);

        return list
            .Select(u => new UnitListItemDto(u.Id, u.DepartmentId, deptNames.GetValueOrDefault(u.DepartmentId, "?"), u.Name, u.Code, u.IsActive))
            .OrderBy(u => u.DepartmentName).ThenBy(u => u.Name)
            .ToList();
    }

    public async Task<UnitFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var u = await units.GetByIdAsync(id, ct);
        return u is null ? null : new UnitFormDto
        {
            Id = u.Id,
            DepartmentId = u.DepartmentId,
            Name = u.Name,
            Code = u.Code,
            Description = u.Description,
            IsActive = u.IsActive
        };
    }

    public async Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(int departmentId, CancellationToken ct = default)
    {
        var list = await units.ListAsync(u => u.DepartmentId == departmentId && u.IsActive, ct);
        return list.OrderBy(u => u.Name).Select(u => new LookupItem(u.Id.ToString(), u.Name)).ToList();
    }

    public async Task<Result<int>> CreateAsync(UnitFormDto form, CancellationToken ct = default)
    {
        Normalize(form);
        var errors = await ValidateAsync(form, isNew: true, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);

        var entity = new Unit();
        Apply(form, entity);
        units.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(UnitFormDto form, CancellationToken ct = default)
    {
        var entity = await units.GetByIdAsync(form.Id, ct);
        if (entity is null) return Result.Failure("Unit not found.");

        Normalize(form);
        var errors = await ValidateAsync(form, isNew: false, ct);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        Apply(form, entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var entity = await units.GetByIdAsync(id, ct);
        if (entity is null) return Result.Failure("Unit not found.");

        // A unit cannot be switched back on while its department is switched off.
        if (isActive && !await departments.AnyAsync(d => d.Id == entity.DepartmentId && d.IsActive, ct))
            return Result.Failure("Activate the department first; a unit cannot be active inside an inactive department.");

        entity.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Normalize(UnitFormDto form)
    {
        form.Name = form.Name.Trim();
        form.Code = form.Code.Trim().ToUpperInvariant();
        form.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
    }

    private async Task<List<string>> ValidateAsync(UnitFormDto form, bool isNew, CancellationToken ct)
    {
        var errors = new List<string>();
        var deptId = form.DepartmentId!.Value;

        // New units may only be added to ACTIVE departments; an existing unit may stay where it is.
        var dept = await departments.GetByIdAsync(deptId, ct);
        if (dept is null)
            errors.Add("The selected department does not exist.");
        else if (isNew && !dept.IsActive)
            errors.Add($"'{dept.Name}' is inactive. Activate it before adding units.");

        // Unit names/codes only need to be unique INSIDE a department.
        if (await units.AnyAsync(u => u.DepartmentId == deptId && u.Name == form.Name && u.Id != form.Id, ct))
            errors.Add($"This department already has a unit named '{form.Name}'.");
        if (await units.AnyAsync(u => u.DepartmentId == deptId && u.Code == form.Code && u.Id != form.Id, ct))
            errors.Add($"This department already has a unit with code '{form.Code}'.");

        return errors;
    }

    private static void Apply(UnitFormDto form, Unit entity)
    {
        entity.DepartmentId = form.DepartmentId!.Value;
        entity.Name = form.Name;
        entity.Code = form.Code;
        entity.Description = form.Description;
        entity.IsActive = form.IsActive;
    }
}
