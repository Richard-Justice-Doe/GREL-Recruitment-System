using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Domain.Employees;
using Grsms.Domain.Security;

namespace Grsms.Application.Employees;

/// <summary>Phase 28 (docs/30): GREL's salary grades. Holders of "Maintain grades" (HR Manager, HR Administrator by default).</summary>
public class GradeService(IRepository<Grade> grades, IRepository<Employee> employees, IUnitOfWork unitOfWork, ICurrentUserService me)
{
    public bool MayManage => me.HasPermission(Permissions.ManageGrades);

    public async Task<IReadOnlyList<GradeListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var counts = (await employees.ListAsync(e => e.GradeId != null && e.Status != EmployeeStatus.Separated, ct))
            .GroupBy(e => e.GradeId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return (await grades.ListAsync(ct: ct)).OrderBy(g => g.SortOrder).ThenBy(g => g.Code)
            .Select(g => new GradeListItemDto(g.Id, g.Code, g.Name, g.SortOrder, g.MinMonthlySalary, g.MaxMonthlySalary, g.IsActive, counts.GetValueOrDefault(g.Id)))
            .ToList();
    }

    /// <summary>Active grades for drop-down lists, as "G5: Senior Officer".</summary>
    public async Task<IReadOnlyList<LookupItem>> GetActiveLookupAsync(CancellationToken ct = default) =>
        (await grades.ListAsync(g => g.IsActive, ct)).OrderBy(g => g.SortOrder).ThenBy(g => g.Code)
            .Select(g => new LookupItem(g.Id.ToString(), Label(g))).ToList();

    public static string Label(Grade g) => $"{g.Code}: {g.Name}";

    public async Task<GradeFormDto?> GetForEditAsync(int id, CancellationToken ct = default) =>
        await grades.GetByIdAsync(id, ct) is not { } g ? null : new GradeFormDto
        {
            Id = g.Id, Code = g.Code, Name = g.Name, SortOrder = g.SortOrder, MinMonthlySalary = g.MinMonthlySalary,
            MaxMonthlySalary = g.MaxMonthlySalary, IsActive = g.IsActive
        };

    public async Task<Result<int>> CreateAsync(GradeFormDto form, CancellationToken ct = default)
    {
        if (!MayManage) return Result.Failure<int>("You may not maintain grades.");
        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);
        var g = new Grade();
        Apply(form, g);
        grades.Add(g);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(g.Id);
    }

    public async Task<Result> UpdateAsync(GradeFormDto form, CancellationToken ct = default)
    {
        if (!MayManage) return Result.Failure("You may not maintain grades.");
        if (await grades.GetByIdAsync(form.Id, ct) is not { } g) return Result.Failure("Grade not found.");
        Normalize(form);
        var errors = await ValidateAsync(form, ct);
        if (errors.Count > 0) return Result.Failure([.. errors]);
        Apply(form, g);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        if (!MayManage) return Result.Failure("You may not maintain grades.");
        if (await grades.GetByIdAsync(id, ct) is not { } g) return Result.Failure("Grade not found.");
        g.IsActive = active;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Normalize(GradeFormDto form)
    {
        form.Code = form.Code.Trim().ToUpperInvariant();
        form.Name = form.Name.Trim();
    }

    private async Task<List<string>> ValidateAsync(GradeFormDto form, CancellationToken ct)
    {
        var errors = new List<string>();
        if (await grades.AnyAsync(g => g.Code == form.Code && g.Id != form.Id, ct)) errors.Add($"The code '{form.Code}' is already used by another grade.");
        if (await grades.AnyAsync(g => g.Name == form.Name && g.Id != form.Id, ct)) errors.Add($"A grade named '{form.Name}' already exists.");
        if (form.MinMonthlySalary is { } min && form.MaxMonthlySalary is { } max && min > max)
            errors.Add("The minimum salary cannot be more than the maximum.");
        return errors;
    }

    private static void Apply(GradeFormDto form, Grade g)
    {
        (g.Code, g.Name, g.SortOrder, g.MinMonthlySalary, g.MaxMonthlySalary, g.IsActive) =
            (form.Code, form.Name, form.SortOrder, form.MinMonthlySalary, form.MaxMonthlySalary, form.IsActive);
    }
}
