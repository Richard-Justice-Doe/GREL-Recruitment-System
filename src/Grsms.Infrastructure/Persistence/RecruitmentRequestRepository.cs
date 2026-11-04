using Grsms.Application.Common;
using Grsms.Application.Recruitment;
using Grsms.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// Recruitment-request data access: the generic operations (inherited) plus history loading and the
/// list-page search, which joins departments and users so names appear without extra queries.
/// </summary>
public class RecruitmentRequestRepository(ApplicationDbContext context)
    : Repository<RecruitmentRequest>(context), IRecruitmentRequestRepository
{
    public Task<RecruitmentRequest?> GetWithHistoryAsync(int id, CancellationToken ct = default) =>
        Db.RecruitmentRequests
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<PagedResult<RecruitmentRequestListItemDto>> SearchAsync(
        RecruitmentRequestFilter filter, RequestVisibility visibility,
        IReadOnlyCollection<RecruitmentRequestStatus>? awaitingStatuses, string? excludeRequestedBy,
        CancellationToken ct = default)
    {
        var q = Db.RecruitmentRequests.AsNoTracking();

        // 1. Who may see what (see RecruitmentRequestService for the rules).
        if (!visibility.SeeAll)
        {
            var hodIds = visibility.HodDepartmentIds.ToList();
            q = q.Where(r => r.RequestedByUserId == visibility.UserId || hodIds.Contains(r.DepartmentId));
        }

        // 2. "Awaiting my action".
        if (awaitingStatuses is not null)
        {
            var statuses = awaitingStatuses.ToList();
            q = q.Where(r => statuses.Contains(r.Status));
            if (excludeRequestedBy is not null) q = q.Where(r => r.RequestedByUserId != excludeRequestedBy);
        }

        // 3. Filters typed on the page.
        if (filter.Status is { } status) q = q.Where(r => r.Status == status);
        if (filter.DivisionId is { } divId) q = q.Where(r => r.DivisionId == divId);
        if (filter.DepartmentId is { } deptId) q = q.Where(r => r.DepartmentId == deptId);

        // Recruitment period: the date the request was submitted, or the date it was created for drafts.
        if (filter.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            q = q.Where(r => (r.DateRequested ?? r.CreatedAt) >= start);
        }
        if (filter.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(r => (r.DateRequested ?? r.CreatedAt) < end);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(r => r.RequestNumber.Contains(s) || r.PositionTitle.Contains(s));
        }

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(1, filter.Page);
        var total = await q.CountAsync(ct);

        // Join first (as a plain anonymous shape that SQL can sort), sort and page, and only then build the DTOs.
        var rows =
            from r in q
            join d in Db.Departments on r.DepartmentId equals d.Id
            join v in Db.Divisions on r.DivisionId equals v.Id
            join u in Db.Users on r.RequestedByUserId equals u.Id
            select new { Request = r, DivisionName = v.Name, DepartmentName = d.Name, RequestedByName = u.FullName };

        // Waiting lists show the longest-waiting first; the full list shows the newest first.
        rows = awaitingStatuses is not null
            ? rows.OrderBy(x => x.Request.StatusChangedAt).ThenBy(x => x.Request.Id)
            : rows.OrderByDescending(x => x.Request.Id);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RecruitmentRequestListItemDto(
                x.Request.Id, x.Request.RequestNumber, x.Request.PositionTitle, x.Request.NumberOfPositions,
                x.DivisionName, x.DepartmentName, x.RequestedByName, x.Request.Status, x.Request.DateRequested,
                x.Request.StatusChangedAt, x.Request.EstimatedAnnualCost))
            .ToListAsync(ct);

        return new PagedResult<RecruitmentRequestListItemDto>(items, page, pageSize, total);
    }
}
