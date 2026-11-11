using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Recruitment;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Adverts;

/// <summary>The create/edit form for an advert.</summary>
public class AdvertFormDto
{
    /// <summary>0 when creating.</summary>
    public int Id { get; set; }

    [Required]
    public int VacancyId { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Advert title")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(10000, MinimumLength = 50, ErrorMessage = "Write at least 50 characters of advert text.")]
    [Display(Name = "Advert text")]
    public string Body { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    [Display(Name = "How to apply")]
    public string HowToApply { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateOnly? StartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "End date (last day shown)")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Channels")]
    public List<AdvertChannel> Channels { get; set; } = [];

    // ---- Read-only details of the vacancy, filled by the service for the page
    public string? VacancyNumber { get; set; }
    public string? VacancyTitle { get; set; }
    public DateOnly? VacancyClosingDate { get; set; }
    public VacancyType? VacancyType { get; set; }
    public IReadOnlyList<AdvertChannel> AllowedChannels { get; set; } = [];

    /// <summary>The advert is published: saving takes it offline and sends it back for review (shown as a warning).</summary>
    public bool TakesOffline { get; set; }

    /// <summary>What the reviewer asked to correct, if it was returned.</summary>
    public string? ReviewComment { get; set; }
}

/// <summary>Filters on the Job adverts list. Every filter is optional.</summary>
public class AdvertFilter
{
    /// <summary>"live", "scheduled", "review" (HR or IT), "hr", "it", "draft" (with returned ones), "closed" (with rejected ones) or "all".</summary>
    public string Tab { get; set; } = "live";
    public AdvertStatus? Status { get; set; }

    /// <summary>Several statuses at once (set from <see cref="Tab"/> by the service).</summary>
    public IReadOnlyList<AdvertStatus>? Statuses { get; set; }
    public AdvertChannel? Channel { get; set; }
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }
    public int? VacancyId { get; set; }

    /// <summary>Recruitment period: adverts running on or after this day (by start date).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Recruitment period: adverts starting on or before this day.</summary>
    public DateOnly? To { get; set; }

    /// <summary>Part of the advert number, vacancy number or title.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

/// <summary>One row on the Job adverts list.</summary>
public record AdvertListItemDto(
    int Id,
    string AdvertNumber,
    string Title,
    int VacancyId,
    string VacancyNumber,
    string DivisionName,
    string DepartmentName,
    AdvertStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<AdvertChannel> Channels,
    int ChannelsToPost);

/// <summary>One channel of an advert on the details page, with copy-ready text for external channels.</summary>
public record AdvertChannelDto(
    int Id,
    AdvertChannel Channel,
    ChannelPostStatus Status,
    bool IsAutomatic,
    string? Reference,
    DateTime? PostedAt,
    string? PostedByName,
    DateTime? RemovedAt,
    string? CopyText);

public record AdvertHistoryDto(AdvertAction Action, AdvertStatus FromStatus, AdvertStatus ToStatus, string ActorName, string? Comments, DateTime ActionAt);

/// <summary>Everything on the advert details page, plus what the current user may do.</summary>
public record AdvertDetailsDto
{
    public int Id { get; init; }
    public string AdvertNumber { get; init; } = "";
    public AdvertStatus Status { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public string HowToApply { get; init; } = "";
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? ClosedAt { get; init; }

    public int VacancyId { get; init; }
    public string VacancyNumber { get; init; } = "";
    public VacancyStatus VacancyStatus { get; init; }
    public DateOnly VacancyClosingDate { get; init; }
    public string DivisionName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string RequestingHodName { get; init; } = "";
    public string CreatedByName { get; init; } = "";
    public int? ClonedFromAdvertId { get; init; }
    public string? ClonedFromAdvertNumber { get; init; }

    /// <summary>Public careers page address, when the advert runs on the company website.</summary>
    public string? PublicLink { get; init; }

    public IReadOnlyList<AdvertChannelDto> Channels { get; init; } = [];
    public IReadOnlyList<AdvertHistoryDto> History { get; init; } = [];

    // ---- Review (11 Nov 2026, docs/29 change 2)
    public string? SubmittedByName { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public string? HrReviewedByName { get; init; }
    public DateTime? HrReviewedAt { get; init; }
    public string? ItApprovedByName { get; init; }
    public DateTime? ItApprovedAt { get; init; }

    /// <summary>The reason given with the last return, rejection or taking offline.</summary>
    public string? ReviewComment { get; init; }

    // ---- What the current user may do
    public bool CanEdit { get; init; }

    /// <summary>Saving changes takes the published advert offline and sends it back for review.</summary>
    public bool EditTakesOffline { get; init; }

    /// <summary>Send it for the HR review (the author).</summary>
    public bool CanSubmit { get; init; }

    /// <summary>The HR review: approve it for the IT Manager.</summary>
    public bool CanHrApprove { get; init; }

    /// <summary>The IT approval, which publishes it (now or on its start date).</summary>
    public bool CanPublish { get; init; }

    public bool CanReturn { get; init; }
    public bool CanReject { get; init; }

    /// <summary>Take a published or scheduled advert offline (IT).</summary>
    public bool CanUnpublish { get; init; }
    public bool CanUnschedule { get; init; }
    public bool CanClose { get; init; }
    public bool CanClone { get; init; }
    public bool CanManageChannels { get; init; }

    /// <summary>Record an external channel as taken down (the IT Manager, also after the advert closed or went offline).</summary>
    public bool CanRecordRemoval { get; init; }

    /// <summary>Why the advert cannot be sent for review or published right now (e.g. the vacancy is not published), if so.</summary>
    public string? PublishBlockedReason { get; init; }
}

/// <summary>One advert waiting for the current user (dashboard to-dos).</summary>
public record AdvertWorkItem(int Id, string AdvertNumber, string Title, AdvertStatus Status, string Action, DateTime Since);

/// <summary>The IT Manager's advert cards on the dashboard.</summary>
public record AdvertItSummary(int AwaitingApproval, int Scheduled, int Active, int Expired);

/// <summary>A running advert as shown on the careers page (public) or internal jobs page (staff).</summary>
public record PublicJobDto(
    string AdvertNumber,
    string Title,
    string Body,
    string HowToApply,
    string DivisionName,
    string DepartmentName,
    string SiteName,
    EmploymentType EmploymentType,
    int NumberOfPositions,
    DateOnly ClosingDate,
    bool InternalOnly,
    DateTime? PublishedAt);

/// <summary>Number of adverts per tab on the list page.</summary>
/// <param name="Draft">Drafts and adverts returned for correction.</param>
/// <param name="Closed">Closed and rejected adverts.</param>
public record AdvertCounts(int Live, int Scheduled, int Draft, int ChannelsToPost, int AwaitingHr = 0, int AwaitingIt = 0, int Closed = 0)
{
    public int InReview => AwaitingHr + AwaitingIt;
}

/// <summary>Data access for adverts beyond the generic repository.</summary>
public interface IAdvertRepository : IRepository<JobAdvert>
{
    /// <summary>An advert with its channels and history, or null.</summary>
    Task<JobAdvert?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>An advert by its number (e.g. ADV-2026-0001), with its channels, or null.</summary>
    Task<JobAdvert?> FindByNumberAsync(string advertNumber, CancellationToken ct = default);

    /// <summary>The list page, limited to what the user may see.</summary>
    Task<PagedResult<AdvertListItemDto>> SearchAsync(AdvertFilter filter, RequestVisibility visibility, CancellationToken ct = default);

    /// <summary>Per-tab counts for the user.</summary>
    Task<AdvertCounts> CountAsync(RequestVisibility visibility, CancellationToken ct = default);

    /// <summary>Scheduled adverts whose start date has arrived (with channels and history loaded).</summary>
    Task<IReadOnlyList<JobAdvert>> GetDueToPublishAsync(DateOnly today, CancellationToken ct = default);

    /// <summary>Live or scheduled adverts past their end date, or whose vacancy is no longer published.</summary>
    Task<IReadOnlyList<JobAdvert>> GetDueToCloseAsync(DateOnly today, CancellationToken ct = default);

    /// <summary>Adverts running today on <paramref name="channel"/>, newest first.</summary>
    Task<IReadOnlyList<PublicJobDto>> GetRunningAsync(AdvertChannel channel, DateOnly today, CancellationToken ct = default);
}
