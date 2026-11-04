using Grsms.Application.Adverts;
using Grsms.Application.Common;
using Grsms.Application.Recruitment;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>Job advert data access: details with channels and history, list search, the scheduler's queries and the careers pages.</summary>
public class AdvertRepository(ApplicationDbContext context) : Repository<JobAdvert>(context), IAdvertRepository
{
    public Task<JobAdvert?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        Db.JobAdverts
            .Include(a => a.Channels)
            .Include(a => a.Actions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<JobAdvert?> FindByNumberAsync(string advertNumber, CancellationToken ct = default) =>
        Db.JobAdverts.Include(a => a.Channels).FirstOrDefaultAsync(a => a.AdvertNumber == advertNumber, ct);

    /// <summary>Adverts the user may see: everything for "see all" roles, otherwise their departments' and their own.</summary>
    private IQueryable<JobAdvert> Visible(RequestVisibility visibility)
    {
        var q = Db.JobAdverts.AsNoTracking();
        if (visibility.SeeAll) return q;
        var hodIds = visibility.HodDepartmentIds.ToList();
        return q.Where(a => a.CreatedByUserId == visibility.UserId || hodIds.Contains(a.DepartmentId));
    }

    public async Task<PagedResult<AdvertListItemDto>> SearchAsync(AdvertFilter filter, RequestVisibility visibility, CancellationToken ct = default)
    {
        var q = Visible(visibility);

        if (filter.Status is { } status) q = q.Where(a => a.Status == status);
        if (filter.VacancyId is { } vacancyId) q = q.Where(a => a.VacancyId == vacancyId);
        if (filter.DivisionId is { } divId) q = q.Where(a => a.DivisionId == divId);
        if (filter.DepartmentId is { } deptId) q = q.Where(a => a.DepartmentId == deptId);
        if (filter.Channel is { } channel) q = q.Where(a => a.Channels.Any(c => c.Channel == channel));
        if (filter.From is { } from) q = q.Where(a => a.EndDate >= from);   // still running on or after "from"
        if (filter.To is { } to) q = q.Where(a => a.StartDate <= to);       // started on or before "to"
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(a => a.AdvertNumber.Contains(s) || a.Title.Contains(s) ||
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
            select new { Advert = a, v.VacancyNumber, DepartmentName = d.Name, DivisionName = dv.Name };

        // Live: ending soonest first. Scheduled: starting soonest first. Others: newest first.
        rows = filter.Status switch
        {
            AdvertStatus.Live => rows.OrderBy(x => x.Advert.EndDate).ThenBy(x => x.Advert.Id),
            AdvertStatus.Scheduled => rows.OrderBy(x => x.Advert.StartDate).ThenBy(x => x.Advert.Id),
            _ => rows.OrderByDescending(x => x.Advert.Id)
        };

        var pageRows = await rows.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // Channels for the rows on this page (a second small query, simpler than a grouped join).
        var ids = pageRows.Select(x => x.Advert.Id).ToList();
        var channels = (await Db.AdvertChannels.AsNoTracking().Where(c => ids.Contains(c.JobAdvertId)).ToListAsync(ct))
            .ToLookup(c => c.JobAdvertId);

        var items = pageRows.Select(x => new AdvertListItemDto(
                x.Advert.Id, x.Advert.AdvertNumber, x.Advert.Title, x.Advert.VacancyId, x.VacancyNumber,
                x.DivisionName, x.DepartmentName, x.Advert.Status, x.Advert.StartDate, x.Advert.EndDate,
                channels[x.Advert.Id].Select(c => c.Channel).Order().ToList(),
                x.Advert.Status == AdvertStatus.Live ? channels[x.Advert.Id].Count(c => c.Status == ChannelPostStatus.ToPost) : 0))
            .ToList();

        return new PagedResult<AdvertListItemDto>(items, page, pageSize, total);
    }

    public async Task<AdvertCounts> CountAsync(RequestVisibility visibility, CancellationToken ct = default)
    {
        var byStatus = await Visible(visibility)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Of(AdvertStatus s) => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var liveIds = Visible(visibility).Where(a => a.Status == AdvertStatus.Live).Select(a => a.Id);
        var toPost = await Db.AdvertChannels.CountAsync(c => liveIds.Contains(c.JobAdvertId) && c.Status == ChannelPostStatus.ToPost, ct);

        return new AdvertCounts(Of(AdvertStatus.Live), Of(AdvertStatus.Scheduled), Of(AdvertStatus.Draft), toPost);
    }

    public async Task<IReadOnlyList<JobAdvert>> GetDueToPublishAsync(DateOnly today, CancellationToken ct = default) =>
        await Db.JobAdverts
            .Include(a => a.Channels)
            .Include(a => a.Actions)
            .AsSplitQuery()
            .Where(a => a.Status == AdvertStatus.Scheduled && a.StartDate <= today)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobAdvert>> GetDueToCloseAsync(DateOnly today, CancellationToken ct = default) =>
        await Db.JobAdverts
            .Include(a => a.Channels)
            .Include(a => a.Actions)
            .AsSplitQuery()
            .Where(a => a.Status == AdvertStatus.Live || a.Status == AdvertStatus.Scheduled)
            .Where(a => a.EndDate < today ||
                        Db.Vacancies.Any(v => v.Id == a.VacancyId && (v.Status != VacancyStatus.Published || v.ClosingDate < today)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PublicJobDto>> GetRunningAsync(AdvertChannel channel, DateOnly today, CancellationToken ct = default) =>
        await (
                from a in Db.JobAdverts.AsNoTracking()
                where a.Status == AdvertStatus.Live && a.StartDate <= today && a.EndDate >= today
                where a.Channels.Any(c => c.Channel == channel && c.Status == ChannelPostStatus.Posted)
                join v in Db.Vacancies on a.VacancyId equals v.Id
                where v.Status == VacancyStatus.Published && v.ClosingDate >= today
                join d in Db.Departments on a.DepartmentId equals d.Id
                join dv in Db.Divisions on a.DivisionId equals dv.Id
                join s in Db.Sites on v.SiteId equals s.Id
                orderby a.PublishedAt descending
                select new PublicJobDto(a.AdvertNumber, a.Title, a.Body, a.HowToApply, dv.Name, d.Name, s.Name,
                    v.EmploymentType, v.NumberOfPositions, a.EndDate, channel == AdvertChannel.InternalPortal, a.PublishedAt))
            .ToListAsync(ct);
}
