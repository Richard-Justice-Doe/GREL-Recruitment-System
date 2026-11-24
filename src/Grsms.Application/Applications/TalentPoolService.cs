using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Interviews;
using Grsms.Application.Notifications;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Interviews;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Applications;

/// <summary>
/// 12 November 2026 (docs/34 quick win): the talent pool. People who applied before and were not hired (not successful,
/// withdrew, declined the offer, or automatically rejected) can be found again by skills, education, experience and how far
/// they got, and invited by email to apply for a vacancy that is open now. Their past applications stay as they were.
///
/// WHO: the pool is shown to people who see every department (Recruitment Officers, HR, MD) and to a Head of Department for
/// their own departments; inviting is for the Recruitment Officer and the HR Manager. A person is invited to a vacancy once.
/// </summary>
public class TalentPoolService(
    IApplicationRepository applications,
    IInterviewRepository interviews,
    IRepository<Vacancy> vacancies,
    IRepository<JobAdvert> adverts,
    INotificationCenter notifications,
    WorkflowMailer mailer,
    IAppLinks links,
    ICurrentUserService me,
    AccessScope scope)
{
    public const int PageSize = 50;

    /// <summary>The newest past applications read at most (one person can have several).</summary>
    private const int MaxApplications = 3000;

    public bool MayView => me.HasPermission(Permissions.ViewApplications);
    public bool MayInvite => me.IsInRole(Roles.RecruitmentOfficer) || me.IsInRole(Roles.HrManager);

    public async Task<PagedResult<TalentPoolPersonDto>> SearchAsync(TalentPoolFilter filter, CancellationToken ct = default)
    {
        if (!MayView) return new([], 1, PageSize, 0);
        var rows = await applications.TalentPoolRowsAsync(filter, await scope.VisibilityAsync(ct), MaxApplications, ct);
        if (rows.Count == 0) return new([], 1, PageSize, 0);

        var hired = await applications.HiredApplicantsAsync(rows.Select(r => r.ApplicantUserId).Distinct().ToList(), ct);
        var interviewed = (await interviews.ListForApplicationsAsync(rows.Select(r => r.ApplicationId).ToList(), ct))
            .Where(i => i.Status == InterviewStatus.Completed).Select(i => i.JobApplicationId).ToHashSet();
        var vacancyIds = rows.Select(r => r.VacancyId).Distinct().ToList();
        var titles = (await vacancies.ListAsync(v => vacancyIds.Contains(v.Id), ct)).ToDictionary(v => v.Id, v => v.JobTitle);

        TalentStage Stage(TalentApplicationRow r) =>
            r.Status == ApplicationStatus.OfferDeclined ? TalentStage.Offered
            : interviewed.Contains(r.ApplicationId) ? TalentStage.Interviewed
            : r.ShortlistedAt is not null ? TalentStage.Shortlisted
            : TalentStage.Applied;
        var minimum = filter.Reached switch
        {
            "shortlisted" => TalentStage.Shortlisted,
            "interviewed" => TalentStage.Interviewed,
            "offered" => TalentStage.Offered,
            _ => TalentStage.Applied
        };

        var people = rows.Where(r => !hired.Contains(r.ApplicantUserId))
            .GroupBy(r => r.ApplicantUserId)
            .Select(g =>
            {
                // Show the person as they were in their furthest-reaching application (then the best score, then the latest).
                var best = g.OrderByDescending(Stage).ThenByDescending(r => r.ScreeningScore ?? -1).ThenByDescending(r => r.SubmittedAt).First();
                var latest = g.OrderByDescending(r => r.SubmittedAt).First();
                return new TalentPoolPersonDto(g.Key, best.FullName, latest.Email, latest.Phone, best.Education, best.FieldOfStudy, best.YearsExperience,
                    best.Skills, best.CurrentPosition, Stage(best), g.Max(r => r.ScreeningScore), titles.GetValueOrDefault(latest.VacancyId, ""),
                    latest.SubmittedAt, g.Count(), latest.ApplicationId);
            })
            .Where(p => p.Reached >= minimum)
            .OrderByDescending(p => p.Reached).ThenByDescending(p => p.BestScore ?? -1).ThenByDescending(p => p.LastApplied)
            .ToList();

        var page = Math.Clamp(filter.Page, 1, Math.Max(1, (int)Math.Ceiling(people.Count / (double)PageSize)));
        return new(people.Skip((page - 1) * PageSize).Take(PageSize).ToList(), page, PageSize, people.Count);
    }

    /// <summary>Vacancies open now with a published advert, which a candidate from the pool can apply to.</summary>
    public async Task<IReadOnlyList<InvitableVacancyDto>> InvitableVacanciesAsync(CancellationToken ct = default)
    {
        if (!MayInvite) return [];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var open = await vacancies.ListAsync(v => v.Status == VacancyStatus.Published && v.ClosingDate >= today && v.VacancyType != VacancyType.Internal, ct);
        var ids = open.Select(v => v.Id).ToList();
        var advertised = (await adverts.ListAsync(a => ids.Contains(a.VacancyId) && a.Status == AdvertStatus.Live, ct)).Select(a => a.VacancyId).ToHashSet();
        return open.Where(v => advertised.Contains(v.Id)).OrderBy(v => v.JobTitle)
            .Select(v => new InvitableVacancyDto(v.Id, v.VacancyNumber, v.JobTitle, v.ClosingDate)).ToList();
    }

    /// <summary>Emails a person from the pool an invitation to apply for an open vacancy (once per person and vacancy).</summary>
    public async Task<Result> InviteAsync(string applicantUserId, int vacancyId, CancellationToken ct = default)
    {
        if (!MayInvite) return Result.Failure("Only a Recruitment Officer or the HR Manager can invite candidates.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await vacancies.GetByIdAsync(vacancyId, ct) is not { Status: VacancyStatus.Published } v || v.ClosingDate < today || v.VacancyType == VacancyType.Internal)
            return Result.Failure("Choose a vacancy that is open to external applicants.");
        var advert = (await adverts.ListAsync(a => a.VacancyId == vacancyId && a.Status == AdvertStatus.Live, ct)).OrderByDescending(a => a.Id).FirstOrDefault();
        if (advert is null) return Result.Failure("This vacancy has no published advert yet, so there is nothing to apply to.");
        var past = await applications.ListAsync(a => a.ApplicantUserId == applicantUserId, ct);
        if (past.Count == 0) return Result.Failure("This person is not in the talent pool.");
        if (past.Any(a => a.VacancyId == vacancyId)) return Result.Failure("This person has already applied for that vacancy.");
        if (!await notifications.ClaimReminderAsync($"talent-invite:{vacancyId}:{applicantUserId}", ct))
            return Result.Failure("This person has already been invited to apply for that vacancy.");

        var name = past.OrderByDescending(a => a.SubmittedAt).First().FullName;
        await mailer.ToUserAsync(applicantUserId, $"GREL is recruiting: {v.JobTitle}",
            $"Dear {WorkflowMailer.Enc(name)},<p>Thank you for your earlier interest in working at GREL. We are now recruiting for " +
            $"<b>{WorkflowMailer.Enc(v.JobTitle)}</b> and think your profile could be a good match. If you are interested, please apply by " +
            $"<b>{v.ClosingDate:d MMMM yyyy}</b>.</p>", links.CareersJob(advert.AdvertNumber), "See the vacancy and apply", ct);
        return Result.Success();
    }
}
