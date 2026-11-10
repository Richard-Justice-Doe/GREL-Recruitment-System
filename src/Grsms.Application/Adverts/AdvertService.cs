using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Users;
using Grsms.Application.Vacancies;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Organization;
using Grsms.Domain.Vacancies;
using static Grsms.Application.Common.Text;

namespace Grsms.Application.Adverts;

/// <summary>
/// Job Advertisement Management (Module 4): create, edit, schedule, publish, close and clone adverts for published
/// vacancies, and track each channel.
///
/// WHO MAY DO WHAT (every rule is checked here, on the server):
///   * Create / edit / publish / close / clone / record channel postings : Recruitment Officer or HR Manager.
///   * See                                                               : RO, HR Manager, MD, HR Administrator (all);
///                                                                         a HOD (their departments' adverts).
///   * Scheduled publishing and automatic closing                         : the system (see <see cref="RunScheduleAsync"/>).
///
/// The internal portal and company website are published by GRSMS itself when the advert goes live. LinkedIn, Indeed,
/// Jobberman and newspapers have no connection to GRSMS: the recruiter posts the copy-ready text and records the link.
/// </summary>
public class AdvertService(
    IAdvertRepository adverts,
    IVacancyRepository vacancies,
    IRepository<Division> divisions,
    IRepository<Department> departments,
    IRepository<Site> sites,
    IUserDirectory users,
    INumberGenerator numbers,
    IUnitOfWork unitOfWork,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links)
{
    private const string ConflictMessage = "Someone else updated this advert at the same moment. Please reload the page and try again.";

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private bool IsManager => AdvertWorkflow.ManagerRoles.Any(scope.IsInRole);

    // ===================================================================== Lists

    public async Task<PagedResult<AdvertListItemDto>> SearchAsync(AdvertFilter filter, CancellationToken ct = default)
    {
        filter.Status = filter.Tab switch
        {
            "live" => AdvertStatus.Live,
            "scheduled" => AdvertStatus.Scheduled,
            "draft" => AdvertStatus.Draft,
            _ => filter.Status
        };
        return await adverts.SearchAsync(filter, await scope.VisibilityAsync(ct), ct);
    }

    public async Task<AdvertCounts> CountsAsync(CancellationToken ct = default) =>
        await adverts.CountAsync(await scope.VisibilityAsync(ct), ct);

    /// <summary>The adverts of one vacancy (for the vacancy page).</summary>
    public async Task<IReadOnlyList<AdvertListItemDto>> ListForVacancyAsync(int vacancyId, CancellationToken ct = default) =>
        (await adverts.SearchAsync(new AdvertFilter { Tab = "all", VacancyId = vacancyId, PageSize = 100 }, await scope.VisibilityAsync(ct), ct)).Items;

    /// <summary>True when the current user may create adverts for this vacancy right now.</summary>
    public async Task<bool> CanCreateForAsync(int vacancyId, CancellationToken ct = default) =>
        IsManager && await vacancies.GetByIdAsync(vacancyId, ct) is { } v && AdvertWorkflow.VacancyAllowsAdvertising(v, Today);

    // ===================================================================== Details

    public async Task<AdvertDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var a = await adverts.GetWithDetailsAsync(id, ct);
        if (a is null || !AccessScope.CanSee(await scope.VisibilityAsync(ct), a.DepartmentId, a.CreatedByUserId)) return null;

        var v = (await vacancies.GetByIdAsync(a.VacancyId, ct))!;
        var names = await users.GetDisplayNamesAsync([a.RequestingHodUserId, a.CreatedByUserId], ct);
        var source = a.ClonedFromAdvertId is int sid ? await adverts.GetByIdAsync(sid, ct) : null;
        var link = links.CareersJob(a.AdvertNumber);
        var publishBlocked = AdvertWorkflow.CanPublish(a.Status) ? PublishProblem(a, v) : null;

        return new AdvertDetailsDto
        {
            Id = a.Id,
            AdvertNumber = a.AdvertNumber,
            Status = a.Status,
            Title = a.Title,
            Body = a.Body,
            HowToApply = a.HowToApply,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            PublishedAt = a.PublishedAt,
            ClosedAt = a.ClosedAt,
            VacancyId = v.Id,
            VacancyNumber = v.VacancyNumber,
            VacancyStatus = v.Status,
            VacancyClosingDate = v.ClosingDate,
            DivisionName = (await divisions.GetByIdAsync(a.DivisionId, ct))?.Name ?? "?",
            DepartmentName = (await departments.GetByIdAsync(a.DepartmentId, ct))?.Name ?? "?",
            RequestingHodName = names.GetValueOrDefault(a.RequestingHodUserId) ?? "?",
            CreatedByName = names.GetValueOrDefault(a.CreatedByUserId) ?? "?",
            ClonedFromAdvertId = source?.Id,
            ClonedFromAdvertNumber = source?.AdvertNumber,
            PublicLink = a.Status == AdvertStatus.Live && a.Channels.Any(c => c.Channel == AdvertChannel.CompanyWebsite) ? link : null,
            Channels = a.Channels
                .OrderBy(c => c.Channel)
                .Select(c => new AdvertChannelDto(c.Id, c.Channel, c.Status, AdvertWorkflow.IsAutomatic(c.Channel), c.Reference,
                    c.PostedAt, c.PostedByName, c.RemovedAt,
                    AdvertWorkflow.IsAutomatic(c.Channel) ? null : AdvertText.ForChannel(c.Channel, a, link)))
                .ToList(),
            History = a.Actions.OrderBy(x => x.ActionAt).ThenBy(x => x.Id)
                .Select(x => new AdvertHistoryDto(x.Action, x.FromStatus, x.ToStatus, x.ActorName, x.Comments, x.ActionAt)).ToList(),
            CanEdit = IsManager && AdvertWorkflow.IsEditable(a.Status),
            CanPublish = IsManager && AdvertWorkflow.CanPublish(a.Status) && publishBlocked is null,
            PublishBlockedReason = IsManager ? publishBlocked : null,
            CanUnschedule = IsManager && AdvertWorkflow.CanUnschedule(a.Status),
            CanClose = IsManager && AdvertWorkflow.CanClose(a.Status),
            CanClone = IsManager,
            CanManageChannels = IsManager && a.Status == AdvertStatus.Live
        };
    }

    // ===================================================================== Create / edit

    /// <summary>A new advert form, pre-filled from the vacancy. Null if the user may not advertise it.</summary>
    public async Task<Result<AdvertFormDto>> GetNewFormAsync(int vacancyId, CancellationToken ct = default)
    {
        var (v, error) = await LoadVacancyForAdvertAsync(vacancyId, ct);
        if (v is null) return Result.Failure<AdvertFormDto>(error!);

        var dept = await departments.GetByIdAsync(v.DepartmentId, ct);
        var site = await sites.GetByIdAsync(v.SiteId, ct);
        var form = new AdvertFormDto
        {
            VacancyId = v.Id,
            Title = $"{v.JobTitle} – {dept?.Name}",
            Body = AdvertText.DefaultBody(v, dept?.Name ?? "", site?.Name ?? ""),
            HowToApply = AdvertText.DefaultHowToApply(v.ClosingDate),
            StartDate = Today,
            EndDate = v.ClosingDate,
            Channels = [.. AdvertWorkflow.AllowedChannels(v.VacancyType).Where(c => AdvertWorkflow.IsAutomatic(c))]
        };
        FillVacancyFields(form, v);
        return Result.Success(form);
    }

    /// <summary>Loads an advert into the edit form, or null if it cannot be edited by this user.</summary>
    public async Task<AdvertFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        if (!IsManager) return null;
        var a = await adverts.GetWithDetailsAsync(id, ct);
        if (a is null || !AdvertWorkflow.IsEditable(a.Status)) return null;
        var v = (await vacancies.GetByIdAsync(a.VacancyId, ct))!;

        var form = new AdvertFormDto
        {
            Id = a.Id, VacancyId = a.VacancyId, Title = a.Title, Body = a.Body, HowToApply = a.HowToApply,
            StartDate = a.StartDate, EndDate = a.EndDate, Channels = [.. a.Channels.Select(c => c.Channel).Order()]
        };
        FillVacancyFields(form, v);
        return form;
    }

    /// <summary>Fills the vacancy details shown on the form (also after a failed post).</summary>
    public async Task FillFormDetailsAsync(AdvertFormDto form, CancellationToken ct = default)
    {
        if (await vacancies.GetByIdAsync(form.VacancyId, ct) is { } v) FillVacancyFields(form, v);
    }

    public async Task<Result<int>> CreateAsync(AdvertFormDto form, CancellationToken ct = default)
    {
        var (v, error) = await LoadVacancyForAdvertAsync(form.VacancyId, ct);
        if (v is null) return Result.Failure<int>(error!);

        Normalize(form);
        var errors = Validate(form, v);
        if (errors.Count > 0) return Result.Failure<int>([.. errors]);

        var number = await numbers.NextAsync($"ADV-{DateTime.UtcNow.Year}", ct); // before Add: it saves its own change
        var now = DateTime.UtcNow;
        var a = NewAdvert(v, $"ADV-{now.Year}-{number:0000}", now);
        Apply(form, a);
        a.RecordStep(AdvertAction.Created, AdvertStatus.Draft, scope.UserId, scope.UserName, $"For vacancy {v.VacancyNumber}", now);

        adverts.Add(a);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(a.Id);
    }

    public async Task<Result> UpdateAsync(AdvertFormDto form, CancellationToken ct = default)
    {
        if (!IsManager) return Result.Failure("Only a Recruitment Officer or HR Manager can edit adverts.");
        var a = await adverts.GetWithDetailsAsync(form.Id, ct);
        if (a is null) return Result.Failure("Advert not found.");
        if (!AdvertWorkflow.IsEditable(a.Status)) return Result.Failure("Only draft or scheduled adverts can be edited.");
        var v = (await vacancies.GetByIdAsync(a.VacancyId, ct))!;

        Normalize(form);
        var errors = Validate(form, v);
        // A scheduled advert stays scheduled, so its start date must still be in the future.
        if (a.Status == AdvertStatus.Scheduled && form.StartDate <= Today)
            errors.Add("A scheduled advert must start after today. To publish it now, cancel the schedule and publish.");
        if (errors.Count > 0) return Result.Failure([.. errors]);

        Apply(form, a);
        a.Version = Guid.NewGuid();
        return await SaveAsync(ct);
    }

    // ===================================================================== Publish / schedule / close

    /// <summary>
    /// Publishes a draft: live straight away if the start date is today (or earlier), otherwise scheduled for that day.
    /// The vacancy must be published (approval complete) and still open.
    /// </summary>
    public async Task<Result> PublishAsync(int id, CancellationToken ct = default)
    {
        var (a, error) = await LoadForManagerAsync(id, AdvertWorkflow.CanPublish, "Only draft adverts can be published.", ct);
        if (a is null) return Result.Failure(error!);
        var v = (await vacancies.GetByIdAsync(a.VacancyId, ct))!;
        if (PublishProblem(a, v) is { } problem) return Result.Failure(problem);

        var now = DateTime.UtcNow;
        if (a.StartDate > Today)
        {
            a.RecordStep(AdvertAction.Scheduled, AdvertStatus.Scheduled, scope.UserId, scope.UserName,
                $"Goes live on {a.StartDate:dd MMM yyyy}", now);
            return await SaveAsync(ct);
        }

        GoLive(a, AdvertAction.Published, scope.UserId, scope.UserName, now);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded) await NotifyLiveAsync(a, ct);
        return saved;
    }

    /// <summary>Takes a scheduled advert back to Draft.</summary>
    public async Task<Result> UnscheduleAsync(int id, CancellationToken ct = default)
    {
        var (a, error) = await LoadForManagerAsync(id, AdvertWorkflow.CanUnschedule, "Only scheduled adverts can be cancelled.", ct);
        if (a is null) return Result.Failure(error!);
        a.RecordStep(AdvertAction.Unscheduled, AdvertStatus.Draft, scope.UserId, scope.UserName, null, DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    /// <summary>Ends a live or scheduled advert early.</summary>
    public async Task<Result> CloseAsync(int id, string? reason, CancellationToken ct = default)
    {
        var (a, error) = await LoadForManagerAsync(id, AdvertWorkflow.CanClose, "Only live or scheduled adverts can be closed.", ct);
        if (a is null) return Result.Failure(error!);
        End(a, AdvertAction.Closed, scope.UserId, scope.UserName, Clean(reason), DateTime.UtcNow);
        return await SaveAsync(ct);
    }

    /// <summary>
    /// Copies an advert into a new Draft, for the same vacancy or another published one (e.g. to re-advertise after
    /// the vacancy is reopened). Dates are reset to today → the target vacancy's closing date.
    /// </summary>
    public async Task<Result<int>> CloneAsync(int id, int? targetVacancyId, CancellationToken ct = default)
    {
        if (!IsManager) return Result.Failure<int>("Only a Recruitment Officer or HR Manager can clone adverts.");
        var source = await adverts.GetWithDetailsAsync(id, ct);
        if (source is null) return Result.Failure<int>("Advert not found.");

        var (v, error) = await LoadVacancyForAdvertAsync(targetVacancyId ?? source.VacancyId, ct);
        if (v is null) return Result.Failure<int>(error!);

        var number = await numbers.NextAsync($"ADV-{DateTime.UtcNow.Year}", ct);
        var now = DateTime.UtcNow;
        var a = NewAdvert(v, $"ADV-{now.Year}-{number:0000}", now);
        a.Title = source.Title;
        a.Body = source.Body;
        a.HowToApply = source.HowToApply;
        a.StartDate = Today;
        a.EndDate = v.ClosingDate;
        a.ClonedFromAdvertId = source.Id;
        var allowed = AdvertWorkflow.AllowedChannels(v.VacancyType);
        foreach (var c in source.Channels.Select(c => c.Channel).Where(allowed.Contains))
            a.Channels.Add(new AdvertChannelPost { Channel = c });
        if (a.Channels.Count == 0) a.Channels.Add(new AdvertChannelPost { Channel = AdvertChannel.InternalPortal });
        a.RecordStep(AdvertAction.Cloned, AdvertStatus.Draft, scope.UserId, scope.UserName,
            $"Copied from {source.AdvertNumber}" + (v.Id == source.VacancyId ? "" : $" for vacancy {v.VacancyNumber}"), now);

        adverts.Add(a);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(a.Id);
    }

    // ===================================================================== External channels

    /// <summary>Records that the recruiter has placed the advert on an external channel (with its link or newspaper edition).</summary>
    public async Task<Result> MarkPostedAsync(int id, int channelId, string? reference, CancellationToken ct = default)
    {
        var (a, channel, error) = await LoadChannelAsync(id, channelId, ct);
        if (a is null) return Result.Failure(error!);
        if (a.Status != AdvertStatus.Live) return Result.Failure("Channels can only be updated while the advert is live.");
        if (AdvertWorkflow.IsAutomatic(channel!.Channel)) return Result.Failure("This channel is published automatically.");

        reference = Clean(reference);
        if (reference is null) return Result.Failure(channel.Channel == AdvertChannel.Newspaper
            ? "Enter the newspaper name and date of the edition."
            : "Paste the link to the posting.");
        if (reference.Length > 500) return Result.Failure("The link or reference must be 500 characters or fewer.");

        var now = DateTime.UtcNow;
        channel.Status = ChannelPostStatus.Posted;
        channel.Reference = reference;
        channel.PostedAt = now;
        channel.PostedByName = scope.UserName;
        channel.RemovedAt = null;
        a.RecordStep(AdvertAction.ChannelPosted, a.Status, scope.UserId, scope.UserName, $"{EnumText.Label(channel.Channel)}: {reference}", now);
        return await SaveAsync(ct);
    }

    /// <summary>Records that the advert was taken down from an external channel.</summary>
    public async Task<Result> MarkRemovedAsync(int id, int channelId, CancellationToken ct = default)
    {
        var (a, channel, error) = await LoadChannelAsync(id, channelId, ct);
        if (a is null) return Result.Failure(error!);
        if (AdvertWorkflow.IsAutomatic(channel!.Channel)) return Result.Failure("This channel is removed automatically when the advert closes.");
        if (channel.Status != ChannelPostStatus.Posted) return Result.Failure("This channel has not been posted.");

        var now = DateTime.UtcNow;
        channel.Status = ChannelPostStatus.Removed;
        channel.RemovedAt = now;
        a.RecordStep(AdvertAction.ChannelRemoved, a.Status, scope.UserId, scope.UserName, EnumText.Label(channel.Channel), now);
        return await SaveAsync(ct);
    }

    // ===================================================================== Careers pages

    /// <summary>
    /// Adverts running today. Everyone sees the company-website adverts; staff (<paramref name="includeInternal"/>)
    /// also see internal-portal adverts. Internal-only adverts are marked.
    /// </summary>
    public async Task<IReadOnlyList<PublicJobDto>> GetRunningJobsAsync(bool includeInternal, CancellationToken ct = default)
    {
        var website = await adverts.GetRunningAsync(AdvertChannel.CompanyWebsite, Today, ct);
        if (!includeInternal) return website;

        var internalOnes = await adverts.GetRunningAsync(AdvertChannel.InternalPortal, Today, ct);
        var publicNumbers = website.Select(j => j.AdvertNumber).ToHashSet();
        return website
            .Concat(internalOnes.Where(j => !publicNumbers.Contains(j.AdvertNumber)).Select(j => j with { InternalOnly = true }))
            .OrderByDescending(j => j.PublishedAt)
            .ToList();
    }

    public async Task<PublicJobDto?> GetRunningJobAsync(string advertNumber, bool includeInternal, CancellationToken ct = default) =>
        (await GetRunningJobsAsync(includeInternal, ct)).FirstOrDefault(j => j.AdvertNumber == advertNumber);

    // ===================================================================== Schedule (system)

    /// <summary>
    /// Publishes scheduled adverts whose start date has arrived, and closes adverts whose end date has passed or whose
    /// vacancy is no longer published. Run regularly by the background worker. Returns how many of each.
    /// </summary>
    public async Task<(int Published, int Closed)> RunScheduleAsync(DateOnly today, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Close first, so an advert whose vacancy has already closed is never published.
        var toClose = await adverts.GetDueToCloseAsync(today, ct);
        foreach (var a in toClose)
        {
            var reason = a.EndDate < today ? "End date passed" : "The vacancy is no longer open";
            End(a, AdvertAction.AutoClosed, null, "System", reason, now);
        }

        var closedIds = toClose.Select(a => a.Id).ToHashSet();
        var toPublish = (await adverts.GetDueToPublishAsync(today, ct)).Where(a => !closedIds.Contains(a.Id)).ToList();
        foreach (var a in toPublish) GoLive(a, AdvertAction.AutoPublished, null, "System", now);

        if (toClose.Count + toPublish.Count == 0) return (0, 0);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return (0, 0); } // someone changed one at the same moment; the next run retries

        foreach (var a in toPublish) await NotifyLiveAsync(a, ct);
        return (toPublish.Count, toClose.Count);
    }

    // ===================================================================== Helpers

    /// <summary>Checks the user may advertise this vacancy: manager role, vacancy visible, published and open.</summary>
    private async Task<(Vacancy? Vacancy, string? Error)> LoadVacancyForAdvertAsync(int vacancyId, CancellationToken ct)
    {
        if (!IsManager) return (null, "Only a Recruitment Officer or HR Manager can create adverts.");
        var v = await vacancies.GetWithDetailsAsync(vacancyId, ct);
        if (v is null || !AccessScope.CanSee(await scope.VisibilityAsync(ct), v.DepartmentId, v.RecruiterUserId))
            return (null, "Vacancy not found.");
        if (v.Status != VacancyStatus.Published)
            return (null, "Adverts can only be created for published vacancies (the vacancy's approval must be complete).");
        if (v.ClosingDate < Today)
            return (null, "This vacancy's closing date has passed.");
        return (v, null);
    }

    private async Task<(JobAdvert? Advert, string? Error)> LoadForManagerAsync(int id, Func<AdvertStatus, bool> allowed, string notAllowed, CancellationToken ct)
    {
        if (!IsManager) return (null, "Only a Recruitment Officer or HR Manager can do this.");
        var a = await adverts.GetWithDetailsAsync(id, ct);
        if (a is null) return (null, "Advert not found.");
        return allowed(a.Status) ? (a, null) : (null, notAllowed);
    }

    private async Task<(JobAdvert? Advert, AdvertChannelPost? Channel, string? Error)> LoadChannelAsync(int id, int channelId, CancellationToken ct)
    {
        if (!IsManager) return (null, null, "Only a Recruitment Officer or HR Manager can do this.");
        var a = await adverts.GetWithDetailsAsync(id, ct);
        var channel = a?.Channels.FirstOrDefault(c => c.Id == channelId);
        return a is null || channel is null ? (null, null, "Advert channel not found.") : (a, channel, null);
    }

    /// <summary>Why a draft cannot be published now, or null if it can.</summary>
    private static string? PublishProblem(JobAdvert a, Vacancy v)
    {
        if (v.Status != VacancyStatus.Published)
            return $"The vacancy is {EnumText.Label(v.Status).ToLowerInvariant()}. Adverts can only be published while the vacancy is published.";
        if (v.ClosingDate < Today) return "The vacancy's closing date has passed.";
        if (a.EndDate < Today) return "The end date has passed. Edit the advert and choose new dates.";
        if (a.EndDate > v.ClosingDate) return $"The end date is after the vacancy's closing date ({v.ClosingDate:dd MMM yyyy}).";
        if (a.Channels.Count == 0) return "Choose at least one channel.";
        return null;
    }

    private JobAdvert NewAdvert(Vacancy v, string number, DateTime now) => new()
    {
        AdvertNumber = number,
        VacancyId = v.Id,
        DivisionId = v.DivisionId,
        DepartmentId = v.DepartmentId,
        RequestingHodUserId = v.RequestingHodUserId,
        CreatedByUserId = scope.UserId,
        Status = AdvertStatus.Draft,
        StatusChangedAt = now
    };

    /// <summary>Goes live: automatic channels are posted now; external channels wait for the recruiter.</summary>
    private static void GoLive(JobAdvert a, AdvertAction action, string? actorId, string actorName, DateTime now)
    {
        a.PublishedAt = now;
        foreach (var c in a.Channels.Where(c => AdvertWorkflow.IsAutomatic(c.Channel)))
        {
            c.Status = ChannelPostStatus.Posted;
            c.PostedAt = now;
            c.PostedByName = "GRSMS (automatic)";
            c.RemovedAt = null;
        }
        a.RecordStep(action, AdvertStatus.Live, actorId, actorName, null, now);
    }

    /// <summary>Ends an advert: automatic channels come down at once; posted external channels are left for the recruiter to take down.</summary>
    private static void End(JobAdvert a, AdvertAction action, string? actorId, string actorName, string? comments, DateTime now)
    {
        a.ClosedAt = now;
        foreach (var c in a.Channels.Where(c => AdvertWorkflow.IsAutomatic(c.Channel) && c.Status == ChannelPostStatus.Posted))
        {
            c.Status = ChannelPostStatus.Removed;
            c.RemovedAt = now;
        }
        a.RecordStep(action, AdvertStatus.Closed, actorId, actorName, comments, now);
    }

    /// <summary>Tells the vacancy's recruiter (and the HOD) that the advert is live, listing external channels still to post.</summary>
    private async Task NotifyLiveAsync(JobAdvert a, CancellationToken ct)
    {
        var v = await vacancies.GetByIdAsync(a.VacancyId, ct);
        var toPost = a.Channels.Where(c => c.Status == ChannelPostStatus.ToPost).Select(c => EnumText.Label(c.Channel)).ToList();
        var html = $"The advert <b>{WorkflowMailer.Enc(a.Title)}</b> is now live until {a.EndDate:dd MMM yyyy}." +
                   (toPost.Count == 0 ? "" : $"<p>Still to post, using the copy-ready text on the advert page: {WorkflowMailer.Enc(string.Join(", ", toPost))}.</p>");
        var link = links.Advert(a.Id);
        if (v is not null)
            await mailer.ToUserAsync(v.RecruiterUserId, $"{a.AdvertNumber} is live", html, link, $"Open advert {a.AdvertNumber}", ct);
        await mailer.ToUserAsync(a.RequestingHodUserId, $"{a.AdvertNumber} is live",
            $"The advert <b>{WorkflowMailer.Enc(a.Title)}</b> for your department is now live until {a.EndDate:dd MMM yyyy}.",
            link, $"Open advert {a.AdvertNumber}", ct);
    }

    private static void FillVacancyFields(AdvertFormDto form, Vacancy v)
    {
        form.VacancyNumber = v.VacancyNumber;
        form.VacancyTitle = v.JobTitle;
        form.VacancyClosingDate = v.ClosingDate;
        form.VacancyType = v.VacancyType;
        form.AllowedChannels = AdvertWorkflow.AllowedChannels(v.VacancyType);
    }

    private static void Normalize(AdvertFormDto form)
    {
        form.Title = form.Title.Trim();
        form.Body = form.Body.Trim().Replace("\r\n", "\n");
        form.HowToApply = form.HowToApply.Trim();
        form.Channels = form.Channels.Distinct().Order().ToList();
    }

    private static List<string> Validate(AdvertFormDto form, Vacancy v)
    {
        var errors = new List<string>();
        if (form.StartDate is not { } start || form.EndDate is not { } end)
        {
            errors.Add("Choose the start and end dates.");
            return errors;
        }
        if (start < Today) errors.Add("The start date cannot be in the past.");
        if (end < start) errors.Add("The end date cannot be before the start date.");
        if (end > v.ClosingDate) errors.Add($"The end date cannot be after the vacancy's closing date ({v.ClosingDate:dd MMM yyyy}).");

        if (form.Channels.Count == 0) errors.Add("Choose at least one channel.");
        var allowed = AdvertWorkflow.AllowedChannels(v.VacancyType);
        if (form.Channels.Any(c => !allowed.Contains(c)))
            errors.Add("This vacancy is for GREL staff only, so it can only be advertised on the internal portal.");
        return errors;
    }

    /// <summary>Copies the form onto the advert. Channel rows are added or removed to match the ticked channels.</summary>
    private static void Apply(AdvertFormDto form, JobAdvert a)
    {
        a.Title = form.Title;
        a.Body = form.Body;
        a.HowToApply = form.HowToApply;
        a.StartDate = form.StartDate!.Value;
        a.EndDate = form.EndDate!.Value;
        foreach (var gone in a.Channels.Where(c => !form.Channels.Contains(c.Channel)).ToList()) a.Channels.Remove(gone);
        foreach (var added in form.Channels.Where(c => a.Channels.All(x => x.Channel != c)))
            a.Channels.Add(new AdvertChannelPost { Channel = added });
    }


    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);
}
