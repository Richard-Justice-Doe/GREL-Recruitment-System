using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Application.Recruitment;
using Grsms.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>Job application data access: history loading, the staff list search, counts and "My applications".</summary>
public class ApplicationRepository(ApplicationDbContext context) : Repository<JobApplication>(context), IApplicationRepository
{
    public Task<JobApplication?> GetWithHistoryAsync(int id, CancellationToken ct = default) =>
        Db.JobApplications.Include(a => a.Actions).FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>Applications the user may see as staff: all for "see all" roles, otherwise their departments'.</summary>
    private IQueryable<JobApplication> Visible(RequestVisibility visibility)
    {
        var q = Db.JobApplications.AsNoTracking();
        if (visibility.SeeAll) return q;
        var hodIds = visibility.HodDepartmentIds.ToList();
        return q.Where(a => hodIds.Contains(a.DepartmentId));
    }

    public async Task<PagedResult<ApplicationListItemDto>> SearchAsync(ApplicationFilter filter, RequestVisibility visibility, CancellationToken ct = default)
    {
        var q = Visible(visibility);

        if (filter.Status is { } status) q = q.Where(a => a.Status == status);
        if (filter.VacancyId is { } vacancyId) q = q.Where(a => a.VacancyId == vacancyId);
        if (filter.DivisionId is { } divId) q = q.Where(a => a.DivisionId == divId);
        if (filter.DepartmentId is { } deptId) q = q.Where(a => a.DepartmentId == deptId);
        if (filter.Source is { } source) q = q.Where(a => a.Source == source);
        if (filter.Internal is { } isInternal) q = q.Where(a => a.IsInternal == isInternal);
        if (filter.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            q = q.Where(a => a.SubmittedAt >= start);
        }
        if (filter.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(a => a.SubmittedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(a => a.ApplicationNumber.Contains(s) || a.FullName.Contains(s) || a.Email.Contains(s) ||
                             Db.Vacancies.Any(v => v.Id == a.VacancyId && v.VacancyNumber.Contains(s)));
        }

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(1, filter.Page);
        var total = await q.CountAsync(ct);

        var rows =
            from a in q
            join v in Db.Vacancies on a.VacancyId equals v.Id
            join d in Db.Departments on a.DepartmentId equals d.Id
            join dv in Db.Divisions on a.DivisionId equals dv.Id
            select new { App = a, v.VacancyNumber, v.JobTitle, DepartmentName = d.Name, DivisionName = dv.Name };

        // New and under review: oldest first (first come, first served). All: newest first.
        rows = filter.Status is ApplicationStatus.Submitted or ApplicationStatus.UnderReview
            ? rows.OrderBy(x => x.App.SubmittedAt).ThenBy(x => x.App.Id)
            : rows.OrderByDescending(x => x.App.Id);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ApplicationListItemDto(x.App.Id, x.App.ApplicationNumber, x.App.FullName, x.App.VacancyId, x.VacancyNumber,
                x.JobTitle, x.DivisionName, x.DepartmentName, x.App.IsInternal, x.App.Source, x.App.HighestEducation,
                x.App.YearsExperience, x.App.Status, x.App.SubmittedAt))
            .ToListAsync(ct);

        return new PagedResult<ApplicationListItemDto>(items, page, pageSize, total);
    }

    public async Task<ApplicationCounts> CountAsync(RequestVisibility visibility, CancellationToken ct = default)
    {
        var byStatus = await Visible(visibility)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Of(ApplicationStatus s) => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        return new ApplicationCounts(Of(ApplicationStatus.Submitted), Of(ApplicationStatus.UnderReview), byStatus.Sum(x => x.Count));
    }

    public async Task<IReadOnlyList<MyApplicationDto>> ListForApplicantAsync(string userId, CancellationToken ct = default) =>
        await (
                from a in Db.JobApplications.AsNoTracking()
                where a.ApplicantUserId == userId
                join v in Db.Vacancies on a.VacancyId equals v.Id
                join d in Db.Departments on a.DepartmentId equals d.Id
                orderby a.SubmittedAt descending
                select new MyApplicationDto(a.Id, a.ApplicationNumber, v.JobTitle, d.Name, a.Status, a.SubmittedAt, v.ClosingDate))
            .ToListAsync(ct);
}
