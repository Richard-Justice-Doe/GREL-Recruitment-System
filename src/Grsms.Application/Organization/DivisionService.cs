using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Organization;

namespace Grsms.Application.Organization;

/// <summary>
/// Business operations for divisions, the top level of GREL's organisation (Corporate, Operations).
/// Departments are grouped under divisions; see <see cref="Division"/>.
/// </summary>
public interface IDivisionService
{
    /// <summary>All divisions, active and inactive, sorted by name, with their department counts.</summary>
    Task<IReadOnlyList<DivisionListItemDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Loads a division into the edit form, or null if not found.</summary>
    Task<DivisionFormDto?> GetForEditAsync(int id, CancellationToken ct = default);

    /// <summary>Active divisions for drop-down lists.</summary>
    Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(CancellationToken ct = default);

    /// <summary>Every division (active or not), for report filters, so old records can still be found.</summary>
    Task<IReadOnlyList<LookupItem>> GetAllLookupAsync(CancellationToken ct = default);

    Task<Result<int>> CreateAsync(DivisionFormDto form, CancellationToken ct = default);
    Task<Result> UpdateAsync(DivisionFormDto form, CancellationToken ct = default);

    /// <summary>Activates or deactivates a division. A division with active departments cannot be deactivated.</summary>
    Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default);
}

/// <inheritdoc cref="IDivisionService"/>
public class DivisionService(
    IRepository<Division> divisions,
    IRepository<Department> departments,
    IUnitOfWork unitOfWork) : IDivisionService
{
    public async Task<IReadOnlyList<DivisionListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await divisions.ListAsync(ct: ct);
        var depts = await departments.ListAsync(ct: ct);
        return all
            .OrderBy(d => d.Name)
            .Select(d => new DivisionListItemDto(
                d.Id, d.Name, d.Code, d.Description,
                depts.Count(x => x.DivisionId == d.Id && x.IsActive),
                depts.Count(x => x.DivisionId == d.Id && !x.IsActive),
                d.IsActive))
            .ToList();
    }

    public async Task<DivisionFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var d = await divisions.GetByIdAsync(id, ct);
        return d is null ? null : new DivisionFormDto { Id = d.Id, Name = d.Name, Code = d.Code, Description = d.Description, IsActive = d.IsActive };
    }

    public async Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(CancellationToken ct = default) =>
        (await divisions.ListAsync(d => d.IsActive, ct))
            .OrderBy(d => d.Name).Select(d => new LookupItem(d.Id.ToString(), d.Name)).ToList();

    public async Task<IReadOnlyList<LookupItem>> GetAllLookupAsync(CancellationToken ct = default) =>
        (await divisions.ListAsync(ct: ct))
            .OrderBy(d => d.Name).Select(d => new LookupItem(d.Id.ToString(), d.IsActive ? d.Name : $"{d.Name} (inactive)")).ToList();

    public async Task<Result<int>> CreateAsync(DivisionFormDto form, CancellationToken ct = default)
    {
        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);

        var entity = new Division();
        Apply(form, entity);
        divisions.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(DivisionFormDto form, CancellationToken ct = default)
    {
        var entity = await divisions.GetByIdAsync(form.Id, ct);
        if (entity is null) return Result.Failure("Division not found.");

        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (!form.IsActive && entity.IsActive && await HasActiveDepartmentsAsync(form.Id, ct))
            errors.Add(ActiveDepartmentsMessage);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        Apply(form, entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var entity = await divisions.GetByIdAsync(id, ct);
        if (entity is null) return Result.Failure("Division not found.");
        if (!isActive && await HasActiveDepartmentsAsync(id, ct)) return Result.Failure(ActiveDepartmentsMessage);

        entity.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private const string ActiveDepartmentsMessage =
        "This division still has active departments. Move them to another division, or deactivate them, first.";

    private Task<bool> HasActiveDepartmentsAsync(int divisionId, CancellationToken ct) =>
        departments.AnyAsync(d => d.DivisionId == divisionId && d.IsActive, ct);

    private static void Normalize(DivisionFormDto form)
    {
        form.Name = form.Name.Trim();
        form.Code = form.Code.Trim().ToUpperInvariant();
        form.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
    }

    private async Task<List<string>> ValidateAsync(DivisionFormDto form, CancellationToken ct)
    {
        var errors = new List<string>();
        if (await divisions.AnyAsync(d => d.Name == form.Name && d.Id != form.Id, ct))
            errors.Add($"A division named '{form.Name}' already exists.");
        if (await divisions.AnyAsync(d => d.Code == form.Code && d.Id != form.Id, ct))
            errors.Add($"The code '{form.Code}' is already used by another division.");
        return errors;
    }

    private static void Apply(DivisionFormDto form, Division entity)
    {
        entity.Name = form.Name;
        entity.Code = form.Code;
        entity.Description = form.Description;
        entity.IsActive = form.IsActive;
    }
}
