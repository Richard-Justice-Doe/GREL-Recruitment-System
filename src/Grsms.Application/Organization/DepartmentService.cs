using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Users;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;

namespace Grsms.Application.Organization;

/// <summary>Business operations for departments (Department Master Data).</summary>
public interface IDepartmentService
{
    /// <summary>All departments, active and inactive, sorted by name.</summary>
    Task<IReadOnlyList<DepartmentListItemDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>One department with its units, or null if not found.</summary>
    Task<DepartmentDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>Loads a department into the edit form, or null if not found.</summary>
    Task<DepartmentFormDto?> GetForEditAsync(int id, CancellationToken ct = default);

    /// <summary>Active departments for drop-down lists on other screens.</summary>
    Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(CancellationToken ct = default);

    /// <summary>
    /// Departments grouped under their division, for report filters. With <paramref name="includeInactive"/>,
    /// inactive departments are listed too (marked "(inactive)") so old records can still be found.
    /// </summary>
    Task<IReadOnlyList<GroupedLookupItem>> GetGroupedLookupAsync(bool includeInactive, CancellationToken ct = default);

    /// <summary>Creates a department. Returns the new id, or errors (e.g. duplicate name/code).</summary>
    Task<Result<int>> CreateAsync(DepartmentFormDto form, CancellationToken ct = default);

    /// <summary>Saves changes to an existing department.</summary>
    Task<Result> UpdateAsync(DepartmentFormDto form, CancellationToken ct = default);

    /// <summary>Activates or deactivates a department.</summary>
    Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default);
}

/// <inheritdoc cref="IDepartmentService"/>
public class DepartmentService(
    IRepository<Department> departments,
    IRepository<Division> divisions,
    IRepository<Unit> units,
    IUserDirectory users,
    IUnitOfWork unitOfWork) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await departments.ListAsync(ct: ct);

        // Count units per department in memory: master data is small (tens of rows), so two simple
        // queries are clearer than one complicated join.
        var unitCounts = (await units.ListAsync(ct: ct))
            .GroupBy(u => u.DepartmentId)
            .ToDictionary(g => g.Key, g => g.Count());

        var hodNames = await users.GetDisplayNamesAsync(
            all.Select(d => d.HeadOfDepartmentUserId).OfType<string>(), ct);
        var divisionNames = await DivisionNamesAsync(ct);

        return all
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentListItemDto(
                d.Id,
                d.Name,
                d.Code,
                divisionNames.GetValueOrDefault(d.DivisionId, "?"),
                d.HeadOfDepartmentUserId is null ? null : hodNames.GetValueOrDefault(d.HeadOfDepartmentUserId),
                unitCounts.GetValueOrDefault(d.Id),
                d.IsActive))
            .ToList();
    }

    public async Task<DepartmentDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var d = await departments.GetByIdAsync(id, ct);
        if (d is null) return null;

        var deptUnits = await units.ListAsync(u => u.DepartmentId == id, ct);
        string? hodName = null;
        if (d.HeadOfDepartmentUserId is not null)
        {
            var names = await users.GetDisplayNamesAsync([d.HeadOfDepartmentUserId], ct);
            hodName = names.GetValueOrDefault(d.HeadOfDepartmentUserId);
        }

        var division = await divisions.GetByIdAsync(d.DivisionId, ct);

        return new DepartmentDetailsDto(
            d.Id, d.Name, d.Code, d.DivisionId, division?.Name ?? "?", d.Description, d.HeadOfDepartmentUserId, hodName, d.IsActive,
            d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy,
            deptUnits
                .OrderBy(u => u.Name)
                .Select(u => new UnitListItemDto(u.Id, d.Id, d.Name, u.Name, u.Code, u.IsActive))
                .ToList());
    }

    public async Task<DepartmentFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var d = await departments.GetByIdAsync(id, ct);
        return d is null ? null : new DepartmentFormDto
        {
            Id = d.Id,
            Name = d.Name,
            Code = d.Code,
            DivisionId = d.DivisionId,
            Description = d.Description,
            HeadOfDepartmentUserId = d.HeadOfDepartmentUserId,
            IsActive = d.IsActive
        };
    }

    public async Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(CancellationToken ct = default)
    {
        var active = await departments.ListAsync(d => d.IsActive, ct);
        return active.OrderBy(d => d.Name).Select(d => new LookupItem(d.Id.ToString(), d.Name)).ToList();
    }

    public async Task<IReadOnlyList<GroupedLookupItem>> GetGroupedLookupAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive ? await departments.ListAsync(ct: ct) : await departments.ListAsync(d => d.IsActive, ct);
        var divisionNames = await DivisionNamesAsync(ct);
        return list
            .Select(d => new GroupedLookupItem(d.Id.ToString(), d.IsActive ? d.Name : $"{d.Name} (inactive)",
                divisionNames.GetValueOrDefault(d.DivisionId, "?")))
            .OrderBy(x => x.Group).ThenBy(x => x.Text.EndsWith("(inactive)") ? 1 : 0).ThenBy(x => x.Text)
            .ToList();
    }

    private async Task<Dictionary<int, string>> DivisionNamesAsync(CancellationToken ct) =>
        (await divisions.ListAsync(ct: ct)).ToDictionary(v => v.Id, v => v.Name);

    public async Task<Result<int>> CreateAsync(DepartmentFormDto form, CancellationToken ct = default)
    {
        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);

        var entity = new Department();
        Apply(form, entity);
        departments.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(DepartmentFormDto form, CancellationToken ct = default)
    {
        var entity = await departments.GetByIdAsync(form.Id, ct);
        if (entity is null) return Result.Failure("Department not found.");

        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        Apply(form, entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var entity = await departments.GetByIdAsync(id, ct);
        if (entity is null) return Result.Failure("Department not found.");

        // Deactivating only stops the department being chosen for NEW work. Existing requests,
        // vacancies and units keep pointing to it so reports stay correct.
        entity.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Trims text and upper-cases the code so "hse " and "HSE" are treated as the same code.</summary>
    private static void Normalize(DepartmentFormDto form)
    {
        form.Name = form.Name.Trim();
        form.Code = form.Code.Trim().ToUpperInvariant();
        form.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        form.HeadOfDepartmentUserId = string.IsNullOrWhiteSpace(form.HeadOfDepartmentUserId) ? null : form.HeadOfDepartmentUserId;
    }

    /// <summary>Business rules that the form attributes cannot check on their own.</summary>
    private async Task<List<string>> ValidateAsync(DepartmentFormDto form, CancellationToken ct)
    {
        var errors = new List<string>();

        // Names and codes must be unique. "d.Id != form.Id" lets a record keep its own name when edited.
        // MySQL's default collation is case-insensitive, so "finance" clashes with "Finance".
        if (await departments.AnyAsync(d => d.Name == form.Name && d.Id != form.Id, ct))
            errors.Add($"A department named '{form.Name}' already exists.");
        if (await departments.AnyAsync(d => d.Code == form.Code && d.Id != form.Id, ct))
            errors.Add($"The code '{form.Code}' is already used by another department.");

        // The division must exist and be active. (An existing department may keep an inactive division it already has.)
        var division = form.DivisionId is int divId ? await divisions.GetByIdAsync(divId, ct) : null;
        if (division is null)
            errors.Add("Choose the division this department belongs to.");
        else if (!division.IsActive &&
                 !await departments.AnyAsync(d => d.Id == form.Id && d.DivisionId == division.Id, ct))
            errors.Add($"The division '{division.Name}' is inactive.");

        // The HOD must be a real, active user who holds the Head of Department role.
        if (form.HeadOfDepartmentUserId is not null &&
            !await users.IsActiveUserInRoleAsync(form.HeadOfDepartmentUserId, Roles.HeadOfDepartment, ct))
            errors.Add("The selected Head of Department must be an active user with the 'Head of Department' role.");

        return errors;
    }

    /// <summary>Copies the form values onto the entity (manual mapping keeps every field visible and avoids a mapping library).</summary>
    private static void Apply(DepartmentFormDto form, Department entity)
    {
        entity.Name = form.Name;
        entity.Code = form.Code;
        entity.DivisionId = form.DivisionId!.Value;
        entity.Description = form.Description;
        entity.HeadOfDepartmentUserId = form.HeadOfDepartmentUserId;
        entity.IsActive = form.IsActive;
    }
}
