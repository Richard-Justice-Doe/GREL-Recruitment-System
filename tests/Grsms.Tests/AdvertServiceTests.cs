using Grsms.Application.Adverts;
using Grsms.Application.Vacancies;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Job advert rules (Module 4): only for published vacancies, dates within the vacancy, channel rules, publish now or
/// schedule, automatic publishing and closing, external channel tracking, clone, careers pages and visibility.
/// </summary>
public class AdvertServiceTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3, HeadOffice = 1, It = 2;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly TestDatabase _db = new();
    private string _hod = "", _otherHod = "", _ro1 = "", _ro2 = "", _hrm = "", _itm = "";

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro1 = await _db.CreateUserAsync("ro1@grel.test", 1, Roles.RecruitmentOfficer);
        _ro2 = await _db.CreateUserAsync("ro2@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
        _itm = await _db.CreateUserAsync("itm@grel.test", It, Roles.ItManager);

        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);
    private void AsRecruiter() => As(_ro1, Roles.RecruitmentOfficer);
    private void AsHrManager() => As(_hrm, Roles.HrManager);
    private void AsItManager() => As(_itm, Roles.ItManager);

    /// <summary>
    /// 11 Nov 2026 (docs/29 change 2): the RO sends the advert for review, the HR Manager approves it, the IT Manager
    /// approves and publishes it. Ends signed in as the recruiter again.
    /// </summary>
    private async Task ApproveAsync(int id)
    {
        AsRecruiter();
        var r = await Run(s => s.SubmitAsync(id));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        AsHrManager();
        r = await Run(s => s.HrApproveAsync(id, null));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        AsItManager();
        r = await Run(s => s.PublishAsync(id));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        AsRecruiter();
    }

    private async Task<T> Run<T>(Func<AdvertService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<AdvertService>());
    }

    private async Task<T> RunVacancy<T>(Func<VacancyService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<VacancyService>());
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>A vacancy for Factory APM: created by RO1, then (unless <paramref name="publish"/> is false) approved by the HR Manager.</summary>
    private async Task<int> VacancyAsync(bool publish = true, VacancyType type = VacancyType.InternalAndExternal)
    {
        var requestId = await Db(async db =>
        {
            var r = new RecruitmentRequest
            {
                RequestNumber = "RR-TEST-" + Guid.NewGuid().ToString("N")[..8], DivisionId = 2, DepartmentId = Apm, SiteId = HeadOffice,
                PositionTitle = "Safety Officer", NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent,
                VacancyReason = VacancyReason.NewPosition, Justification = "Needed for the new processing plant.",
                BudgetCode = "APM-26", EstimatedAnnualCost = 90000, RequestedByUserId = _hod, AssignedRecruiterUserId = _ro2,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(r);
            await db.SaveChangesAsync();
            return r.Id;
        });

        AsRecruiter();
        var created = await RunVacancy(s => s.CreateAsync(new VacancyFormDto
        {
            RecruitmentRequestId = requestId, JobTitle = "Safety Officer", SiteId = HeadOffice, VacancyType = type,
            JobDescription = "Leads daily safety inspections, investigates incidents and trains staff on safe work procedures.",
            MinimumEducation = EducationLevel.Bachelors, FieldOfStudy = "Occupational Health and Safety", MinYearsExperience = 3,
            EssentialSkills = "Risk assessment", EssentialCertifications = "NEBOSH IGC",
            SalaryMin = 6000, SalaryMax = 8000, ShowSalary = true, ClosingDate = Today.AddDays(14)
        }));
        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        if (!publish) return created.Value;

        Assert.True((await RunVacancy(s => s.SubmitAsync(created.Value))).Succeeded);
        As(_hrm, Roles.HrManager);
        Assert.True((await RunVacancy(s => s.DecideAsync(created.Value,
            new VacancyDecisionDto { Decision = VacancyDecision.Approve, ExpectedStatus = VacancyStatus.PendingApproval }))).Succeeded);
        AsRecruiter();
        return created.Value;
    }

    /// <summary>A draft advert for the vacancy (RO1), on the given channels, starting today unless stated.</summary>
    private async Task<int> DraftAdvertAsync(int vacancyId, DateOnly? start = null, params AdvertChannel[] channels)
    {
        AsRecruiter();
        var form = (await Run(s => s.GetNewFormAsync(vacancyId))).Value!;
        form.StartDate = start ?? Today;
        if (channels.Length > 0) form.Channels = [.. channels];
        var result = await Run(s => s.CreateAsync(form));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Value;
    }

    private Task<AdvertDetailsDto?> Details(int id) => Run(s => s.GetDetailsAsync(id));

    /// <summary>
    /// The saved emails with base64 bodies decoded. Bodies containing non-ASCII text (e.g. the "–" in advert titles)
    /// are written base64-encoded, so their words cannot be searched in the raw .eml file.
    /// </summary>
    private IEnumerable<string> DecodedEmails() => _db.SentEmails().Select(m =>
    {
        var split = m.IndexOf("\r\n\r\n", StringComparison.Ordinal) is var i and >= 0 ? i : m.IndexOf("\n\n", StringComparison.Ordinal);
        if (split < 0 || !m.Contains("Content-Transfer-Encoding: base64")) return m;
        var body = string.Concat(m[split..].Where(c => !char.IsWhiteSpace(c)));
        return m[..split] + "\n\n" + System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(body));
    });

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Adverts_need_a_published_vacancy_and_a_recruiter_or_hr_manager()
    {
        var draftVacancy = await VacancyAsync(publish: false);
        AsRecruiter();
        Assert.False((await Run(s => s.GetNewFormAsync(draftVacancy))).Succeeded);

        var published = await VacancyAsync();
        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.GetNewFormAsync(published))).Succeeded);

        // The form is pre-filled from the vacancy.
        As(_hrm, Roles.HrManager);
        var form = (await Run(s => s.GetNewFormAsync(published))).Value!;
        Assert.StartsWith("Safety Officer", form.Title);
        Assert.Contains("NEBOSH IGC", form.Body);
        Assert.Contains("GHS 6,000 – 8,000 per month", form.Body);
        Assert.Equal(Today.AddDays(14), form.EndDate);
        Assert.Equal([AdvertChannel.InternalPortal, AdvertChannel.CompanyWebsite], form.Channels);
    }

    [Fact]
    public async Task Dates_and_channels_are_checked()
    {
        var vacancyId = await VacancyAsync();
        AsRecruiter();
        var form = (await Run(s => s.GetNewFormAsync(vacancyId))).Value!;

        async Task<bool> Saves(Action<AdvertFormDto> change)
        {
            var f = (await Run(s => s.GetNewFormAsync(vacancyId))).Value!;
            change(f);
            return (await Run(s => s.CreateAsync(f))).Succeeded;
        }

        Assert.False(await Saves(f => f.EndDate = Today.AddDays(15)));              // after the vacancy closes
        Assert.False(await Saves(f => f.StartDate = Today.AddDays(-1)));            // in the past
        Assert.False(await Saves(f => { f.StartDate = Today.AddDays(5); f.EndDate = Today.AddDays(4); }));
        Assert.False(await Saves(f => f.Channels = []));
        Assert.True(await Saves(f => f.Channels = [AdvertChannel.LinkedIn, AdvertChannel.Newspaper]));

        // Staff-only vacancies may only use the internal portal.
        var internalVacancy = await VacancyAsync(type: VacancyType.Internal);
        AsRecruiter();
        var internalForm = (await Run(s => s.GetNewFormAsync(internalVacancy))).Value!;
        Assert.Equal([AdvertChannel.InternalPortal], internalForm.Channels);
        internalForm.Channels = [AdvertChannel.InternalPortal, AdvertChannel.LinkedIn];
        Assert.False((await Run(s => s.CreateAsync(internalForm))).Succeeded);
    }

    [Fact]
    public async Task Publishing_today_goes_live_on_the_automatic_channels_and_the_careers_page()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId, null, AdvertChannel.InternalPortal, AdvertChannel.CompanyWebsite, AdvertChannel.LinkedIn);

        // Not visible anywhere while it is a draft.
        Assert.Empty(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));

        await ApproveAsync(id);
        var a = (await Details(id))!;
        Assert.Equal(AdvertStatus.Live, a.Status);
        Assert.Matches(@"^ADV-\d{4}-0001$", a.AdvertNumber);
        Assert.Equal("Operations", a.DivisionName);
        Assert.Equal("hod", a.RequestingHodName);
        Assert.All(a.Channels.Where(c => c.IsAutomatic), c => Assert.Equal(ChannelPostStatus.Posted, c.Status));
        var linkedIn = a.Channels.Single(c => c.Channel == AdvertChannel.LinkedIn);
        Assert.Equal(ChannelPostStatus.ToPost, linkedIn.Status);
        Assert.Contains(a.AdvertNumber, linkedIn.CopyText);
        Assert.Contains($"https://grsms.test/Careers/Details/{a.AdvertNumber}", linkedIn.CopyText);
        Assert.NotNull(a.PublicLink);

        // The assigned recruiter is told, including what is still to post.
        Assert.Contains(DecodedEmails(), m => m.Contains("To: ro2@grel.test") && m.Contains("is live") && m.Contains("Still to post") && m.Contains("LinkedIn"));

        var jobs = await Run(s => s.GetRunningJobsAsync(includeInternal: false));
        Assert.Equal(a.AdvertNumber, Assert.Single(jobs).AdvertNumber);
        Assert.Equal(1, (await Run(s => s.CountsAsync())).ChannelsToPost);
    }

    [Fact]
    public async Task A_future_start_date_schedules_the_advert_which_then_publishes_and_closes_itself()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId, Today.AddDays(3));

        await ApproveAsync(id);
        Assert.Equal(AdvertStatus.Scheduled, (await Details(id))!.Status);
        Assert.Empty(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));

        // Before the start date nothing happens; on it, the system publishes.
        Assert.Equal((0, 0), await Run(s => s.RunScheduleAsync(Today.AddDays(2))));
        Assert.Equal((1, 0), await Run(s => s.RunScheduleAsync(Today.AddDays(3))));
        var live = (await Details(id))!;
        Assert.Equal(AdvertStatus.Live, live.Status);
        Assert.Contains(live.History, h => h.Action == AdvertAction.AutoPublished && h.ActorName == "System");

        // The day after the end date (= the vacancy's closing date), it closes and comes off the automatic channels.
        Assert.Equal((0, 0), await Run(s => s.RunScheduleAsync(Today.AddDays(14))));
        Assert.Equal((0, 1), await Run(s => s.RunScheduleAsync(Today.AddDays(15))));
        var closed = (await Details(id))!;
        Assert.Equal(AdvertStatus.Closed, closed.Status);
        Assert.All(closed.Channels, c => Assert.Equal(ChannelPostStatus.Removed, c.Status));
    }

    [Fact]
    public async Task Closing_the_vacancy_closes_its_adverts_and_blocks_new_ones()
    {
        var vacancyId = await VacancyAsync();
        var live = await DraftAdvertAsync(vacancyId);
        await ApproveAsync(live);
        var draft = await DraftAdvertAsync(vacancyId);

        Assert.True((await RunVacancy(s => s.CloseAsync(vacancyId, "Enough applicants"))).Succeeded);

        // The draft can no longer be published, and the next scheduler run closes the live advert.
        AsRecruiter();
        Assert.False((await Run(s => s.SubmitAsync(draft))).Succeeded);
        Assert.NotNull((await Details(draft))!.PublishBlockedReason);
        Assert.Equal((0, 1), await Run(s => s.RunScheduleAsync(Today)));
        Assert.Equal(AdvertStatus.Closed, (await Details(live))!.Status);
        Assert.Empty(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));
        Assert.False((await Run(s => s.GetNewFormAsync(vacancyId))).Succeeded);
    }

    [Fact]
    public async Task External_channels_are_recorded_as_posted_and_taken_down()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId, null, AdvertChannel.CompanyWebsite, AdvertChannel.Jobberman, AdvertChannel.Newspaper);

        var jobberman = (await Details(id))!.Channels.Single(c => c.Channel == AdvertChannel.Jobberman).Id;
        var website = (await Details(id))!.Channels.Single(c => c.Channel == AdvertChannel.CompanyWebsite).Id;

        // Not while the advert is a draft; never for automatic channels; a link/reference is required.
        AsItManager();
        Assert.False((await Run(s => s.MarkPostedAsync(id, jobberman, "https://jobberman.example/job/1"))).Succeeded);
        await ApproveAsync(id);
        AsItManager();
        Assert.False((await Run(s => s.MarkPostedAsync(id, website, "https://x.example"))).Succeeded);
        Assert.False((await Run(s => s.MarkPostedAsync(id, jobberman, "  "))).Succeeded);

        // 11 Nov 2026 (docs/29 change 2): only the IT Manager records postings, not the recruiter.
        AsRecruiter();
        Assert.False((await Details(id))!.CanManageChannels);
        Assert.False((await Run(s => s.MarkPostedAsync(id, jobberman, "https://jobberman.example/job/1"))).Succeeded);

        AsItManager();
        Assert.True((await Details(id))!.CanManageChannels);
        Assert.True((await Run(s => s.MarkPostedAsync(id, jobberman, "https://jobberman.example/job/1"))).Succeeded);
        var posted = (await Details(id))!.Channels.Single(c => c.Id == jobberman);
        Assert.Equal((ChannelPostStatus.Posted, "https://jobberman.example/job/1", _itm), (posted.Status, posted.Reference, posted.PostedByName)); // test users are named by their id

        // A HOD can see the advert but not change it.
        As(_hod, Roles.HeadOfDepartment);
        var hodView = (await Details(id))!;
        Assert.False(hodView.CanManageChannels || hodView.CanClose || hodView.CanEdit || hodView.CanClone || hodView.CanUnpublish);
        Assert.False((await Run(s => s.MarkRemovedAsync(id, jobberman))).Succeeded);

        // Closing the advert leaves the posted external channel for the IT Manager to take down.
        AsRecruiter();
        Assert.True((await Run(s => s.CloseAsync(id, null))).Succeeded);
        Assert.Equal(ChannelPostStatus.Posted, (await Details(id))!.Channels.Single(c => c.Id == jobberman).Status);
        Assert.False((await Run(s => s.MarkRemovedAsync(id, jobberman))).Succeeded);
        AsItManager();
        Assert.True((await Run(s => s.MarkRemovedAsync(id, jobberman))).Succeeded);
        Assert.Equal(ChannelPostStatus.Removed, (await Details(id))!.Channels.Single(c => c.Id == jobberman).Status);
    }

    [Fact]
    public async Task Cloning_copies_the_advert_into_a_new_draft()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId, null, AdvertChannel.CompanyWebsite, AdvertChannel.LinkedIn);
        await ApproveAsync(id);
        Assert.True((await Run(s => s.CloseAsync(id, "Wrong salary in the text"))).Succeeded);

        var clone = await Run(s => s.CloneAsync(id, null));
        Assert.True(clone.Succeeded, string.Join(" ", clone.Errors));
        var copy = (await Details(clone.Value))!;
        var original = (await Details(id))!;
        Assert.Equal(AdvertStatus.Draft, copy.Status);
        Assert.NotEqual(original.AdvertNumber, copy.AdvertNumber);
        Assert.Equal(original.Body, copy.Body);
        Assert.Equal(id, copy.ClonedFromAdvertId);
        Assert.Equal([AdvertChannel.CompanyWebsite, AdvertChannel.LinkedIn], copy.Channels.Select(c => c.Channel).ToArray());
        Assert.All(copy.Channels, c => Assert.Equal(ChannelPostStatus.ToPost, c.Status));
        Assert.Equal((Today, Today.AddDays(14)), (copy.StartDate, copy.EndDate));
        await ApproveAsync(clone.Value);
    }

    [Fact]
    public async Task Internal_portal_adverts_are_for_signed_in_staff_only()
    {
        var vacancyId = await VacancyAsync(type: VacancyType.Internal);
        var id = await DraftAdvertAsync(vacancyId);
        await ApproveAsync(id);

        Assert.Empty(await Run(s => s.GetRunningJobsAsync(includeInternal: false)));
        var number = (await Details(id))!.AdvertNumber;
        Assert.Null(await Run(s => s.GetRunningJobAsync(number, includeInternal: false)));
        Assert.True(Assert.Single(await Run(s => s.GetRunningJobsAsync(includeInternal: true))).InternalOnly);
        Assert.Null((await Details(id))!.PublicLink);
    }

    [Fact]
    public async Task Other_departments_cannot_see_adverts_and_filters_work()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId, null, AdvertChannel.CompanyWebsite, AdvertChannel.Indeed);

        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Details(id));
        Assert.Equal(0, (await Run(s => s.SearchAsync(new AdvertFilter { Tab = "all" }))).TotalCount);

        As(_hrm, Roles.HrManager);
        async Task<int> Count(AdvertFilter f) => (await Run(s => s.SearchAsync(f))).TotalCount;
        Assert.Equal(1, await Count(new() { Tab = "draft" }));
        Assert.Equal(0, await Count(new() { Tab = "live" }));
        Assert.Equal(1, await Count(new() { Tab = "all", DivisionId = 2, DepartmentId = Apm }));
        Assert.Equal(0, await Count(new() { Tab = "all", DivisionId = 1 }));
        Assert.Equal(1, await Count(new() { Tab = "all", Channel = AdvertChannel.Indeed }));
        Assert.Equal(0, await Count(new() { Tab = "all", Channel = AdvertChannel.Newspaper }));
        Assert.Equal(1, await Count(new() { Tab = "all", From = Today, To = Today }));
        Assert.Equal(0, await Count(new() { Tab = "all", From = Today.AddDays(20) }));
        Assert.Single(await Run(s => s.ListForVacancyAsync(vacancyId)));

        // The advert stores the division, department and requesting HOD of its vacancy.
        var stored = await Db(db => db.JobAdverts.SingleAsync(a => a.Id == id));
        Assert.Equal((2, Apm, _hod), (stored.DivisionId, stored.DepartmentId, stored.RequestingHodUserId));
    }

    // ------------------------------------------------------------------ 11 Nov 2026 (docs/29 change 2): HR review, then IT approval

    [Fact]
    public async Task An_advert_is_reviewed_by_the_hr_manager_then_approved_and_published_by_the_it_manager()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId);

        // Nobody can skip a step.
        AsHrManager();
        Assert.False((await Run(s => s.HrApproveAsync(id, null))).Succeeded); // still a draft
        AsItManager();
        Assert.False((await Run(s => s.PublishAsync(id))).Succeeded);

        AsRecruiter();
        Assert.True((await Details(id))!.CanSubmit);
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);
        var sent = (await Details(id))!;
        Assert.Equal((AdvertStatus.AwaitingHrReview, "ro1"), (sent.Status, sent.SubmittedByName));
        Assert.False(sent.CanEdit); // under review: the reviewer returns it if it needs changes
        Assert.Null(await Run(s => s.GetForEditAsync(id)));
        var stale = new AdvertFormDto { Id = id, VacancyId = vacancyId, Title = sent.Title, Body = sent.Body, HowToApply = sent.HowToApply,
            StartDate = sent.StartDate, EndDate = sent.EndDate, Channels = [AdvertChannel.InternalPortal] };
        Assert.False((await Run(s => s.UpdateAsync(stale))).Succeeded);
        Assert.Contains(DecodedEmails(), m => m.Contains("To: hrm@grel.test") && m.Contains("advert to review"));

        // The recruiter and the IT Manager cannot do the HR review; the IT Manager cannot publish before it.
        Assert.False((await Run(s => s.HrApproveAsync(id, null))).Succeeded);
        AsItManager();
        Assert.False((await Run(s => s.HrApproveAsync(id, null))).Succeeded);
        Assert.False((await Run(s => s.PublishAsync(id))).Succeeded);
        Assert.Empty(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));

        AsHrManager();
        var hrView = (await Details(id))!;
        Assert.True(hrView.CanHrApprove && hrView.CanReturn && hrView.CanReject && !hrView.CanPublish);
        Assert.Contains(await Run(s => s.MyWorkAsync()), w => w.Id == id && w.Status == AdvertStatus.AwaitingHrReview);
        Assert.True((await Run(s => s.HrApproveAsync(id, "Text checked"))).Succeeded);
        Assert.Equal(AdvertStatus.AwaitingItApproval, (await Details(id))!.Status);
        Assert.False((await Run(s => s.PublishAsync(id))).Succeeded); // the HR Manager does not publish
        Assert.Contains(DecodedEmails(), m => m.Contains("To: itm@grel.test") && m.Contains("advert to approve"));

        AsItManager();
        var itView = (await Details(id))!;
        Assert.True(itView.CanPublish && itView.CanReturn && itView.CanReject && !itView.CanHrApprove);
        Assert.Contains(await Run(s => s.MyWorkAsync()), w => w.Id == id && w.Status == AdvertStatus.AwaitingItApproval);
        Assert.Equal(1, (await Run(s => s.ItSummaryAsync()))!.AwaitingApproval);
        Assert.True((await Run(s => s.PublishAsync(id))).Succeeded);

        var live = (await Details(id))!;
        Assert.Equal(AdvertStatus.Live, live.Status);
        Assert.Equal(("hrm", "itm"), (live.HrReviewedByName, live.ItApprovedByName));
        Assert.Equal([AdvertAction.Created, AdvertAction.Submitted, AdvertAction.HrApproved, AdvertAction.ItApproved], live.History.Select(h => h.Action));
        Assert.Single(await Run(s => s.GetRunningJobsAsync(includeInternal: false)));
        Assert.Contains(DecodedEmails(), m => m.Contains("To: hrm@grel.test") && m.Contains("approved by IT"));
        Assert.Equal(new AdvertItSummary(0, 0, 1, 0), await Run(s => s.ItSummaryAsync()));

        AsRecruiter();
        Assert.Null(await Run(s => s.ItSummaryAsync())); // the IT cards are the IT Manager's
    }

    [Fact]
    public async Task A_returned_advert_is_corrected_and_resubmitted_and_a_rejected_one_is_final()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId);
        AsRecruiter();
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);

        // The HR Manager returns it: a reason is required and the recruiter is told.
        AsHrManager();
        Assert.False((await Run(s => s.ReturnAsync(id, "  "))).Succeeded);
        Assert.True((await Run(s => s.ReturnAsync(id, "Add the salary band to the text."))).Succeeded);
        var returned = (await Details(id))!;
        Assert.Equal((AdvertStatus.ReturnedForCorrection, "Add the salary band to the text."), (returned.Status, returned.ReviewComment));
        Assert.Contains(DecodedEmails(), m => m.Contains("To: ro1@grel.test") && m.Contains("returned for correction") && m.Contains("salary band"));

        // The recruiter corrects and resubmits; it is on their to-do list until then.
        AsRecruiter();
        Assert.Contains(await Run(s => s.MyWorkAsync()), w => w.Id == id && w.Status == AdvertStatus.ReturnedForCorrection);
        var form = (await Run(s => s.GetForEditAsync(id)))!;
        form.Title = "Safety Officer (corrected)";
        Assert.True((await Run(s => s.UpdateAsync(form))).Succeeded);
        Assert.Equal(AdvertStatus.ReturnedForCorrection, (await Details(id))!.Status); // saving alone does not resubmit
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);
        Assert.Null((await Details(id))!.ReviewComment);

        // The IT Manager returns it too: the recruiter AND the HR reviewer are told.
        AsHrManager();
        Assert.True((await Run(s => s.HrApproveAsync(id, null))).Succeeded);
        AsItManager();
        Assert.True((await Run(s => s.ReturnAsync(id, "The careers page link is missing."))).Succeeded);
        var mails = DecodedEmails().Where(m => m.Contains("careers page link is missing")).ToList();
        Assert.Contains(mails, m => m.Contains("To: ro1@grel.test"));
        Assert.Contains(mails, m => m.Contains("To: hrm@grel.test"));

        // Rejected at the IT step: final. It can no longer be sent, edited or published; it can be cloned.
        AsRecruiter();
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);
        AsHrManager();
        Assert.True((await Run(s => s.HrApproveAsync(id, null))).Succeeded);
        AsItManager();
        Assert.False((await Run(s => s.RejectAsync(id, null))).Succeeded);
        Assert.True((await Run(s => s.RejectAsync(id, "This vacancy is to be filled internally."))).Succeeded);
        AsRecruiter();
        var rejected = (await Details(id))!;
        Assert.Equal(AdvertStatus.Rejected, rejected.Status);
        Assert.False(rejected.CanEdit || rejected.CanSubmit);
        Assert.False((await Run(s => s.SubmitAsync(id))).Succeeded);
        Assert.True((await Run(s => s.CloneAsync(id, null))).Succeeded);
        Assert.Equal(1, (await Run(s => s.SearchAsync(new AdvertFilter { Tab = "closed" }))).TotalCount);
    }

    [Fact]
    public async Task Editing_or_taking_down_a_published_advert_takes_it_offline_until_it_is_approved_again()
    {
        var vacancyId = await VacancyAsync();
        var id = await DraftAdvertAsync(vacancyId);
        await ApproveAsync(id);
        Assert.Single(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));

        // The recruiter edits the published advert: it goes offline and back to the HR Manager.
        var form = (await Run(s => s.GetForEditAsync(id)))!;
        Assert.True(form.TakesOffline);
        form.Body += "\nPlease note: shift work is required.";
        Assert.True((await Run(s => s.UpdateAsync(form))).Succeeded);
        var edited = (await Details(id))!;
        Assert.Equal(AdvertStatus.AwaitingHrReview, edited.Status);
        Assert.All(edited.Channels.Where(c => c.IsAutomatic), c => Assert.Equal(ChannelPostStatus.Removed, c.Status));
        Assert.Contains(edited.History, h => h.Action == AdvertAction.Unpublished);
        Assert.Empty(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));

        // Approved again: back online.
        AsHrManager();
        Assert.True((await Run(s => s.HrApproveAsync(id, null))).Succeeded);
        AsItManager();
        Assert.True((await Run(s => s.PublishAsync(id))).Succeeded);
        Assert.Single(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));

        // Only the IT Manager takes a published advert offline, with a reason.
        AsRecruiter();
        Assert.False((await Run(s => s.UnpublishAsync(id, "Wrong dates"))).Succeeded);
        AsHrManager();
        Assert.False((await Run(s => s.UnpublishAsync(id, "Wrong dates"))).Succeeded);
        AsItManager();
        Assert.True((await Details(id))!.CanUnpublish);
        Assert.False((await Run(s => s.UnpublishAsync(id, null))).Succeeded);
        Assert.True((await Run(s => s.UnpublishAsync(id, "Wrong dates"))).Succeeded);
        Assert.Equal(AdvertStatus.ReturnedForCorrection, (await Details(id))!.Status);
        Assert.Empty(await Run(s => s.GetRunningJobsAsync(includeInternal: true)));
        Assert.Contains(DecodedEmails(), m => m.Contains("To: ro1@grel.test") && m.Contains("taken offline") && m.Contains("Wrong dates"));
    }
}
