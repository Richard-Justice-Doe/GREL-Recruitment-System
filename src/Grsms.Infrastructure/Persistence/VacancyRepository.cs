using Grsms.Application.Common;
using Grsms.Application.Recruitment;
using Grsms.Application.Vacancies;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>Vacancy data access: details with requirements and history, list search, and the look-ups other modules need.</summary>
public class VacancyRepository(ApplicationDbContext context) : Repository<Vacancy>(context), IVacancyRepository
{
    public Task<Vacancy?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        Db.Vacancies
            .Include(v => v.Requirements)
            .Include(v => v.KnockoutQuestions) // Phase 15
            .Include(v => v.Actions)
            .AsSplitQuery() // two small queries instead of one wide join of requirements × history
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<(int Id, string Number)?> FindForRequestAsync(int recruitmentRequestId, CancellationToken ct = default)
    {
        var hit = await Db.Vacancies.AsNoTracking()
            .Where(v => v.RecruitmentRequestId == recruitmentRequestId)
            .Select(v => new { v.Id, v.VacancyNumber })
            .FirstOrDefaultAsync(ct);
        return hit is null ? null : (hit.Id, hit.VacancyNumber);
    }

    public async Task<PagedResult<VacancyListItemDto>> SearchAsync(VacancyFilter filter, RequestVisibility visibility,
        IReadOnlyCollection<VacancyStatus>? statuses, CancellationToken ct = default)
    {
        var q = Db.Vacancies.AsNoTracking();

        if (!visibility.SeeAll)
        {
            var hodIds = visibility.HodDepartmentIds.ToList();
            q = q.Where(v => v.RecruiterUserId == visibility.UserId || hodIds.Contains(v.DepartmentId));
        }
        if (statuses is not null)
        {
            var list = statuses.ToList();
            q = q.Where(v => list.Contains(v.Status));
        }
        if (filter.Status is { } status) q = q.Where(v => v.Status == status);
        if (filter.DivisionId is { } divId) q = q.Where(v => v.DivisionId == divId);
        if (filter.DepartmentId is { } deptId) q = q.Where(v => v.DepartmentId == deptId);

        // Recruitment period: the day the vacancy was published, or the day it was created if never published.
        if (filter.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            q = q.Where(v => (v.PublishedAt ?? v.CreatedAt) >= start);
        }
        if (filter.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(v => (v.PublishedAt ?? v.CreatedAt) < end);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(v => v.VacancyNumber.Contains(s) || v.JobTitle.Contains(s));
        }

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(1, filter.Page);
        var total = await q.CountAsync(ct);

        var rows =
            from v in q
            join d in Db.Departments on v.DepartmentId equals d.Id
            join dv in Db.Divisions on v.DivisionId equals dv.Id
            join u in Db.Users on v.RecruiterUserId equals u.Id
            select new { Vacancy = v, DivisionName = dv.Name, DepartmentName = d.Name, RecruiterName = u.FullName };

        // Open: closing soonest first. Pending: waiting longest first. All: newest first.
        rows = filter.Tab switch
        {
            "open" => rows.OrderBy(x => x.Vacancy.ClosingDate).ThenBy(x => x.Vacancy.Id),
            "pending" => rows.OrderBy(x => x.Vacancy.StatusChangedAt).ThenBy(x => x.Vacancy.Id),
            _ => rows.OrderByDescending(x => x.Vacancy.Id)
        };

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VacancyListItemDto(
                x.Vacancy.Id, x.Vacancy.VacancyNumber, x.Vacancy.JobTitle, x.DivisionName, x.DepartmentName, x.Vacancy.NumberOfPositions,
                x.Vacancy.VacancyType, x.Vacancy.Status, x.Vacancy.ClosingDate, x.RecruiterName, x.Vacancy.StatusChangedAt))
            .ToListAsync(ct);

        return new PagedResult<VacancyListItemDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ApprovedRequestDto>> GetApprovedRequestsWithoutVacancyAsync(RequestVisibility visibility, CancellationToken ct = default)
    {
        var q = Db.RecruitmentRequests.AsNoTracking()
            .Where(r => r.Status == RecruitmentRequestStatus.Approved)
            .Where(r => !Db.Vacancies.Any(v => v.RecruitmentRequestId == r.Id));

        if (!visibility.SeeAll)
        {
            var hodIds = visibility.HodDepartmentIds.ToList();
            q = q.Where(r => r.RequestedByUserId == visibility.UserId || hodIds.Contains(r.DepartmentId));
        }

        return await (
                from r in q
                join d in Db.Departments on r.DepartmentId equals d.Id
                from u in Db.Users.Where(u => u.Id == r.AssignedRecruiterUserId).DefaultIfEmpty() // LEFT JOIN: recruiter may be empty
                orderby r.ApprovedAt
                select new ApprovedRequestDto(r.Id, r.RequestNumber, r.PositionTitle, r.NumberOfPositions, d.Name, r.ApprovedAt,
                    u == null ? null : u.FullName))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Vacancy>> GetExpiredPublishedAsync(DateOnly today, CancellationToken ct = default) =>
        await Db.Vacancies
            .Include(v => v.Actions)
            .Where(v => v.Status == VacancyStatus.Published && v.ClosingDate < today)
            .ToListAsync(ct);
}
