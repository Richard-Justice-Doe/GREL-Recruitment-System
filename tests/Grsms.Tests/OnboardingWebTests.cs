using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Onboarding through the real pages:
///   * the recruiter starts it from the hiring case, the HR Manager issues the appointment letter, the Head of IT ticks their
///     task from their home page, and the new employee reads and signs the letter and sees their first-day information;
///   * Phase 18, employee onboarding journey: the new employee uploads the new-hire documents in their portal and submits;
///     HR verifies them, the HR Manager approves, the induction is closed and HR activates the employee;
///   * Phase 18, document verification journey: HR rejects a document, sends it back, the new employee sees why, uploads it
///     again and resubmits.
/// </summary>
public class OnboardingWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<string> UserAsync(string email, string fullName, int? department, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is { } existing) return existing.Id;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, DepartmentId = department, CreatedAt = DateTime.UtcNow };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRolesAsync(user, roles);
        return user.Id;
    }

    private async Task<HttpClient> SignedInAsync(string email)
    {
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, Password)).StatusCode);
        return client;
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>The people of one scenario. <paramref name="Tag"/> keeps the e-mail addresses and numbers unique.</summary>
    private record Cast(string Tag, string Hod, string Ro, string Candidate, int ApplicationId)
    {
        public string Email(string who) => $"{Tag}.{who}@{(who == "cand" ? "mail" : "grel")}.test";
    }

    /// <summary>A Garage HOD, the Head of IT, a recruiter, an HR Manager and a candidate who has accepted an offer.</summary>
    private async Task<Cast> AcceptedOfferAsync(string tag, string candidateName, DateOnly start)
    {
        var hod = await UserAsync($"{tag}.hod@grel.test", "Garage Head", 27, Roles.HeadOfDepartment);          // Garage
        var itHod = await UserAsync($"{tag}.ithod@grel.test", "Head of IT", 2, Roles.HeadOfDepartment);        // IT
        var ro = await UserAsync($"{tag}.ro@grel.test", "Recruiter", 1, Roles.RecruitmentOfficer);
        await UserAsync($"{tag}.hrm@grel.test", "HR Manager", 1, Roles.HrManager);
        var candidate = await UserAsync($"{tag}.cand@mail.test", candidateName, null, Roles.Candidate);

        var applicationId = await Db(async db =>
        {
            (await db.Departments.FindAsync(2))!.HeadOfDepartmentUserId = itHod;
            (await db.Departments.FindAsync(27))!.HeadOfDepartmentUserId = hod;
            var request = new RecruitmentRequest
            {
                RequestNumber = $"RR-GAR-{tag}", DivisionId = 2, DepartmentId = 27, SiteId = 2, PositionTitle = "Diesel Mechanic",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Fleet.", BudgetCode = "GAR-26", EstimatedAnnualCost = 1, RequestedByUserId = hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = $"VAC-{tag}", RecruitmentRequestId = request.Id, JobTitle = "Diesel Mechanic", DivisionId = 2, DepartmentId = 27,
                SiteId = 2, RequestingHodUserId = hod, RecruiterUserId = ro, ReportingManagerUserId = hod, EmploymentType = EmploymentType.Permanent,
                NumberOfPositions = 1, VacancyType = VacancyType.External, JobDescription = "Engines.", MinimumEducation = EducationLevel.Certificate,
                ClosingDate = start.AddDays(-40), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var app = new JobApplication
            {
                ApplicationNumber = $"APP-{tag}", VacancyId = vacancy.Id, ApplicantUserId = candidate, Source = ApplicationSource.CareersPage,
                DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod, FullName = candidateName, Email = $"{tag}.cand@mail.test",
                Phone = "0240000000", HighestEducation = EducationLevel.Certificate, Status = ApplicationStatus.Hired,
                SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
            };
            db.JobApplications.Add(app);
            await db.SaveChangesAsync();
            var rec = new HiringRecommendation
            {
                Number = $"HIR-{tag}", JobApplicationId = app.Id, VacancyId = vacancy.Id, DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod,
                ProposedSalary = 3900, ProposedStartDate = start, Justification = "Best practical test.", CreatedByUserId = ro, Status = HiringStatus.Approved
            };
            db.HiringRecommendations.Add(rec);
            await db.SaveChangesAsync();
            db.Offers.Add(new Offer
            {
                Number = $"OFF-{tag}", JobApplicationId = app.Id, HiringRecommendationId = rec.Id, VacancyId = vacancy.Id, DivisionId = 2, DepartmentId = 27,
                RequestingHodUserId = hod, JobTitle = "Diesel Mechanic", MonthlySalary = 3900, StartDate = start, ExpiresOn = start.AddDays(-7),
                Status = OfferStatus.Accepted, SentAt = DateTime.UtcNow, SentByUserId = ro, RespondedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return app.Id;
        });
        return new Cast(tag, hod, ro, candidate, applicationId);
    }

    /// <summary>The recruiter starts the onboarding on the Start page. Returns the details URL and the onboarding id.</summary>
    private async Task<(string Url, int Id)> StartAsync(Cast c, DateOnly start)
    {
        var recruiter = await SignedInAsync(c.Email("ro"));
        var response = await recruiter.PostFormAsync($"/Onboarding/Start/{c.ApplicationId}", $"/Onboarding/Start/{c.ApplicationId}", new()
        {
            ["ApplicationId"] = c.ApplicationId.ToString(), ["StartDate"] = start.ToString("yyyy-MM-dd"), ["ReportTime"] = "07:30",
            ["ReportLocation"] = "Garage workshop, Abura", ["ContactPersonUserId"] = c.Hod, ["FirstDayNotes"] = "Wear safety boots."
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var url = response.Headers.Location!.ToString();
        return (url, int.Parse(url.Split('/').Last()));
    }

    /// <summary>The HR Manager issues the letter; the new employee signs it.</summary>
    private async Task LetterIssuedAndSignedAsync(Cast c, string url, int id, string fullName)
    {
        var hrm = await SignedInAsync(c.Email("hrm"));
        Assert.Equal(HttpStatusCode.Redirect, (await hrm.PostFormAsync(url, $"/Onboarding/IssueLetter/{id}",
            new() { ["terms"] = "Probation: 6 months. Hours: 7:30 to 16:30, Monday to Friday." })).StatusCode);
        var employee = await SignedInAsync(c.Email("cand"));
        var letterPage = $"/MyApplications/Appointment/{c.ApplicationId}";
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(letterPage, $"/MyApplications/SignLetter/{c.ApplicationId}",
            new() { ["fullName"] = fullName, ["confirm"] = "true" })).StatusCode);
    }

    private Task<List<EmployeeDocument>> DocumentsAsync(int onboardingId) =>
        Db(db => db.EmployeeDocuments.Where(d => d.OnboardingRecordId == onboardingId).OrderBy(d => d.DocumentType).ToListAsync());

    /// <summary>Uploads one PDF through a multipart form, with the page's anti-forgery token.</summary>
    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string pageUrl, string postUrl, int documentId, string fileName)
    {
        var page = await client.GetStringAsync(pageUrl);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var form = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" },
            { new StringContent(documentId.ToString()), "documentId" }
        };
        var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%scan\n%%EOF"));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", fileName);
        return await client.PostAsync(postUrl, form);
    }

    /// <summary>In the portal, the new employee uploads every required document, marks the optional ones not applicable and submits.</summary>
    private async Task<HttpClient> EmployeeSubmitsDocumentsAsync(Cast c, int onboardingId)
    {
        var employee = await SignedInAsync(c.Email("cand"));
        var page = $"/MyApplications/Documents/{c.ApplicationId}";
        var html = await employee.GetStringAsync(page);
        Assert.Contains("New-hire documents", html);
        Assert.Contains("Police clearance", html);
        foreach (var d in await DocumentsAsync(onboardingId))
        {
            var r = d.IsRequired
                ? await UploadAsync(employee, page, $"/MyApplications/UploadDocument/{c.ApplicationId}", d.Id, $"{d.DocumentType}.pdf")
                : await employee.PostFormAsync(page, $"/MyApplications/MarkNotApplicable/{c.ApplicationId}",
                    new() { ["documentId"] = d.Id.ToString(), ["reason"] = "Does not apply to me" });
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        }
        Assert.All(await DocumentsAsync(onboardingId), d => Assert.True(OnboardingWorkflow.ReadyToSubmit(d.Status)));
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(page, $"/MyApplications/SubmitDocuments/{c.ApplicationId}", new())).StatusCode);
        Assert.Contains("Your documents are with HR", await employee.GetStringAsync(page));
        return employee;
    }

    private static async Task VerifyAllAsync(HttpClient hr, string url, int id, IEnumerable<EmployeeDocument> docs)
    {
        foreach (var d in docs)
            Assert.Equal(HttpStatusCode.Redirect, (await hr.PostFormAsync(url, $"/Onboarding/VerifyDocument/{id}", new() { ["documentId"] = d.Id.ToString() })).StatusCode);
    }

    private Task<OnboardingStatus> StageAsync(int id) => Db(db => db.Onboardings.Where(o => o.Id == id).Select(o => o.Status).SingleAsync());

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Recruiter_starts_hr_manager_issues_it_ticks_and_the_new_employee_signs()
    {
        var start = Today.AddDays(14);
        var c = await AcceptedOfferAsync("o", "Efua Asante", start);

        // ---- Recruiter: the accepted offer is in their tasks and on the hiring case; start onboarding
        var recruiter = await SignedInAsync(c.Email("ro"));
        Assert.Contains("Onboard Efua Asante", await recruiter.GetStringAsync("/"));
        Assert.Contains("Start onboarding", await recruiter.GetStringAsync($"/Hiring/Case/{c.ApplicationId}"));
        var (details, id) = await StartAsync(c, start);
        Assert.Contains("Open the onboarding", await recruiter.GetStringAsync($"/Hiring/Case/{c.ApplicationId}"));
        Assert.Contains("Draft", await recruiter.GetStringAsync(details));

        // ---- HR Manager: issue the appointment letter
        var hrm = await SignedInAsync(c.Email("hrm"));
        Assert.Equal(HttpStatusCode.Redirect, (await hrm.PostFormAsync(details, $"/Onboarding/IssueLetter/{id}",
            new() { ["terms"] = "Probation: 6 months. Hours: 7:30 to 16:30, Monday to Friday." })).StatusCode);

        // ---- Head of IT: the task is on their home page; tick it on the onboarding page
        var it = await SignedInAsync(c.Email("ithod"));
        Assert.Contains("Create the email account and system access: Efua Asante", await it.GetStringAsync("/"));
        var itTaskId = await Db(db => db.OnboardingTasks
            .Where(t => t.OnboardingRecordId == id && t.Team == OnboardingTeam.InformationTechnology).Select(t => t.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await it.PostFormAsync(details, $"/Onboarding/UpdateTask/{id}",
            new() { ["taskId"] = itTaskId.ToString(), ["status"] = "Done", ["notes"] = "efua.asante@grel.example" })).StatusCode);
        Assert.DoesNotContain("New-hire documents", await it.GetStringAsync(details)); // personal documents are HR's only

        // ---- New employee: first-day information, then read and sign the letter
        var employee = await SignedInAsync(c.Email("cand"));
        var tracker = await employee.GetStringAsync($"/MyApplications/Details/{c.ApplicationId}");
        Assert.Contains("Your first day", tracker);
        Assert.Contains("Garage workshop, Abura", tracker);
        Assert.Contains("Upload my new-hire documents", tracker);
        Assert.Contains("Read and sign my appointment letter", tracker);
        var letterPage = $"/MyApplications/Appointment/{c.ApplicationId}";
        var letter = await employee.GetStringAsync(letterPage);
        Assert.Contains("Letter of appointment: Diesel Mechanic", letter);
        Assert.Contains("3,900.00", letter);
        await employee.PostFormAsync(letterPage, $"/MyApplications/SignLetter/{c.ApplicationId}", new() { ["fullName"] = "Efua Asante" }); // box not ticked
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(letterPage, $"/MyApplications/SignLetter/{c.ApplicationId}",
            new() { ["fullName"] = "Efua Asante", ["confirm"] = "true" })).StatusCode);
        Assert.Contains("Accepted and signed electronically", await employee.GetStringAsync(letterPage));

        var o = await Db(db => db.Onboardings.SingleAsync(x => x.JobApplicationId == c.ApplicationId));
        Assert.Equal(("Efua Asante", new TimeOnly(7, 30)), (o.LetterAcknowledgedName, o.ReportTime));

        // Candidates cannot open the staff onboarding screens.
        Assert.Contains("/Account/AccessDenied", (await employee.GetAsync(details)).Headers.Location!.ToString());
    }

    [Fact]
    public async Task Employee_onboarding_journey_from_documents_to_an_active_staff_account()
    {
        var c = await AcceptedOfferAsync("j", "Kwame Owusu", Today);
        var (url, id) = await StartAsync(c, Today);
        await LetterIssuedAndSignedAsync(c, url, id, "Kwame Owusu");

        // ---- The new employee uploads the documents in the portal and submits them
        await EmployeeSubmitsDocumentsAsync(c, id);
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, await StageAsync(id));

        // ---- The recruiter verifies every document and sends the onboarding to the HR Manager
        var recruiter = await SignedInAsync(c.Email("ro"));
        var page = await recruiter.GetStringAsync(url);
        Assert.Contains("New-hire documents", page);
        Assert.Contains("Verified against the original", page);
        await VerifyAllAsync(recruiter, url, id, await DocumentsAsync(id));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/MarkVerified/{id}", new())).StatusCode);
        Assert.Equal(OnboardingStatus.AwaitingHrApproval, await StageAsync(id));

        // ---- The HR Manager approves: the induction begins
        var hrm = await SignedInAsync(c.Email("hrm"));
        Assert.Contains("Approve: start the induction", await hrm.GetStringAsync(url));
        Assert.Equal(HttpStatusCode.Redirect, (await hrm.PostFormAsync(url, $"/Onboarding/HrDecide/{id}", new() { ["approve"] = "true" })).StatusCode);
        Assert.Equal(OnboardingStatus.AwaitingDepartmentalInduction, await StageAsync(id));

        // ---- Every checklist item ticked (HR may tick any), then the induction is closed
        foreach (var taskId in await Db(db => db.OnboardingTasks.Where(t => t.OnboardingRecordId == id).Select(t => t.Id).ToListAsync()))
            Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/UpdateTask/{id}",
                new() { ["taskId"] = taskId.ToString(), ["status"] = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/CompleteInduction/{id}", new())).StatusCode);
        Assert.Contains("Activate the employee", await recruiter.GetStringAsync(url));

        // ---- Activation: the account becomes a GREL staff account
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/Activate/{id}", new() { ["staffNumber"] = "GREL-9001" })).StatusCode);
        Assert.Equal(OnboardingStatus.Completed, await StageAsync(id));
        var account = await Db(db => db.Users.SingleAsync(u => u.Id == c.Candidate));
        Assert.Equal(("GREL-9001", false), (account.StaffNumber, account.IsCandidate));

        // The new employee now signs in as staff and can open staff screens.
        var staff = await SignedInAsync(c.Email("cand"));
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/RecruitmentRequests")).StatusCode);
    }

    [Fact]
    public async Task Document_verification_journey_rejected_documents_go_back_and_come_again()
    {
        var c = await AcceptedOfferAsync("v", "Abena Darko", Today.AddDays(10));
        var (url, id) = await StartAsync(c, Today.AddDays(10));
        await LetterIssuedAndSignedAsync(c, url, id, "Abena Darko");
        var employee = await EmployeeSubmitsDocumentsAsync(c, id);

        // ---- HR rejects the medical report and sends it back
        var recruiter = await SignedInAsync(c.Email("ro"));
        var medical = (await DocumentsAsync(id)).Single(d => d.DocumentType == EmployeeDocumentType.MedicalReport);
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/RejectDocument/{id}",
            new() { ["documentId"] = medical.Id.ToString(), ["reason"] = "Not signed by the doctor." })).StatusCode);
        Assert.Contains("Send rejected documents back", await recruiter.GetStringAsync(url));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/ReturnToEmployee/{id}", new())).StatusCode);
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, await StageAsync(id));

        // ---- The new employee sees why, uploads it again and resubmits
        var page = $"/MyApplications/Documents/{c.ApplicationId}";
        Assert.Contains("Rejected: Not signed by the doctor.", await employee.GetStringAsync(page));
        Assert.Equal(HttpStatusCode.Redirect, (await UploadAsync(employee, page, $"/MyApplications/UploadDocument/{c.ApplicationId}", medical.Id, "medical-signed.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(page, $"/MyApplications/SubmitDocuments/{c.ApplicationId}", new())).StatusCode);
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, await StageAsync(id));

        // ---- HR verifies everything; the files download for HR, never for another candidate
        await VerifyAllAsync(recruiter, url, id, await DocumentsAsync(id));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/MarkVerified/{id}", new())).StatusCode);
        Assert.Equal(OnboardingStatus.AwaitingHrApproval, await StageAsync(id));

        var fileId = await Db(db => db.Attachments.Where(a => a.OwnerType == "EmployeeDocument" && a.OwnerId == medical.Id)
            .OrderByDescending(a => a.Id).Select(a => a.Id).FirstAsync());
        var download = await recruiter.GetAsync($"/Onboarding/DownloadDocument/{id}?documentId={medical.Id}&attachmentId={fileId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        await UserAsync("v.other@mail.test", "Someone Else", null, Roles.Candidate);
        var other = await SignedInAsync("v.other@mail.test");
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync($"/MyApplications/DownloadDocument/{c.ApplicationId}?documentId={medical.Id}&attachmentId={fileId}")).StatusCode);
    }
}
