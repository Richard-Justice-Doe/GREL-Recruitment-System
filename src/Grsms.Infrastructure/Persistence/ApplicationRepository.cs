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
        Db.JobApplications.Include(a => a.Actions).Include(a => a.KnockoutAnswers).AsSplitQuery().FirstOrDefaultAsync(a => a.Id == id, ct);

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
        rows = filter.Status is ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.AutoRejected
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
        return new ApplicationCounts(Of(ApplicationStatus.Submitted), Of(ApplicationStatus.UnderReview), byStatus.Sum(x => x.Count), Of(ApplicationStatus.AutoRejected));
    }

    private static readonly ApplicationStatus[] PoolStatuses =
        [ApplicationStatus.NotSuccessful, ApplicationStatus.Withdrawn, ApplicationStatus.OfferDeclined, ApplicationStatus.AutoRejected];

    public async Task<IReadOnlyList<TalentApplicationRow>> TalentPoolRowsAsync(TalentPoolFilter filter, RequestVisibility visibility, int max,
        CancellationToken ct = default)
    {
        var q = Db.JobApplications.AsNoTracking().Where(a => PoolStatuses.Contains(a.Status));
        if (!visibility.SeeAll)
        {
            var departments = visibility.HodDepartmentIds.ToList();
            q = q.Where(a => departments.Contains(a.DepartmentId));
        }
        if (filter.MinEducation is { } min)
        {
            // Stored as text, so "at least" is the list of levels from min upwards.
            var levels = Enum.GetValues<Grsms.Domain.Vacancies.EducationLevel>().Where(l => l >= min).ToList();
            q = q.Where(a => levels.Contains(a.HighestEducation));
        }
        if (filter.MinYears is { } years) q = q.Where(a => a.YearsExperience >= years);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var like = "%" + filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(a => EF.Functions.Like(a.FullName, like, "\\") || EF.Functions.Like(a.Skills!, like, "\\") || EF.Functions.Like(a.FieldOfStudy!, like, "\\")
                             || EF.Functions.Like(a.CurrentPosition!, like, "\\") || EF.Functions.Like(a.Certifications!, like, "\\"));
        }
        return await q.OrderByDescending(a => a.SubmittedAt).Take(max)
            .Select(a => new TalentApplicationRow(a.Id, a.ApplicantUserId, a.FullName, a.Email, a.Phone, a.HighestEducation, a.FieldOfStudy, a.YearsExperience,
                a.Skills, a.CurrentPosition, a.VacancyId, a.Status, a.ScreeningScore, a.ShortlistedAt, a.SubmittedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlySet<string>> HiredApplicantsAsync(IReadOnlyCollection<string> userIds, CancellationToken ct = default)
    {
        var ids = userIds.ToList();
        return (await Db.JobApplications.AsNoTracking().Where(a => ids.Contains(a.ApplicantUserId) && a.Status == ApplicationStatus.Hired)
            .Select(a => a.ApplicantUserId).Distinct().ToListAsync(ct)).ToHashSet();
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
