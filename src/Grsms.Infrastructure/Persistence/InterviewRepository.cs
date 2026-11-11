using Grsms.Application.Common;
using Grsms.Application.Interviews;
using Grsms.Application.Recruitment;
using Grsms.Domain.Interviews;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>Interview data access: a session with panel, criteria and scorecards; sessions per application; the list search.</summary>
public class InterviewRepository(ApplicationDbContext context) : Repository<Interview>(context), IInterviewRepository
{
    public Task<Interview?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        Db.Interviews
            .Include(i => i.Panel)
            .Include(i => i.Criteria)
            .Include(i => i.Scorecards).ThenInclude(s => s.Ratings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<Interview>> ListForApplicationsAsync(IReadOnlyCollection<int> applicationIds, CancellationToken ct = default)
    {
        var ids = applicationIds.ToList();
        return await Db.Interviews.AsNoTracking()
            .Include(i => i.Panel)
            .Include(i => i.Scorecards)
            .AsSplitQuery()
            .Where(i => ids.Contains(i.JobApplicationId))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<InterviewListItemDto>> SearchAsync(InterviewFilter filter, RequestVisibility visibility, string? panelUserId,
        bool onlyAwaitingMyScorecard, DateTime now, CancellationToken ct = default)
    {
        var q = Db.Interviews.AsNoTracking();

        if (panelUserId is not null)
            q = q.Where(i => i.Panel.Any(p => p.UserId == panelUserId));
        else if (!visibility.SeeAll)
        {
            var hodIds = visibility.HodDepartmentIds.ToList();
            q = q.Where(i => hodIds.Contains(i.DepartmentId));
        }
        // Waiting for my scorecard: the session has started, is still open, I am expected to score (not marked absent or
        // excused) and I have no valid scorecard (none yet, or the Recruitment Officer returned mine for correction).
        if (onlyAwaitingMyScorecard && panelUserId is not null)
            q = q.Where(i => i.Status == InterviewStatus.Scheduled && i.StartsAt <= now
                && i.Panel.Any(p => p.UserId == panelUserId && p.Attendance != PanelAttendance.Absent && p.Attendance != PanelAttendance.Excused)
                && !i.Scorecards.Any(s => s.PanelUserId == panelUserId && s.ReturnedAt == null));

        if (filter.Status is { } status) q = q.Where(i => i.Status == status);
        if (filter.VacancyId is { } vacancyId) q = q.Where(i => i.VacancyId == vacancyId);
        if (filter.DivisionId is { } divId) q = q.Where(i => i.DivisionId == divId);
        if (filter.DepartmentId is { } deptId) q = q.Where(i => i.DepartmentId == deptId);
        if (filter.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            q = q.Where(i => i.StartsAt >= start);
        }
        if (filter.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(i => i.StartsAt < end);
        }

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(1, filter.Page);
        var total = await q.CountAsync(ct);

        var rows =
            from i in q
            join a in Db.JobApplications on i.JobApplicationId equals a.Id
            join v in Db.Vacancies on i.VacancyId equals v.Id
            join d in Db.Departments on i.DepartmentId equals d.Id
            select new
            {
                Interview = i, a.FullName, v.VacancyNumber, v.JobTitle, DepartmentName = d.Name,
                PanelSize = i.Panel.Count, Cards = i.Scorecards.Count(s => s.ReturnedAt == null),
                // Scores show in lists only once the Recruitment Officer has validated them.
                Average = i.Status == InterviewStatus.Completed
                    ? i.Scorecards.Where(s => s.ReturnedAt == null).Average(s => (decimal?)s.WeightedScore)
                    : null
            };

        // Scheduled sessions soonest first; everything else newest first.
        rows = filter.Status == InterviewStatus.Scheduled || onlyAwaitingMyScorecard
            ? rows.OrderBy(x => x.Interview.StartsAt)
            : rows.OrderByDescending(x => x.Interview.StartsAt);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InterviewListItemDto(x.Interview.Id, x.Interview.JobApplicationId, x.FullName, x.VacancyNumber, x.JobTitle,
                x.DepartmentName, x.Interview.Round, x.Interview.Type, x.Interview.Mode, x.Interview.StartsAt, x.Interview.Location,
                x.Interview.Status, x.PanelSize, x.Cards, x.Average))
            .ToListAsync(ct);

        return new PagedResult<InterviewListItemDto>(items, page, pageSize, total);
    }
}
