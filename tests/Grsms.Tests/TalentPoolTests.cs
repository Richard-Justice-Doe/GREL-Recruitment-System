using Grsms.Application.Applications;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>12 November 2026 (docs/34 quick win): past applicants who were not hired can be found again and invited to apply.</summary>
public class TalentPoolTests : OnboardingTestBase
{
    private async Task<T> RunPool<T>(Func<TalentPoolService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<TalentPoolService>());
    }

    private Task<int> PastApplicationAsync(string applicant, string name, ApplicationStatus status, string skills, int? score, bool shortlisted,
        EducationLevel education = EducationLevel.Bachelors) => Db(async db =>
    {
        // Each past application on its own (closed) vacancy: a person applies to a vacancy only once.
        var key = Guid.NewGuid().ToString("N")[..6];
        var request = new RecruitmentRequest
        {
            RequestNumber = "RR-TP-" + key, DivisionId = 2, DepartmentId = Apm, SiteId = 2, PositionTitle = "Process Engineer", NumberOfPositions = 1,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "X",
            EstimatedAnnualCost = 1, RequestedByUserId = _hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
        };
        db.RecruitmentRequests.Add(request);
        await db.SaveChangesAsync();
        var vacancy = new Vacancy
        {
            VacancyNumber = "VAC-TP-" + key, RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm, SiteId = 2,
            RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
            VacancyType = VacancyType.External, JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Diploma,
            ClosingDate = Today.AddDays(-20), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
        };
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync();
        var a = new JobApplication
        {
            ApplicationNumber = "APP-TP-" + key, VacancyId = vacancy.Id, ApplicantUserId = applicant,
            Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, FullName = name,
            Email = $"{name.Split(' ')[0].ToLowerInvariant()}@mail.test", Phone = "0240000000", HighestEducation = education, YearsExperience = 4,
            Skills = skills, ScreeningScore = score, ShortlistedAt = shortlisted ? DateTime.UtcNow.AddDays(-30) : null, Status = status,
            SubmittedAt = DateTime.UtcNow.AddDays(-40), StatusChangedAt = DateTime.UtcNow.AddDays(-10)
        };
        db.JobApplications.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    });

    /// <summary>A vacancy open now, with a published advert.</summary>
    private Task<(int VacancyId, string AdvertNumber)> OpenVacancyAsync() => Db(async db =>
    {
        var request = new RecruitmentRequest
        {
            RequestNumber = "RR-TP-1", DivisionId = 2, DepartmentId = Apm, SiteId = 2, PositionTitle = "Field Supervisor", NumberOfPositions = 1,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "X",
            EstimatedAnnualCost = 1, RequestedByUserId = _hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
        };
        db.RecruitmentRequests.Add(request);
        await db.SaveChangesAsync();
        var v = new Vacancy
        {
            VacancyNumber = "VAC-TP-1", RecruitmentRequestId = request.Id, JobTitle = "Field Supervisor", DivisionId = 2, DepartmentId = Apm, SiteId = 2,
            RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
            VacancyType = VacancyType.External, JobDescription = "Supervises tapping.", MinimumEducation = EducationLevel.Diploma,
            ClosingDate = Today.AddDays(14), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
        };
        db.Vacancies.Add(v);
        await db.SaveChangesAsync();
        db.JobAdverts.Add(new JobAdvert
        {
            AdvertNumber = "ADV-TP-0001", VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, Title = "Field Supervisor",
            Body = "Supervises tapping.", HowToApply = "Apply online.", StartDate = Today, EndDate = Today.AddDays(14), Status = AdvertStatus.Live,
            StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow, CreatedByUserId = _ro
        });
        await db.SaveChangesAsync();
        return (v.Id, "ADV-TP-0001");
    });

    [Fact]
    public async Task Past_applicants_are_found_by_skills_and_stage_but_hired_people_are_not()
    {
        await PastApplicationAsync(_candidate, "Ama Mensah", ApplicationStatus.NotSuccessful, "Rubber tapping, field safety", 72, shortlisted: true);
        await PastApplicationAsync(_candidate, "Ama Mensah", ApplicationStatus.Withdrawn, "Rubber tapping", 55, shortlisted: false); // same person again
        await PastApplicationAsync(_candidate2, "Kofi Boateng", ApplicationStatus.AutoRejected, "Accounting", 30, shortlisted: false, EducationLevel.Diploma);
        var hired = await _db.CreateUserAsync("yaw@mail.test", null, Roles.Candidate);
        await PastApplicationAsync(hired, "Yaw Darko", ApplicationStatus.NotSuccessful, "Rubber tapping", 90, shortlisted: true);
        await PastApplicationAsync(hired, "Yaw Darko", ApplicationStatus.Hired, "Rubber tapping", 90, shortlisted: true); // hired later: staff now

        AsRecruiter();
        var all = await RunPool(s => s.SearchAsync(new TalentPoolFilter()));
        Assert.Equal(["Ama Mensah", "Kofi Boateng"], all.Items.Select(p => p.FullName)); // one row per person, the furthest first
        var ama = all.Items[0];
        Assert.Equal((TalentStage.Shortlisted, (int?)72, 2), (ama.Reached, ama.BestScore, ama.Applications));

        Assert.Equal("Ama Mensah", Assert.Single((await RunPool(s => s.SearchAsync(new TalentPoolFilter { Search = "TAPPING" }))).Items).FullName);
        Assert.Single((await RunPool(s => s.SearchAsync(new TalentPoolFilter { Reached = "shortlisted" }))).Items);
        Assert.Single((await RunPool(s => s.SearchAsync(new TalentPoolFilter { MinEducation = EducationLevel.Bachelors }))).Items);

        As(_itHod, Roles.HeadOfDepartment); // another department's HOD sees nobody from APM
        Assert.Empty((await RunPool(s => s.SearchAsync(new TalentPoolFilter()))).Items);
        As(_hod, Roles.HeadOfDepartment);
        Assert.Equal(2, (await RunPool(s => s.SearchAsync(new TalentPoolFilter()))).TotalCount);
    }

    [Fact]
    public async Task A_recruiter_invites_someone_once_to_an_open_vacancy()
    {
        await PastApplicationAsync(_candidate, "Ama Mensah", ApplicationStatus.NotSuccessful, "Rubber tapping", 72, shortlisted: true);
        var (vacancyId, advert) = await OpenVacancyAsync();

        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await RunPool(s => s.InviteAsync(_candidate, vacancyId))).Succeeded); // HODs look, recruiters invite

        AsRecruiter();
        Assert.Equal("Field Supervisor", Assert.Single(await RunPool(s => s.InvitableVacanciesAsync())).JobTitle);
        Assert.True((await RunPool(s => s.InviteAsync(_candidate, vacancyId))).Succeeded);
        var invite = Assert.Single(_db.SentEmails());
        Assert.True(invite.Contains("To: ama@mail.test") && invite.Contains("GREL is recruiting: Field Supervisor")
                    && invite.Replace("=\r\n", "").Contains($"/Careers/Details/{advert}"), invite);
        Assert.Contains("already been invited", (await RunPool(s => s.InviteAsync(_candidate, vacancyId))).Errors[0]);
        Assert.False((await RunPool(s => s.InviteAsync(_candidate, _vacancyId))).Succeeded); // the closed vacancy
    }
}
