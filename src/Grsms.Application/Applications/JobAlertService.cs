using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Notifications;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Applications;

/// <summary>My job alert, as shown on My profile.</summary>
public record JobAlertDto(bool IsActive, string? Keywords);

/// <summary>
/// 12 November 2026 (docs/34 quick win): opt-in job alerts. A person switches their alert on (and optionally gives keywords)
/// on My profile; when an advert goes live on the public careers page (the Company website channel) and its title or text
/// matches, they get one email with the link to apply. Nobody is emailed without having switched it on; each advert is
/// sent to a person once, even if it goes live again.
/// </summary>
public class JobAlertService(
    IRepository<JobAlertSubscription> alerts,
    IApplicationRepository applications,
    INotificationCenter notifications,
    WorkflowMailer mailer,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    ICurrentUserService me)
{
    public const int MaxKeywordsLength = 300;

    public async Task<JobAlertDto> GetMineAsync(CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return new(false, null);
        var mine = (await alerts.ListAsync(a => a.UserId == uid, ct)).FirstOrDefault();
        return new(mine?.IsActive ?? false, mine?.Keywords);
    }

    public async Task<Result> SaveMineAsync(bool active, string? keywords, CancellationToken ct = default)
    {
        if (me.UserId is not { } uid) return Result.Failure("Sign in first.");
        keywords = string.IsNullOrWhiteSpace(keywords) ? null : string.Join(", ",
            keywords.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase));
        if (keywords?.Length > MaxKeywordsLength) return Result.Failure($"Keep the keywords under {MaxKeywordsLength} characters.");
        var mine = (await alerts.ListAsync(a => a.UserId == uid, ct)).FirstOrDefault();
        if (mine is null)
        {
            if (!active) return Result.Success(); // nothing to switch off
            alerts.Add(new JobAlertSubscription { UserId = uid, Keywords = keywords, IsActive = true });
        }
        else (mine.IsActive, mine.Keywords) = (active, keywords);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Emails everyone whose alert matches an advert that has just gone live on the public careers page. Called by the advert
    /// service. Returns how many people were emailed. People who already applied for the vacancy are skipped.
    /// </summary>
    public async Task<int> SendForAdvertAsync(JobAdvert advert, Vacancy vacancy, CancellationToken ct = default)
    {
        if (!advert.Channels.Any(c => c.Channel == AdvertChannel.CompanyWebsite) || vacancy.VacancyType == VacancyType.Internal) return 0;
        var active = await alerts.ListAsync(a => a.IsActive, ct);
        if (active.Count == 0) return 0;
        var text = $"{vacancy.JobTitle} {advert.Title} {advert.Body}";
        var applied = (await applications.ListAsync(a => a.VacancyId == vacancy.Id, ct)).Select(a => a.ApplicantUserId).ToHashSet();
        var sent = 0;
        foreach (var alert in active.Where(a => !applied.Contains(a.UserId) && a.Matches(text)))
        {
            if (!await notifications.ClaimReminderAsync($"job-alert:{advert.Id}:{alert.UserId}", ct)) continue;
            await mailer.ToUserAsync(alert.UserId, $"New vacancy at GREL: {advert.Title}",
                $"A vacancy matching your job alert has just been published: <b>{WorkflowMailer.Enc(advert.Title)}</b>, open until " +
                $"<b>{advert.EndDate:d MMMM yyyy}</b>.<p>You get this email because you switched on job alerts on your GRSMS profile; you can " +
                "switch them off there at any time.</p>", links.CareersJob(advert.AdvertNumber), "See the vacancy and apply", ct);
            alert.LastSentAt = DateTime.UtcNow;
            sent++;
        }
        if (sent > 0) await unitOfWork.SaveChangesAsync(ct);
        return sent;
    }
}
