using Grsms.Domain.Common;

namespace Grsms.Domain.Advertisements;

/// <summary>
/// A job advertisement for a published vacancy (Module 4).
///
/// One vacancy can have several adverts over time (for example a first round, then a clone to re-advertise
/// after the vacancy is reopened). Each advert runs on one or more <see cref="AdvertChannel"/>s between its
/// start and end dates. See <see cref="AdvertWorkflow"/> for the rules.
///
/// Like every recruitment record, it stores the division, department and requesting Head of Department,
/// copied from its vacancy, so reports can be filtered by them.
/// </summary>
public class JobAdvert : BaseEntity
{
    /// <summary>Reference, e.g. "ADV-2026-0001". Unique.</summary>
    public string AdvertNumber { get; set; } = string.Empty;

    /// <summary>The vacancy being advertised (Vacancies.Id). It must be published before the advert can go live.</summary>
    public int VacancyId { get; set; }

    // ---------------------------------------------------------------- Links copied from the vacancy

    public int DivisionId { get; set; }
    public int DepartmentId { get; set; }
    public string RequestingHodUserId { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Content

    /// <summary>Headline, normally the job title, e.g. "Safety Officer – Factory APM".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The full advert text shown to applicants (pre-filled from the vacancy, then edited freely).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Application instructions, e.g. "Apply online through the GREL careers page".</summary>
    public string HowToApply { get; set; } = string.Empty;

    // 13 Nov 2026 (docs/36 AI-1): optional versions for other channels (written by hand or with AI). Empty = built from Body.

    /// <summary>Text for job boards and newspapers (Indeed, Jobberman, newspaper) instead of <see cref="Body"/>.</summary>
    public string? ExternalText { get; set; }

    /// <summary>Text for the LinkedIn post instead of <see cref="Body"/>.</summary>
    public string? LinkedInText { get; set; }

    /// <summary>An internal vacancy notice for staff (noticeboards, staff email), shown copy-ready on the advert page.</summary>
    public string? InternalNotice { get; set; }

    // ---------------------------------------------------------------- Dates and state

    /// <summary>First day the advert runs. If it is in the future when published, the advert is Scheduled until then.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day the advert runs (inclusive). Never later than the vacancy's closing date.</summary>
    public DateOnly EndDate { get; set; }

    public AdvertStatus Status { get; set; } = AdvertStatus.Draft;
    public DateTime StatusChangedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>The Recruitment Officer / HR Manager who created it (Users.Id).</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Review (11 Nov 2026, docs/29 change 2): RO → HR Manager → IT Manager

    /// <summary>Who last sent it for review (Users.Id) and when. Null for adverts published before the review existed.</summary>
    public string? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }

    /// <summary>The HR Manager who last reviewed it and sent it for IT approval.</summary>
    public string? HrReviewedByUserId { get; set; }
    public DateTime? HrReviewedAt { get; set; }

    /// <summary>The IT Manager who last approved (published) it.</summary>
    public string? ItApprovedByUserId { get; set; }
    public DateTime? ItApprovedAt { get; set; }

    /// <summary>The reason given with the last return, rejection or taking offline (also in the history). Cleared on resubmission.</summary>
    public string? ReviewComment { get; set; }

    /// <summary>The advert this one was cloned from, if any (JobAdverts.Id).</summary>
    public int? ClonedFromAdvertId { get; set; }

    /// <summary>Concurrency stamp; changes on every step.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<AdvertChannelPost> Channels { get; set; } = new List<AdvertChannelPost>();
    public ICollection<AdvertActionEntry> Actions { get; set; } = new List<AdvertActionEntry>();

    /// <summary>Moves to <paramref name="newStatus"/> and records the step in the history.</summary>
    /// <param name="actorUserId">Null when the system acts on its own (scheduled publishing, automatic closing).</param>
    public void RecordStep(AdvertAction action, AdvertStatus newStatus, string? actorUserId, string actorName, string? comments, DateTime now)
    {
        Actions.Add(new AdvertActionEntry
        {
            Action = action,
            FromStatus = Status,
            ToStatus = newStatus,
            ActorUserId = actorUserId,
            ActorName = actorName,
            Comments = comments,
            ActionAt = now
        });
        if (newStatus != Status) StatusChangedAt = now;
        Status = newStatus;
        Version = Guid.NewGuid();
    }
}

/// <summary>
/// One channel of an advert and where it stands: the internal portal and company website are posted automatically
/// when the advert goes live; LinkedIn, Indeed, Jobberman and newspapers are posted by the recruiter using the
/// copy-ready text, who then records the link or newspaper edition here.
/// </summary>
public class AdvertChannelPost
{
    public int Id { get; set; }
    public int JobAdvertId { get; set; }
    public AdvertChannel Channel { get; set; }
    public ChannelPostStatus Status { get; set; } = ChannelPostStatus.ToPost;

    /// <summary>Link to the posting, or the newspaper name and edition, e.g. "Daily Graphic, 12 Nov 2026, p. 34".</summary>
    public string? Reference { get; set; }

    public DateTime? PostedAt { get; set; }
    public string? PostedByName { get; set; }
    public DateTime? RemovedAt { get; set; }
}

/// <summary>One line of an advert's history. Never edited.</summary>
public class AdvertActionEntry : BaseEntity
{
    public int JobAdvertId { get; set; }
    public AdvertAction Action { get; set; }
    public AdvertStatus FromStatus { get; set; }
    public AdvertStatus ToStatus { get; set; }

    /// <summary>Who acted (Users.Id), or null for the system.</summary>
    public string? ActorUserId { get; set; }

    public string ActorName { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ActionAt { get; set; }
}
