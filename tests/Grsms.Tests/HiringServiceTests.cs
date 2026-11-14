using Grsms.Application.Hiring;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Interviews;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Background checks, the hiring recommendation (HOD endorsement → HR Manager → MD), and offers (Module 8): the
/// preconditions, separation of duties, the HR override, positions, checks before an offer, and the candidate's reply.
/// </summary>
public class HiringServiceTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly TestDatabase _db = new();
    private string _hod = "", _otherHod = "", _ro = "", _hrm = "", _hrm2 = "", _md = "", _candidate = "", _candidate2 = "";
    private int _vacancyId;

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
        _hrm2 = await _db.CreateUserAsync("hrm2@grel.test", 1, Roles.HrManager);
        _md = await _db.CreateUserAsync("md@grel.test", 1, Roles.ManagingDirector);
        _candidate = await _db.CreateUserAsync("ama@mail.test", null, Roles.Candidate);
        _candidate2 = await _db.CreateUserAsync("kofi@mail.test", null, Roles.Candidate);

        _vacancyId = await Db(async db =>
        {
            (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-H-1", DivisionId = 2, DepartmentId = Apm, SiteId = 3, PositionTitle = "Process Engineer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var v = new Vacancy
            {
                VacancyNumber = "VAC-H-1", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm,
                SiteId = 3, RequestingHodUserId = _hod, RecruiterUserId = _ro, ReportingManagerUserId = _hod, EmploymentType = EmploymentType.Permanent,
                NumberOfPositions = 1, VacancyType = VacancyType.External, JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Bachelors,
                SalaryMin = 7000, SalaryMax = 9000, ClosingDate = Today.AddDays(-5), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(v);
            await db.SaveChangesAsync();
            return v.Id;
        });
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);
    private void AsRecruiter() => As(_ro, Roles.RecruitmentOfficer);

    private async Task<T> Run<T>(Func<HiringService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<HiringService>());
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>An application at the interview stage, optionally with a completed, scored interview.</summary>
    private Task<int> InterviewedAsync(string applicant, string name, bool completedInterview = true) => Db(async db =>
    {
        var a = new JobApplication
        {
            ApplicationNumber = "APP-H-" + Guid.NewGuid().ToString("N")[..6], VacancyId = _vacancyId, ApplicantUserId = applicant,
            Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, FullName = name,
            Email = $"{name.ToLowerInvariant()}@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Bachelors,
            Status = ApplicationStatus.Interview, SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
        };
        db.JobApplications.Add(a);
        await db.SaveChangesAsync();
        var i = new Interview
        {
            JobApplicationId = a.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod,
            StartsAt = DateTime.UtcNow.AddDays(-1), Location = "Board Room", ScheduledByUserId = _ro,
            Status = completedInterview ? InterviewStatus.Completed : InterviewStatus.Scheduled
        };
        i.Panel.Add(new InterviewPanelMember { UserId = _hod, IsChair = true });
        db.Interviews.Add(i);
        await db.SaveChangesAsync();
        return a.Id;
    });

    private async Task RecommendAsync(int app)
    {
        AsRecruiter();
        var r = await Run(s => s.RecommendAsync(app, 8500, Today.AddDays(30), "Top interview score and strong references from Tema."));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
    }

    /// <summary>A recommendation made before 9 Nov 2026, still waiting in the old HOD → HR Manager → MD chain.</summary>
    private Task<int> LegacyRecommendationAsync(int app, string proposedBy) => Db(async db =>
    {
        db.HiringRecommendations.Add(new HiringRecommendation
        {
            Number = "HIR-OLD-" + app, JobApplicationId = app, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
            RequestingHodUserId = _hod, ProposedSalary = 8500, ProposedStartDate = Today.AddDays(30),
            Justification = "Recommended by the panel unanimously.", CreatedByUserId = proposedBy, Status = HiringStatus.AwaitingHod
        });
        return await db.SaveChangesAsync();
    });

    private async Task ClearedCheckAsync(int app)
    {
        AsRecruiter();
        Assert.True((await Run(s => s.AddCheckAsync(app, CheckType.References, "Mr K. Owusu, Tema Rubber Ltd"))).Succeeded);
        var id = await Db(db => db.BackgroundChecks.Where(c => c.JobApplicationId == app).Select(c => c.Id).FirstAsync());
        Assert.True((await Run(s => s.CompleteCheckAsync(id, CheckStatus.Cleared, null))).Succeeded);
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Only_the_recruitment_officer_approves_a_hire_without_the_hod_hr_manager_or_md()
    {
        var notYet = await InterviewedAsync(_candidate2, "Kofi", completedInterview: false);
        AsRecruiter();
        Assert.False((await Run(s => s.RecommendAsync(notYet, 8000, Today.AddDays(10), "A long enough justification text."))).Succeeded);

        var app = await InterviewedAsync(_candidate, "Ama");
        Assert.False((await Run(s => s.RecommendAsync(app, 8000, Today.AddDays(10), "Too short"))).Succeeded);
        As(_hrm, Roles.HrManager); // not a Recruitment Officer
        Assert.False((await Run(s => s.RecommendAsync(app, 8000, Today.AddDays(10), "Recommended by the panel unanimously."))).Succeeded);

        await RecommendAsync(app);
        var rec = await Db(db => db.HiringRecommendations.SingleAsync(r => r.JobApplicationId == app));
        Assert.Equal((HiringStatus.Approved, _ro, HiringDecision.None, HiringDecision.None, HiringDecision.None),
            (rec.Status, rec.CreatedByUserId, rec.HodDecision, rec.HrDecision, rec.MdDecision));
        Assert.Equal((2, Apm, _hod), (rec.DivisionId, rec.DepartmentId, rec.RequestingHodUserId));
        Assert.Matches(@"^HIR-\d{4}-0001$", rec.Number);
        Assert.DoesNotContain(_db.SentEmails(), m => m.Contains("needs your endorsement"));
        AsRecruiter();
        Assert.False((await Run(s => s.RecommendAsync(app, 8000, Today.AddDays(10), "A second one is not allowed once approved."))).Succeeded);

        // Nothing waits for the HOD, HR Manager or MD.
        foreach (var (user, role) in new[] { (_hod, Roles.HeadOfDepartment), (_hrm, Roles.HrManager), (_md, Roles.ManagingDirector) })
        {
            As(user, role);
            Assert.Empty(await Run(s => s.AwaitingMyDecisionAsync()));
            Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded);
        }
    }

    [Fact]
    public async Task Recommendations_already_in_the_old_chain_still_go_hod_then_hr_then_md()
    {
        var app = await InterviewedAsync(_candidate, "Ama");
        await LegacyRecommendationAsync(app, _ro);

        // Out of order: HR and MD cannot act before the HOD; another department's HOD cannot endorse.
        As(_hrm, Roles.HrManager);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded);
        As(_otherHod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded);
        Assert.Null(await Run(s => s.GetCaseAsync(app)));

        As(_hod, Roles.HeadOfDepartment);
        Assert.True((await Run(s => s.GetCaseAsync(app)))!.CanHodDecide);
        Assert.Single(await Run(s => s.AwaitingMyDecisionAsync()));
        Assert.True((await Run(s => s.DecideAsync(app, HiringDecision.Approve, "Strong engineer"))).Succeeded);

        As(_md, Roles.ManagingDirector);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded); // HR first
        As(_hrm, Roles.HrManager);
        Assert.True((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded);
        As(_md, Roles.ManagingDirector);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Reject, null))).Succeeded); // rejection needs a reason
        Assert.True((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded);

        var rec = await Db(db => db.HiringRecommendations.SingleAsync(r => r.JobApplicationId == app));
        Assert.Equal((HiringStatus.Approved, _hod, _hrm, _md), (rec.Status, rec.HodUserId, rec.HrUserId, rec.MdUserId));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ro@grel.test") && m.Contains("approved"));
    }

    [Fact]
    public async Task Without_the_hods_endorsement_hr_must_give_a_reason_and_nobody_approves_twice()
    {
        var app = await InterviewedAsync(_candidate, "Ama");
        await LegacyRecommendationAsync(app, _hrm); // an HR Manager proposed (old chain) ...

        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Reject, null))).Succeeded);  // a reason is needed
        Assert.True((await Run(s => s.DecideAsync(app, HiringDecision.Reject, "Prefer an internal candidate"))).Succeeded);

        // ... so another HR Manager decides, and must explain an approval against the HOD.
        As(_hrm, Roles.HrManager);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Approve, "Fine"))).Succeeded);
        As(_hrm2, Roles.HrManager);
        Assert.True((await Run(s => s.GetCaseAsync(app)))!.HrNeedsOverride);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded);
        Assert.True((await Run(s => s.DecideAsync(app, HiringDecision.Approve, "No internal candidate applied"))).Succeeded);

        // The MD cannot also be the HR approver.
        As(_hrm2, Roles.HrManager, Roles.ManagingDirector);
        Assert.False((await Run(s => s.DecideAsync(app, HiringDecision.Approve, null))).Succeeded);
        As(_md, Roles.ManagingDirector);
        Assert.True((await Run(s => s.DecideAsync(app, HiringDecision.Reject, "Budget frozen this quarter"))).Succeeded);
        Assert.Equal(HiringStatus.Rejected, (await Db(db => db.HiringRecommendations.SingleAsync())).Status);

        // After a rejection a new recommendation can be made.
        AsRecruiter();
        Assert.True((await Run(s => s.GetCaseAsync(app)))!.CanRecommend);
    }

    [Fact]
    public async Task An_offer_needs_approval_cleared_checks_and_a_free_position()
    {
        var app = await InterviewedAsync(_candidate, "Ama");
        AsRecruiter();
        Assert.False((await Run(s => s.SendOfferAsync(app, null, null, null, null))).Succeeded); // not approved yet
        await RecommendAsync(app); // approved by the Recruitment Officer

        AsRecruiter();
        var noChecks = await Run(s => s.SendOfferAsync(app, null, null, null, null));
        Assert.Contains("background check", noChecks.Errors[0]);
        Assert.True((await Run(s => s.AddCheckAsync(app, CheckType.References, "Mr K. Owusu"))).Succeeded);
        Assert.False((await Run(s => s.SendOfferAsync(app, null, null, null, null))).Succeeded); // still pending
        var checkId = await Db(db => db.BackgroundChecks.Select(c => c.Id).FirstAsync());
        Assert.False((await Run(s => s.CompleteCheckAsync(checkId, CheckStatus.Concerns, null))).Succeeded); // notes needed
        Assert.True((await Run(s => s.CompleteCheckAsync(checkId, CheckStatus.Concerns, "Late twice in 2025; otherwise excellent"))).Succeeded);

        Assert.False((await Run(s => s.SendOfferAsync(app, null, null, Today.AddDays(45), null))).Succeeded); // reply-by too far
        var sent = await Run(s => s.SendOfferAsync(app, null, null, null, "Probation: 6 months."));
        Assert.True(sent.Succeeded, string.Join(" ", sent.Errors));
        var offer = await Db(db => db.Offers.SingleAsync());
        Assert.Equal((0m, Today.AddDays(7), OfferStatus.Sent), (offer.MonthlySalary, offer.ExpiresOn, offer.Status));
        Assert.Equal(ApplicationStatus.Offer, (await Db(db => db.JobApplications.SingleAsync(a => a.Id == app))).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("Offer of employment"));

        // The single position is now taken: a second candidate cannot be selected or approved.
        var second = await InterviewedAsync(_candidate2, "Kofi");
        AsRecruiter();
        Assert.Contains("position", (await Run(s => s.RecommendAsync(second, 8500, Today.AddDays(30), "Second best, a strong reserve."))).Errors[0]);
        Assert.Contains("position", (await Run(s => s.ProceedAsync([second]))).Errors[0]);
    }

    [Fact]
    public async Task The_candidate_accepts_or_declines_and_unanswered_offers_expire()
    {
        var app = await InterviewedAsync(_candidate, "Ama");
        await RecommendAsync(app);
        await ClearedCheckAsync(app);
        AsRecruiter();
        Assert.True((await Run(s => s.SendOfferAsync(app, null, null, null, null))).Succeeded);

        // Only the applicant sees and answers their offer.
        As(_candidate2, Roles.Candidate);
        Assert.Null(await Run(s => s.GetMyOfferAsync(app)));
        Assert.False((await Run(s => s.RespondToOfferAsync(app, true, null))).Succeeded);

        As(_candidate, Roles.Candidate);
        var letter = (await Run(s => s.GetMyOfferAsync(app)))!;
        Assert.Equal(("Process Engineer", "Apeminim", "hod", true), (letter.JobTitle, letter.SiteName, letter.ReportingTo, letter.CanRespond));
        Assert.True((await Run(s => s.RespondToOfferAsync(app, true, null))).Succeeded);
        Assert.Equal(ApplicationStatus.Hired, (await Db(db => db.JobApplications.SingleAsync(a => a.Id == app))).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ro@grel.test") && m.Contains("accepted"));
        Assert.False((await Run(s => s.RespondToOfferAsync(app, false, null))).Succeeded); // already answered

        // A second vacancy position lets us test decline and expiry.
        await Db(async db => { (await db.Vacancies.FindAsync(_vacancyId))!.NumberOfPositions = 3; return await db.SaveChangesAsync(); });
        var kofi = await InterviewedAsync(_candidate2, "Kofi");
        await RecommendAsync(kofi);
        await ClearedCheckAsync(kofi);
        AsRecruiter();
        Assert.True((await Run(s => s.SendOfferAsync(kofi, null, null, Today.AddDays(1), null))).Succeeded);
        Assert.Equal(0, await Run(s => s.ExpireOffersAsync(Today.AddDays(1))));
        Assert.Equal(1, await Run(s => s.ExpireOffersAsync(Today.AddDays(2))));
        var kofiApp = await Db(db => db.JobApplications.SingleAsync(a => a.Id == kofi));
        Assert.Equal(ApplicationStatus.Interview, kofiApp.Status); // back in the pool

        // After expiry a fresh offer can follow, which this candidate declines.
        AsRecruiter();
        Assert.True((await Run(s => s.SendOfferAsync(kofi, null, null, null, null))).Succeeded);
        As(_candidate2, Roles.Candidate);
        Assert.True((await Run(s => s.RespondToOfferAsync(kofi, false, "Accepted another job"))).Succeeded);
        Assert.Equal(ApplicationStatus.OfferDeclined, (await Db(db => db.JobApplications.SingleAsync(a => a.Id == kofi))).Status);
    }

    [Fact]
    public async Task Withdrawals_and_the_hiring_list()
    {
        var app = await InterviewedAsync(_candidate, "Ama");
        await LegacyRecommendationAsync(app, _ro); // an open (old-chain) recommendation can be withdrawn
        AsRecruiter();
        Assert.False((await Run(s => s.WithdrawRecommendationAsync(app, null))).Succeeded);
        Assert.True((await Run(s => s.WithdrawRecommendationAsync(app, "Candidate relocating abroad"))).Succeeded);
        await RecommendAsync(app);
        await ClearedCheckAsync(app);
        AsRecruiter();
        Assert.True((await Run(s => s.SendOfferAsync(app, null, null, null, null))).Succeeded);
        Assert.True((await Run(s => s.WithdrawOfferAsync(app, "Role restructured"))).Succeeded);
        Assert.Equal(ApplicationStatus.Interview, (await Db(db => db.JobApplications.SingleAsync(a => a.Id == app))).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("withdrawn"));

        As(_hrm, Roles.HrManager);
        Assert.Equal(2, (await Run(s => s.SearchAsync(new HiringFilter { Tab = "all" }))).Count);
        Assert.Single(await Run(s => s.SearchAsync(new HiringFilter { Tab = "approved", DivisionId = 2 })));
        Assert.Empty(await Run(s => s.SearchAsync(new HiringFilter { Tab = "all", DivisionId = 1 })));
        Assert.Single(await Run(s => s.SearchAsync(new HiringFilter { Tab = "offers" })));
        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Empty(await Run(s => s.SearchAsync(new HiringFilter { Tab = "all" })));
    }

    [Fact]
    public async Task The_hiring_case_and_list_show_the_validated_interview_scores_only()
    {
        // Ama's interview is validated with the HOD's scorecard; Kofi's is fully scored but still waiting for validation.
        var ama = await InterviewedAsync(_candidate, "Ama");
        var kofi = await InterviewedAsync(_candidate2, "Kofi", completedInterview: false);
        await Db(async db =>
        {
            foreach (var (app, status) in new[] { (ama, InterviewStatus.Completed), (kofi, InterviewStatus.AwaitingValidation) })
            {
                var i = await db.Interviews.SingleAsync(x => x.JobApplicationId == app);
                i.Status = status;
                db.Scorecards.Add(new Scorecard
                {
                    InterviewId = i.Id, PanelUserId = _hod, Recommendation = PanelRecommendation.StronglyRecommend,
                    Strengths = "Knows the line", WeightedScore = 84.0m, SubmittedAt = DateTime.UtcNow
                });
            }
            return await db.SaveChangesAsync();
        });

        As(_hrm, Roles.HrManager);
        var amaCase = (await Run(s => s.GetCaseAsync(ama)))!;
        var round = Assert.Single(amaCase.InterviewRounds);
        Assert.Equal(84.0m, round.Average);
        Assert.Null(round.WaitingFor);
        var score = Assert.Single(round.Scores);
        Assert.Equal((84.0m, PanelRecommendation.StronglyRecommend, "Knows the line", true), (score.Score, score.Recommendation, score.Strengths, score.IsChair));
        Assert.Equal(84.0m, amaCase.InterviewAverage);

        var kofiCase = (await Run(s => s.GetCaseAsync(kofi)))!;
        var waiting = Assert.Single(kofiCase.InterviewRounds);
        Assert.Empty(waiting.Scores);
        Assert.Null(waiting.Average);
        Assert.Contains("validate", waiting.WaitingFor);
        Assert.Null(kofiCase.InterviewAverage);

        // Validated, so Ama is ready for a hiring recommendation (the vacancy's recruiter gets the to-do); Kofi is not yet.
        var ready = Assert.Single(await Run(s => s.ListReadyToRecommendAsync()));
        Assert.Equal(("Ama", 84.0m, 1), (ready.ApplicantName, ready.InterviewAverage, ready.ValidatedRounds));
        AsRecruiter();
        Assert.Single(await Run(s => s.ListReadyToRecommendAsync(onlyMine: true)));

        // The list shows the validated average next to each recommendation; once recommended, Ama is no longer "ready".
        await RecommendAsync(ama);
        Assert.Empty(await Run(s => s.ListReadyToRecommendAsync()));
        As(_hrm, Roles.HrManager);
        Assert.Equal(84.0m, Assert.Single(await Run(s => s.SearchAsync(new HiringFilter { Tab = "all" }))).InterviewAverage);
    }

    [Fact]
    public async Task Proceeding_with_chosen_candidates_starts_a_draft_and_the_checks_which_the_recruiter_approves_or_discards()
    {
        var ama = await InterviewedAsync(_candidate, "Ama");
        var kofi = await InterviewedAsync(_candidate2, "Kofi");
        await Db(async db => { (await db.Vacancies.FindAsync(_vacancyId))!.NumberOfPositions = 2; return await db.SaveChangesAsync(); });

        // Only recruiters start the hiring process, and only for candidates with validated scores.
        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.ProceedAsync([ama]))).Succeeded);
        AsRecruiter();
        Assert.False((await Run(s => s.ProceedAsync([]))).Succeeded);
        var started = await Run(s => s.ProceedAsync([ama, kofi]));
        Assert.True(started.Succeeded, string.Join(" ", started.Errors));
        Assert.Equal(2, started.Value);

        // Each gets a draft (not yet with the HOD) and the five standard checks, pending; neither is "ready" any more.
        var draft = await Db(db => db.HiringRecommendations.SingleAsync(r => r.JobApplicationId == ama));
        Assert.Equal(HiringStatus.Draft, draft.Status);
        Assert.StartsWith("Interview results:", draft.Justification);
        Assert.Equal(Today.AddDays(30), draft.ProposedStartDate);
        Assert.Equal(5, await Db(db => db.BackgroundChecks.CountAsync(c => c.JobApplicationId == ama && c.Status == CheckStatus.Pending)));
        Assert.Empty(await Run(s => s.ListReadyToRecommendAsync()));
        Assert.True((await Run(s => s.GetCaseAsync(ama)))!.CanSubmitDraft);
        Assert.False((await Run(s => s.ProceedAsync([ama]))).Succeeded); // already started

        // Nobody decides on a draft; the recruiter sees it in "Awaiting my decision".
        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.DecideAsync(ama, HiringDecision.Approve, null))).Succeeded);
        AsRecruiter();
        Assert.Equal(2, (await Run(s => s.SearchAsync(new HiringFilter { Tab = "awaiting" }))).Count);

        // Approving the draft (with the recruiter's salary and words) approves the hire at once, same number; the HOD is not asked.
        await RecommendAsync(ama);
        var approved = await Db(db => db.HiringRecommendations.SingleAsync(r => r.JobApplicationId == ama));
        Assert.Equal((draft.Id, draft.Number, HiringStatus.Approved, 0m), (approved.Id, approved.Number, approved.Status, approved.ProposedSalary));
        Assert.DoesNotContain(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains(draft.Number));

        // Discarding Kofi's draft puts him back in "Ready to recommend"; his checks stay.
        AsRecruiter();
        Assert.True((await Run(s => s.DiscardDraftAsync(kofi))).Succeeded);
        Assert.Equal("Kofi", Assert.Single(await Run(s => s.ListReadyToRecommendAsync())).ApplicantName);
        Assert.Equal(5, await Db(db => db.BackgroundChecks.CountAsync(c => c.JobApplicationId == kofi)));
    }

    [Fact]
    public async Task Approving_the_hire_drafts_the_offer_which_the_recruiter_edits_previews_and_sends_with_one_click()
    {
        var app = await InterviewedAsync(_candidate, "Ama");
        await RecommendAsync(app);

        // Drafted with the approved salary and start date, the default reply-by date and terms, and its number.
        var draft = await Db(db => db.Offers.SingleAsync());
        Assert.Equal((OfferStatus.Draft, 0m, Today.AddDays(30), Today.AddDays(7)), (draft.Status, draft.MonthlySalary, draft.StartDate, draft.ExpiresOn));
        Assert.Contains("Probation: 6 months.", draft.Terms);
        Assert.StartsWith("OFF-", draft.Number);
        Assert.DoesNotContain(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("Offer of employment"));

        // The candidate can't see a draft; the recruiter and the HOD can preview it.
        As(_candidate, Roles.Candidate);
        Assert.Null(await Run(s => s.GetMyOfferAsync(app)));
        As(_hod, Roles.HeadOfDepartment);
        Assert.Equal(OfferStatus.Draft, (await Run(s => s.GetOfferLetterAsync(app)))!.Status);

        // The recruiter edits it while the checks are still to do (sending waits for them), within the approved salary.
        AsRecruiter();
        var caseBefore = (await Run(s => s.GetCaseAsync(app)))!;
        Assert.True(caseBefore.CanEditOffer);
        Assert.False(caseBefore.CanOffer);
        Assert.Equal(draft.Number, caseBefore.OfferDraft!.Number);
        Assert.True((await Run(s => s.SaveOfferDraftAsync(app, 8000, null, null, "Probation: 3 months."))).Succeeded);
        Assert.Contains("background check", (await Run(s => s.SendOfferAsync(app, null, null, null, null))).Errors[0]);

        // One click: the draft is sent as saved, same record and number.
        await ClearedCheckAsync(app);
        Assert.True((await Run(s => s.SendOfferAsync(app, null, null, null, null))).Succeeded);
        var sent = await Db(db => db.Offers.SingleAsync());
        Assert.Equal((draft.Id, draft.Number, OfferStatus.Sent, 0m, "Probation: 3 months."), (sent.Id, sent.Number, sent.Status, sent.MonthlySalary, sent.Terms));
        As(_candidate, Roles.Candidate);
        AsRecruiter();
        Assert.False((await Run(s => s.SaveOfferDraftAsync(app, 8000, null, null, null))).Succeeded); // already sent
    }
}
