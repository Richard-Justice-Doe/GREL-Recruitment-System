using Grsms.Application.Onboarding;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Shared set-up for the onboarding tests (Module 9 and Phase 18): an APM HOD, the Head of IT, a recruiter, an HR Manager,
/// an unrelated staff member, two candidates and a closed Process Engineer vacancy; helpers for offers, starting the
/// onboarding, the letter, the new-hire documents and each workflow stage.
/// </summary>
public abstract class OnboardingTestBase : IAsyncLifetime
{
    protected const int Apm = 31, It = 2;
    protected static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    protected readonly TestDatabase _db = new();
    protected string _hod = "", _itHod = "", _ro = "", _hrm = "", _staff = "", _candidate = "", _candidate2 = "";
    protected int _vacancyId;

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _itHod = await _db.CreateUserAsync("ithod@grel.test", It, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
        _staff = await _db.CreateUserAsync("clerk@grel.test", 3, Roles.Employee);
        _candidate = await _db.CreateUserAsync("ama@mail.test", null, Roles.Candidate);
        _candidate2 = await _db.CreateUserAsync("kofi@mail.test", null, Roles.Candidate);

        _vacancyId = await Db(async db =>
        {
            (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
            (await db.Departments.FindAsync(It))!.HeadOfDepartmentUserId = _itHod;
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-O-1", DivisionId = 2, DepartmentId = Apm, SiteId = 2, PositionTitle = "Process Engineer",
                NumberOfPositions = 2, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var v = new Vacancy
            {
                VacancyNumber = "VAC-O-1", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm,
                SiteId = 2, RequestingHodUserId = _hod, RecruiterUserId = _ro, ReportingManagerUserId = _hod, EmploymentType = EmploymentType.Permanent,
                NumberOfPositions = 2, VacancyType = VacancyType.External, JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Bachelors,
                ClosingDate = Today.AddDays(-20), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
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

    protected void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);
    protected void AsRecruiter() => As(_ro, Roles.RecruitmentOfficer);
    protected void AsHrManager() => As(_hrm, Roles.HrManager);

    protected async Task<T> Run<T>(Func<OnboardingService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<OnboardingService>());
    }

    protected async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>An application with an approved hiring recommendation and an offer in the given state.</summary>
    protected Task<int> WithOfferAsync(string applicant, string name, OfferStatus status, DateOnly? start = null) => Db(async db =>
    {
        var a = new JobApplication
        {
            ApplicationNumber = "APP-O-" + Guid.NewGuid().ToString("N")[..6], VacancyId = _vacancyId, ApplicantUserId = applicant,
            Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, FullName = name,
            Email = $"{name.Split(' ')[0].ToLowerInvariant()}@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Bachelors,
            Status = HiringWorkflow.ApplicationStatusFor(status), SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
        };
        db.JobApplications.Add(a);
        await db.SaveChangesAsync();
        var rec = new HiringRecommendation
        {
            Number = "HIR-O-" + a.Id, JobApplicationId = a.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod,
            ProposedSalary = 8500, ProposedStartDate = start ?? Today.AddDays(20), Justification = "Best candidate.", CreatedByUserId = _ro,
            Status = HiringStatus.Approved
        };
        db.HiringRecommendations.Add(rec);
        await db.SaveChangesAsync();
        db.Offers.Add(new Offer
        {
            Number = "OFF-O-" + a.Id, JobApplicationId = a.Id, HiringRecommendationId = rec.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
            RequestingHodUserId = _hod, JobTitle = "Process Engineer", MonthlySalary = 8500, StartDate = start ?? Today.AddDays(20),
            ExpiresOn = Today.AddDays(5), Terms = "Probation: 6 months.", Status = status, SentAt = DateTime.UtcNow, SentByUserId = _ro,
            RespondedAt = status == OfferStatus.Accepted ? DateTime.UtcNow : null
        });
        await db.SaveChangesAsync();
        return a.Id;
    });

    protected async Task<int> StartAsync(int app, DateOnly? start = null)
    {
        AsRecruiter();
        var form = (await Run(s => s.GetStartFormAsync(app))).Value!;
        if (start is not null) form.StartDate = start;
        var r = await Run(s => s.StartAsync(form));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        return r.Value;
    }

    protected async Task IssueAndSignAsync(int id, int app)
    {
        AsHrManager();
        Assert.True((await Run(s => s.IssueLetterAsync(id, "Probation: 6 months. Hours: 8:00 to 17:00."))).Succeeded);
        As(_candidate, Roles.Candidate);
        Assert.True((await Run(s => s.SignLetterAsync(app, "Ama Mensah"))).Succeeded);
    }

    protected Task<List<OnboardingTask>> TasksAsync(int id) => Db(db => db.OnboardingTasks.Where(t => t.OnboardingRecordId == id).OrderBy(t => t.SortOrder).ToListAsync());

    protected async Task<T> RunDocs<T>(Func<DocumentVerificationService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<DocumentVerificationService>());
    }

    protected static MemoryStream FakePdf() => new(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n% fake scan\n%%EOF"));

    protected Task<List<EmployeeDocument>> DocumentsAsync(int id) =>
        Db(db => db.EmployeeDocuments.Where(d => d.OnboardingRecordId == id).OrderBy(d => d.DocumentType).ToListAsync());

    protected Task<OnboardingRecord> RecordAsync(int id) => Db(db => db.Onboardings.SingleAsync(o => o.Id == id));

    /// <summary>As the new employee: upload a PDF for every required document and mark every optional one not applicable.</summary>
    protected async Task FillDocumentsAsync(int id)
    {
        As(_candidate, Roles.Candidate);
        foreach (var d in await DocumentsAsync(id))
        {
            if (d.IsRequired)
            {
                var up = await RunDocs(s => s.UploadAsync(d.Id, $"{d.DocumentType}.pdf", FakePdf()));
                Assert.True(up.Succeeded, string.Join(" ", up.Errors));
            }
            else
                Assert.True((await RunDocs(s => s.MarkNotApplicableAsync(d.Id, "Does not apply to me"))).Succeeded);
        }
    }

    /// <summary>Started, letter issued and signed, documents uploaded and submitted: "Awaiting document verification".</summary>
    protected async Task<(int Id, int App)> SubmittedAsync(DateOnly? start = null)
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted, start);
        var id = await StartAsync(app, start);
        await IssueAndSignAsync(id, app);
        await FillDocumentsAsync(id);
        As(_candidate, Roles.Candidate);
        var submitted = await Run(s => s.SubmitAsync(id));
        Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));
        return (id, app);
    }

    /// <summary>As the recruiter: verify (or accept) every document.</summary>
    protected async Task VerifyAllAsync(int id)
    {
        AsRecruiter();
        foreach (var d in await DocumentsAsync(id))
            Assert.True((await RunDocs(s => s.VerifyAsync(d.Id))).Succeeded);
    }
}
