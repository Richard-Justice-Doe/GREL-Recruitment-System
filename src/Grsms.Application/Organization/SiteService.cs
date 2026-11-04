using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Organization;

namespace Grsms.Application.Organization;

/// <summary>Business operations for sites (physical work locations).</summary>
public interface ISiteService
{
    Task<IReadOnlyList<SiteListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<SiteFormDto?> GetForEditAsync(int id, CancellationToken ct = default);

    /// <summary>Active sites, for drop-down lists.</summary>
    Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(CancellationToken ct = default);

    Task<Result<int>> CreateAsync(SiteFormDto form, CancellationToken ct = default);
    Task<Result> UpdateAsync(SiteFormDto form, CancellationToken ct = default);
    Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default);
}

/// <inheritdoc cref="ISiteService"/>
public class SiteService(IRepository<Site> sites, IUnitOfWork unitOfWork) : ISiteService
{
    public async Task<IReadOnlyList<SiteListItemDto>> GetAllAsync(CancellationToken ct = default) =>
        (await sites.ListAsync(ct: ct))
            .OrderBy(s => s.Name)
            .Select(s => new SiteListItemDto(s.Id, s.Name, s.Code, s.SiteType, s.Location, s.IsActive))
            .ToList();

    public async Task<SiteFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var s = await sites.GetByIdAsync(id, ct);
        return s is null ? null : new SiteFormDto
        {
            Id = s.Id,
            Name = s.Name,
            Code = s.Code,
            SiteType = s.SiteType,
            Location = s.Location,
            IsActive = s.IsActive
        };
    }

    public async Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(CancellationToken ct = default) =>
        (await sites.ListAsync(s => s.IsActive, ct))
            .OrderBy(s => s.Name)
            .Select(s => new LookupItem(s.Id.ToString(), s.Name))
            .ToList();

    public async Task<Result<int>> CreateAsync(SiteFormDto form, CancellationToken ct = default)
    {
        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);

        var entity = new Site();
        Apply(form, entity);
        sites.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    public async Task<Result> UpdateAsync(SiteFormDto form, CancellationToken ct = default)
    {
        var entity = await sites.GetByIdAsync(form.Id, ct);
        if (entity is null) return Result.Failure("Site not found.");

        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (errors.Count > 0) return Result.Failure([.. errors]);

        Apply(form, entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        var entity = await sites.GetByIdAsync(id, ct);
        if (entity is null) return Result.Failure("Site not found.");

        entity.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Normalize(SiteFormDto form)
    {
        form.Name = form.Name.Trim();
        form.Code = form.Code.Trim().ToUpperInvariant();
        form.Location = string.IsNullOrWhiteSpace(form.Location) ? null : form.Location.Trim();
        form.SiteType = string.IsNullOrWhiteSpace(form.SiteType) ? null : form.SiteType.Trim();
    }

    private async Task<List<string>> ValidateAsync(SiteFormDto form, CancellationToken ct)
    {
        var errors = new List<string>();
        if (await sites.AnyAsync(s => s.Name == form.Name && s.Id != form.Id, ct))
            errors.Add($"A site named '{form.Name}' already exists.");
        if (await sites.AnyAsync(s => s.Code == form.Code && s.Id != form.Id, ct))
            errors.Add($"The code '{form.Code}' is already used by another site.");
        return errors;
    }

    private static void Apply(SiteFormDto form, Site entity)
    {
        entity.Name = form.Name;
        entity.Code = form.Code;
        entity.SiteType = form.SiteType;
        entity.Location = form.Location;
        entity.IsActive = form.IsActive;
    }
}
