using Grsms.Application.Common;
using Grsms.Application.Employees;
using Grsms.Application.Recruitment;
using Grsms.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>docs/32 (11 November 2026): the employee list filtered, counted and paged in the database.</summary>
public class EmployeeRepository(ApplicationDbContext context) : Repository<Employee>(context), IEmployeeRepository
{
    private IQueryable<Employee> Visible(RequestVisibility v)
    {
        var q = Db.Employees.AsNoTracking();
        if (v.SeeAll) return q;
        var departments = v.HodDepartmentIds.ToList();
        return q.Where(e => departments.Contains(e.DepartmentId) || e.UserId == v.UserId);
    }

    public async Task<PagedResult<EmployeeRow>> SearchAsync(EmployeeFilter filter, RequestVisibility visibility, CancellationToken ct = default)
    {
        var q = Visible(visibility);
        q = filter.Status is { } status ? q.Where(e => e.Status == status) : q.Where(e => e.Status != EmployeeStatus.Separated);
        if (filter.DepartmentId is { } dep) q = q.Where(e => e.DepartmentId == dep);
        if (filter.SiteId is { } site) q = q.Where(e => e.SiteId == site);
        if (filter.GradeId is { } grade) q = q.Where(e => e.GradeId == grade);

        var rows = from e in q
                   join u in Db.Users on e.UserId equals u.Id
                   select new { e, u.FullName };
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // LIKE is case-insensitive in MySQL (and in SQLite for the tests); % and _ typed by the user are taken literally.
            var like = "%" + filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            rows = rows.Where(x => EF.Functions.Like(x.FullName, like, "\\") || EF.Functions.Like(x.e.EmployeeNumber, like, "\\") ||
                                   EF.Functions.Like(x.e.PositionTitle, like, "\\"));
        }

        var pageSize = Math.Clamp(filter.PageSize, 10, 200);
        var total = await rows.CountAsync(ct);
        var page = Math.Clamp(filter.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var items = await rows.OrderBy(x => x.FullName).ThenBy(x => x.e.EmployeeNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new EmployeeRow(x.e.Id, x.e.EmployeeNumber, x.FullName, x.e.PositionTitle, x.e.DepartmentId, x.e.SiteId, x.e.GradeId,
                x.e.EmploymentType, x.e.Status, x.e.HireDate))
            .ToListAsync(ct);
        return new PagedResult<EmployeeRow>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyDictionary<EmployeeStatus, int>> CountByStatusAsync(RequestVisibility visibility, CancellationToken ct = default) =>
        (await Visible(visibility).GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
        .ToDictionary(x => x.Key, x => x.Count);

    public async Task<IReadOnlyDictionary<int, int>> CountByGradeAsync(CancellationToken ct = default) =>
        (await Db.Employees.AsNoTracking().Where(e => e.GradeId != null && e.Status != EmployeeStatus.Separated)
            .GroupBy(e => e.GradeId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
        .ToDictionary(x => x.Key, x => x.Count);
}
