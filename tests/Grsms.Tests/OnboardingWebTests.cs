using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Probation;
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
///     again and resubmits;
///   * Phase 21, probation assessment journey: the HOD records the initial meeting, the employee signs it, the HOD records
///     both assessments, the HR Manager confirms and the employee opens the confirmation letter.
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

    /// <summary>The recruiter issues the letter (Phase 27); the new employee signs it.</summary>
    private async Task LetterIssuedAndSignedAsync(Cast c, string url, int id, string fullName)
    {
        // Before the letter is issued the page still opens (linked from "My onboarding"): first day, letter being prepared.
        var employee = await SignedInAsync(c.Email("cand"));
        var letterPage = $"/MyApplications/Appointment/{c.ApplicationId}";
        var waiting = await employee.GetStringAsync(letterPage);
        Assert.Contains("Your letter is being prepared", waiting);
        Assert.Contains("Your first day", waiting);
        Assert.DoesNotContain("Sign the letter", waiting);

        var recruiter = await SignedInAsync(c.Email("ro"));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/IssueLetter/{id}",
            new() { ["terms"] = "Probation: 6 months. Hours: 7:30 to 16:30, Monday to Friday." })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(letterPage, $"/MyApplications/SignLetter/{c.ApplicationId}",
            new() { ["fullName"] = fullName, ["confirm"] = "true" })).StatusCode);
    }

    private Task<List<EmployeeDocument>> DocumentsAsync(int onboardingId) =>
        Db(db => db.EmployeeDocuments.Where(d => d.OnboardingRecordId == onboardingId).OrderBy(d => d.Id).ToListAsync());

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

    /// <summary>
    /// In the portal, the new employee uploads every required document (two recommendation letters), marks the optional ones not
    /// applicable and signs the personal record. Phase 27: nothing to submit; the documents reach the RO as they are uploaded.
    /// </summary>
    private async Task<HttpClient> EmployeeSubmitsDocumentsAsync(Cast c, int onboardingId)
    {
        var employee = await SignedInAsync(c.Email("cand"));
        var page = $"/MyApplications/Documents/{c.ApplicationId}";
        var html = await employee.GetStringAsync(page);
        Assert.Contains("Onboarding documents", html);
        Assert.Contains("Police clearance", html);
        Assert.Contains("Education documents", html);
        foreach (var d in (await DocumentsAsync(onboardingId)).Where(d => d.IsApplicable))
        {
            if (d.IsRequired)
                for (var i = 0; i < (d.DocumentTypeCode == DefaultDocumentTypes.RecommendationLetters ? 2 : 1); i++)
                    Assert.Equal(HttpStatusCode.Redirect,
                        (await UploadAsync(employee, page, $"/MyApplications/UploadDocument/{c.ApplicationId}", d.Id, $"{d.DocumentTypeCode}-{i}.pdf")).StatusCode);
            else
                Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(page, $"/MyApplications/MarkNotApplicable/{c.ApplicationId}",
                    new() { ["documentId"] = d.Id.ToString(), ["reason"] = "Does not apply to me" })).StatusCode);
        }
        Assert.All((await DocumentsAsync(onboardingId)).Where(d => d.IsApplicable && d.IsRequired), d => Assert.Equal(EmployeeDocumentStatus.Submitted, d.Status));

        // Phase 19: the Personal Record Declaration, filled in and signed on its page (rows posted with their ".Index" keys).
        var recordPage = $"/MyApplications/PersonalRecord/{c.ApplicationId}";
        var form = await employee.GetStringAsync(recordPage);
        Assert.Contains("Beneficiaries", form);
        Assert.Contains("repeater.js", form);
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(recordPage, recordPage, new()
        {
            ["SsnitNumber"] = "C987654321098", ["DateOfBirth"] = Today.AddYears(-31).ToString("yyyy-MM-dd"), ["ResidentialAddress"] = "Kwesimintsim, Takoradi",
            ["HomeTown"] = "Elmina", ["Region"] = "Central", ["MobileNumber"] = "0244000111",
            ["NextOfKinName"] = "Yaw Mensah", ["NextOfKinRelationship"] = "Father", ["NextOfKinTelephone"] = "0208000222",
            ["Languages.Index"] = "0", ["Languages[0].Language"] = "English", ["Languages[0].Proficiency"] = "VeryFluent",
            ["BankAccounts.Index"] = "0", ["BankAccounts[0].BankName"] = "Absa", ["BankAccounts[0].AccountNumber"] = "0331001",
            ["EmergencyContacts.Index"] = "0", ["EmergencyContacts[0].Name"] = "Yaw Mensah", ["EmergencyContacts[0].Relationship"] = "Father",
            ["EmergencyContacts[0].Telephone1"] = "0208000222",
            ["Beneficiaries.Index"] = "k7", ["Beneficiaries[k7].Name"] = "Yaw Mensah", ["Beneficiaries[k7].Relationship"] = "Father",
            ["Beneficiaries[k7].Percentage"] = "100"
        })).StatusCode);
        var fullName = await Db(db => db.Users.Where(u => u.Id == c.Candidate).Select(u => u.FullName).SingleAsync());
        Assert.Contains(PersonalRecordServiceText, await employee.GetStringAsync(recordPage));
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(recordPage, $"/MyApplications/SignPersonalRecord/{c.ApplicationId}",
            new() { ["fullName"] = fullName, ["confirm"] = "true" })).StatusCode);
        Assert.NotNull(await Db(db => db.EmployeePersonalRecords.Where(r => r.OnboardingRecordId == onboardingId).Select(r => r.DeclaredAt).SingleAsync()));

        Assert.Contains("HR is checking your documents", await employee.GetStringAsync(page));
        return employee;
    }

    /// <summary>The recruiter verifies every uploaded document and accepts every "not applicable".</summary>
    private static async Task VerifyAllAsync(HttpClient hr, string url, int id, IEnumerable<EmployeeDocument> docs)
    {
        foreach (var d in docs.Where(d => d.IsApplicable && d.Status is not (EmployeeDocumentStatus.Pending or EmployeeDocumentStatus.Verified)))
            Assert.Equal(HttpStatusCode.Redirect, (await hr.PostFormAsync(url, $"/Onboarding/VerifyDocument/{id}", new() { ["documentId"] = d.Id.ToString() })).StatusCode);
    }

    private static readonly string PersonalRecordServiceText = System.Net.WebUtility.HtmlEncode(Application.Onboarding.PersonalRecordService.DeclarationText);

    private Task<OnboardingStatus> StageAsync(int id) => Db(db => db.Onboardings.Where(o => o.Id == id).Select(o => o.Status).SingleAsync());

    // ------------------------------------------------------------------ tests


    /// <summary>Phase 27: documents verified (→ ready), activated (the probation starts), every induction task ticked, induction closed.</summary>
    private async Task<HttpClient> ActivatedThroughPagesAsync(Cast c, string url, int id, string staffNumber)
    {
        await EmployeeSubmitsDocumentsAsync(c, id);
        var recruiter = await SignedInAsync(c.Email("ro"));
        await VerifyAllAsync(recruiter, url, id, await DocumentsAsync(id));
        Assert.Equal(OnboardingStatus.AwaitingActivation, await StageAsync(id));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/Activate/{id}", new() { ["staffNumber"] = staffNumber })).StatusCode);
        foreach (var taskId in await Db(db => db.OnboardingTasks.Where(t => t.OnboardingRecordId == id).Select(t => t.Id).ToListAsync()))
            Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/UpdateTask/{id}",
                new() { ["taskId"] = taskId.ToString(), ["status"] = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/CompleteInduction/{id}", new())).StatusCode);
        Assert.Equal(OnboardingStatus.Completed, await StageAsync(id));
        return recruiter;
    }

    [Fact]
    public async Task Probation_assessment_journey_from_the_initial_meeting_to_the_confirmation_letter()
    {
        var c = await AcceptedOfferAsync("p", "Kofi Asante", Today);
        var (url, id) = await StartAsync(c, Today);
        await LetterIssuedAndSignedAsync(c, url, id, "Kofi Asante");
        await ActivatedThroughPagesAsync(c, url, id, "GREL-9101");
        var probationId = await Db(db => db.ProbationRecords.Where(p => p.OnboardingRecordId == id).Select(p => p.Id).SingleAsync());
        var page = $"/Probation/Details/{probationId}";

        // ---- The HOD sees the new employee on their home page and records the initial meeting
        var hod = await SignedInAsync(c.Email("hod"));
        var home = await hod.GetStringAsync("/");
        Assert.Contains("My department&#x27;s new employees", home);
        Assert.Contains("Objectives pending", home);
        Assert.Contains("Kofi Asante", await hod.GetStringAsync("/Probation?tab=all"));
        Assert.Contains("Initial meeting", await hod.GetStringAsync(page));
        Assert.Equal(HttpStatusCode.Redirect, (await hod.PostFormAsync(page, $"/Probation/InitialMeeting/{probationId}", new()
        {
            ["MeetingDate"] = Today.ToString("yyyy-MM-dd"), ["Notes"] = "First week went well.",
            ["Objectives.Index"] = "a", ["Objectives[a].Objective"] = "Service the fleet on schedule", ["Objectives[a].SuccessIndicators"] = "No overdue services",
            ["DevelopmentPlan.Index"] = "b", ["DevelopmentPlan[b].TrainingNeed"] = "Hydraulics", ["DevelopmentPlan[b].Timeline"] = "Month 2",
            ["SignatureName"] = "Garage Head"
        })).StatusCode);

        // ---- The employee reads and signs it in My probation
        var employee = await SignedInAsync(c.Email("cand"));
        var mine = await employee.GetStringAsync("/MyProbation");
        Assert.Contains("Please sign your objectives", mine);
        Assert.Contains("Service the fleet on schedule", mine);
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync("/MyProbation", $"/MyProbation/Sign/{probationId}",
            new() { ["fullName"] = "Kofi Asante", ["confirm"] = "true" })).StatusCode);
        Assert.DoesNotContain("Please sign your objectives", await employee.GetStringAsync("/MyProbation"));

        // ---- Six months later: the first and final assessments (dates moved back so both are due)
        await Db(async db =>
        {
            var p = await db.ProbationRecords.SingleAsync(x => x.Id == probationId);
            p.StartDate = Today.AddMonths(-6); p.EndDate = Today; p.OriginalEndDate = Today;
            return await db.SaveChangesAsync();
        });
        var ratings = new Dictionary<string, string>
        {
            ["ApplicationOfSkills"] = "Good", ["QualityOfWork"] = "VeryGood", ["Productivity"] = "Good", ["Cooperation"] = "Outstanding",
            ["Dependability"] = "Good", ["AbilityToSupervise"] = "", ["Comments"] = "Reliable and careful."
        };
        Assert.Equal(HttpStatusCode.Redirect, (await hod.PostFormAsync(page, $"/Probation/Assess/{probationId}", ratings)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await hod.PostFormAsync(page, $"/Probation/Assess/{probationId}",
            new(ratings) { ["Outcome"] = "Confirm" })).StatusCode);
        Assert.Equal(2, await Db(db => db.ProbationAssessments.CountAsync(a => a.ProbationRecordId == probationId)));

        // ---- The HR Manager sees it pending on the dashboard and confirms
        var hrm = await SignedInAsync(c.Email("hrm"));
        Assert.Contains("Confirmations pending", await hrm.GetStringAsync("/"));
        Assert.Contains("Kofi Asante", await hrm.GetStringAsync("/Probation?tab=decision"));
        Assert.Equal(HttpStatusCode.Redirect, (await hrm.PostFormAsync(page, $"/Probation/HrDecide/{probationId}",
            new() { ["approve"] = "true", ["comments"] = "Confirmed." })).StatusCode);
        Assert.Equal(ProbationStatus.Confirmed, await Db(db => db.ProbationRecords.Where(p => p.Id == probationId).Select(p => p.Status).SingleAsync()));

        // ---- The employee opens the confirmation letter; the Head of IT cannot see the probation
        Assert.Contains("My confirmation letter", await employee.GetStringAsync("/MyProbation"));
        var letter = await employee.GetStringAsync($"/MyProbation/Letter/{probationId}");
        Assert.Contains("CNF-", letter);
        Assert.Contains("Diesel Mechanic", letter);
        Assert.Contains("CNF-", await hrm.GetStringAsync($"/Probation/Letter/{probationId}"));
        var itHod = await SignedInAsync(c.Email("ithod"));
        Assert.NotEqual(HttpStatusCode.OK, (await itHod.GetAsync(page)).StatusCode);

        // ---- The confirmations report lists the employee
        Assert.Contains("Kofi Asante", await hrm.GetStringAsync("/Reports?report=confirmations"));
    }
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
        Assert.Contains("Letter being prepared", await recruiter.GetStringAsync(details));

        // ---- Phase 27: the recruiter issues the appointment letter (the HR Manager only views)
        var hrm = await SignedInAsync(c.Email("hrm"));
        await hrm.PostFormAsync(details, $"/Onboarding/IssueLetter/{id}", new() { ["terms"] = "Probation: 6 months. Hours: 7:30 to 16:30, Monday to Friday." });
        Assert.Null(await Db(db => db.Onboardings.Where(o => o.Id == id).Select(o => o.LetterIssuedAt).SingleAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(details, $"/Onboarding/IssueLetter/{id}",
            new() { ["terms"] = "Probation: 6 months. Hours: 7:30 to 16:30, Monday to Friday." })).StatusCode);

        // ---- Head of IT: the task is on their home page; tick it on the onboarding page
        var it = await SignedInAsync(c.Email("ithod"));
        Assert.Contains("Create the email account and system access: Efua Asante", await it.GetStringAsync("/"));
        var itTaskId = await Db(db => db.OnboardingTasks
            .Where(t => t.OnboardingRecordId == id && t.Team == OnboardingTeam.InformationTechnology).Select(t => t.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await it.PostFormAsync(details, $"/Onboarding/UpdateTask/{id}",
            new() { ["taskId"] = itTaskId.ToString(), ["status"] = "Done", ["notes"] = "efua.asante@grel.example" })).StatusCode);
        Assert.DoesNotContain("Onboarding documents", await it.GetStringAsync(details)); // personal documents are HR's only

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

        // ---- The new employee uploads the documents in the portal: they reach the recruiter one by one
        await EmployeeSubmitsDocumentsAsync(c, id);
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, await StageAsync(id));

        // ---- The recruiter verifies every document: "Ready for activation" (no HR Manager approval)
        var recruiter = await SignedInAsync(c.Email("ro"));
        var page = await recruiter.GetStringAsync(url);
        Assert.Contains("Onboarding documents", page);
        Assert.Contains("Verified against the original", page);
        Assert.Contains("Start review", page);
        Assert.Contains("data-section-tabs", page);                 // usability U3: the page in topics (Documents, Checklist, ...)
        Assert.Contains("data-section=\"Checklist\"", page);
        var hrm = await SignedInAsync(c.Email("hrm"));
        Assert.DoesNotContain("Verified against the original", await hrm.GetStringAsync(url)); // the HR Manager only views
        await VerifyAllAsync(recruiter, url, id, await DocumentsAsync(id));
        Assert.Equal(OnboardingStatus.AwaitingActivation, await StageAsync(id));

        // ---- Activation, with the induction areas: the account becomes a GREL staff account and the induction begins
        var activatePage = await recruiter.GetStringAsync(url);
        Assert.Contains("Activate the employee", activatePage);
        Assert.Contains("Departmental induction areas", activatePage);                     // Phase 20: the 12 areas to tick
        Assert.Contains("TBU Operations", activatePage);
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/Activate/{id}",
            new() { ["staffNumber"] = "GREL-9001", ["areas"] = "Security" })).StatusCode);
        Assert.Equal(OnboardingStatus.AwaitingDepartmentalInduction, await StageAsync(id));
        var account = await Db(db => db.Users.SingleAsync(u => u.Id == c.Candidate));
        Assert.Equal(("GREL-9001", false), (account.StaffNumber, account.IsCandidate));
        Assert.Contains("Access card", await recruiter.GetStringAsync(url));                // the Security area's tasks were added

        // ---- Every checklist item ticked (the RO may tick any), then the induction is closed
        foreach (var taskId in await Db(db => db.OnboardingTasks.Where(t => t.OnboardingRecordId == id).Select(t => t.Id).ToListAsync()))
            Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/UpdateTask/{id}",
                new() { ["taskId"] = taskId.ToString(), ["status"] = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/CompleteInduction/{id}", new())).StatusCode);
        Assert.Equal(OnboardingStatus.Completed, await StageAsync(id));

        // ---- Phase 20: the personnel file was opened and filled; the recruiter opens it, a filed letter and a filed file
        var onboardingPage = await recruiter.GetStringAsync(url);
        var fileUrl = Regex.Match(onboardingPage, @"/PersonnelFiles/Details/\d+").Value;
        Assert.NotEmpty(fileUrl);
        var filePage = await recruiter.GetStringAsync(fileUrl);
        foreach (var text in new[] { "Identification Records", "Compliance Records", "Generated Letters", "Ghana Card", "Employee Personal Record Declaration (signed)" })
            Assert.Contains(text, filePage);
        var letterUrl = Regex.Match(filePage, @"(/PersonnelFiles/Letter/\d+)""[^>]*>Letter of appointment").Groups[1].Value;
        Assert.Contains("Letter of appointment", await recruiter.GetStringAsync(letterUrl));
        var downloadUrl = Regex.Match(filePage, @"/PersonnelFiles/Download/\d+").Value;
        var download = await recruiter.GetAsync(downloadUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("/Account/AccessDenied", (await (await SignedInAsync(c.Email("hod"))).GetAsync(fileUrl)).Headers.Location!.ToString());

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

        // ---- The recruiter starts the review of the medical report, then rejects it with a reason: it goes straight back
        var recruiter = await SignedInAsync(c.Email("ro"));
        var medical = (await DocumentsAsync(id)).Single(d => d.DocumentTypeCode == "MedicalReport");
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/StartReview/{id}",
            new() { ["documentId"] = medical.Id.ToString() })).StatusCode);
        Assert.Contains("Under review", await recruiter.GetStringAsync(url));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(url, $"/Onboarding/RejectDocument/{id}",
            new() { ["documentId"] = medical.Id.ToString(), ["reasonCode"] = "Other", ["reason"] = "Not signed by the doctor." })).StatusCode);
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, await StageAsync(id));

        // ---- The new employee sees why and uploads a replacement: it is resubmitted, no "Submit" needed
        var page = $"/MyApplications/Documents/{c.ApplicationId}";
        Assert.Contains("Rejected: Not signed by the doctor.", await employee.GetStringAsync(page));
        Assert.Equal(HttpStatusCode.Redirect, (await UploadAsync(employee, page, $"/MyApplications/UploadDocument/{c.ApplicationId}", medical.Id, "medical-signed.pdf")).StatusCode);
        Assert.Equal(EmployeeDocumentStatus.Resubmitted, (await DocumentsAsync(id)).Single(d => d.Id == medical.Id).Status);
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, await StageAsync(id));

        // ---- The recruiter verifies everything; the files download for HR, never for another candidate
        await VerifyAllAsync(recruiter, url, id, await DocumentsAsync(id));
        Assert.Equal(OnboardingStatus.AwaitingActivation, await StageAsync(id));

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

    [Fact]
    public async Task Hr_administrator_sets_an_induction_area_owner_and_tasks()
    {
        var owner = await UserAsync("ia.security@grel.test", "Security Chief", 1, Roles.Employee);
        await UserAsync("ia.admin@grel.test", "HR Admin", 1, Roles.HrAdministrator);
        await UserAsync("ia.ro@grel.test", "Recruiter", 1, Roles.RecruitmentOfficer);

        var admin = await SignedInAsync("ia.admin@grel.test");
        var page = await admin.GetStringAsync("/InductionAreas");
        foreach (var area in new[] { "Human Resource", "Corporate Information Technology", "Corporate Procurement", "Corporate Supply Chain", "Corporate Finance",
                                     "Factory Operations", "TBU Operations", "Stores Operations", "Engineering", "Security", "Health &amp; Safety", "Administration" })
            Assert.Contains(area, page);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/InductionAreas", "/InductionAreas/Save", new()
        {
            ["area"] = "Security", ["ownerUserId"] = owner, ["isDefault"] = "true", ["tasks"] = "Access card\nSecurity induction\nKey register"
        })).StatusCode);
        var saved = await Db(db => db.InductionAreaSettings.SingleAsync(s => s.Area == InductionArea.Security));
        Assert.Equal((owner, "Access card\nSecurity induction\nKey register"), (saved.OwnerUserId, saved.Tasks));

        // Recruiters run onboardings but do not change the areas.
        var ro = await SignedInAsync("ia.ro@grel.test");
        Assert.Contains("/Account/AccessDenied", (await ro.GetAsync("/InductionAreas")).Headers.Location!.ToString());
    }

    [Fact]
    public async Task Hr_administrator_maintains_the_onboarding_documents()
    {
        await UserAsync("od.admin@grel.test", "HR Admin", 1, Roles.HrAdministrator);
        await UserAsync("od.ro@grel.test", "Recruiter", 1, Roles.RecruitmentOfficer);

        var admin = await SignedInAsync("od.admin@grel.test");
        var page = await admin.GetStringAsync("/OnboardingDocuments");
        foreach (var text in new[] { "Personal documents", "Education documents", "Expatriate documents", "WASSCE / SSSCE certificate", "Work permit", "Old checklist" })
            Assert.Contains(text, page);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/OnboardingDocuments", "/OnboardingDocuments/Save", new()
        {
            ["Name"] = "Food handler's certificate", ["Description"] = "For canteen staff.", ["Group"] = "Employment", ["Requirement"] = "Optional",
            ["MinFiles"] = "1", ["PersonnelFileCategoryId"] = "4", ["SortOrder"] = "170", ["IsActive"] = "true"
        })).StatusCode);
        var added = await Db(db => db.OnboardingDocumentTypes.SingleAsync(t => t.Code == "FoodHandlersCertificate"));
        Assert.Equal((DocumentRequirement.Optional, true, false), (added.Requirement, added.IsActive, added.AllowsWordFiles));

        // Retiring: an unticked "Active" box posts false.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/OnboardingDocuments", "/OnboardingDocuments/Save", new()
        {
            ["Id"] = added.Id.ToString(), ["Version"] = added.Version.ToString(), ["Name"] = added.Name, ["Group"] = "Employment",
            ["Requirement"] = "Optional", ["MinFiles"] = "1", ["PersonnelFileCategoryId"] = "4", ["SortOrder"] = "170", ["IsActive"] = "false"
        })).StatusCode);
        Assert.False(await Db(db => db.OnboardingDocumentTypes.Where(t => t.Id == added.Id).Select(t => t.IsActive).SingleAsync()));

        var ro = await SignedInAsync("od.ro@grel.test");
        Assert.Contains("/Account/AccessDenied", (await ro.GetAsync("/OnboardingDocuments")).Headers.Location!.ToString());
    }

    [Fact]
    public async Task Onboarding_and_probation_actions_are_blocked_by_permission_before_they_run()
    {
        // Codebase audit B8: per-action policies on top of the service checks; probation now uses permissions too.
        await UserAsync("pp.hrm@grel.test", "HR Manager", 1, Roles.HrManager);
        await UserAsync("pp.ro@grel.test", "Recruiter", 1, Roles.RecruitmentOfficer);
        var hrm = await SignedInAsync("pp.hrm@grel.test");
        var ro = await SignedInAsync("pp.ro@grel.test");

        // The HR Manager (view only, and activation) is refused verification, management and correction actions.
        foreach (var action in new[] { "StartReview", "VerifyDocument", "RejectDocument", "RequestCorrection", "IssueLetter", "Cancel", "SetRequirements" })
            Assert.Contains("/Account/AccessDenied", (await hrm.PostFormAsync("/Onboarding", $"/Onboarding/{action}/999999", new())).Headers.Location!.ToString());
        // The Recruitment Officer reaches them (the service then says the record does not exist).
        Assert.DoesNotContain("AccessDenied", (await ro.PostFormAsync("/Onboarding", "/Onboarding/StartReview/999999", new() { ["documentId"] = "1" })).Headers.Location!.ToString());

        // Probation decisions: probation.decide (HR Manager by default), not the Recruitment Officer.
        Assert.Contains("/Account/AccessDenied", (await ro.PostFormAsync("/Probation", "/Probation/HrDecide/999999", new() { ["approve"] = "true" })).Headers.Location!.ToString());
        Assert.DoesNotContain("AccessDenied", (await hrm.PostFormAsync("/Probation", "/Probation/HrDecide/999999", new() { ["approve"] = "true" })).Headers.Location!.ToString());
    }

    [Fact]
    public async Task Each_person_gets_a_short_menu()
    {
        // Usability U1: Candidates (applications + screening), one Administration link, HODs without Screening and Hiring,
        // staff's own job search in the menu under their name, a flat menu for occasional users.
        await UserAsync("m.ro@grel.test", "Recruiter", 1, Roles.RecruitmentOfficer);
        await UserAsync("m.hod@grel.test", "Head", 3, Roles.HeadOfDepartment);
        await UserAsync("m.panel@grel.test", "Panel Member", 3, Roles.PanelMember);
        await UserAsync("m.admin@grel.test", "HR Admin", 1, Roles.HrAdministrator);

        var ro = await (await SignedInAsync("m.ro@grel.test")).GetStringAsync("/");
        Assert.Contains(">Candidates", ro.Replace("</i> ", ">"));
        Assert.Contains("Hiring &amp; offers", ro);
        Assert.DoesNotContain("ATS screening", ro);
        Assert.DoesNotContain("My career", ro);
        Assert.Contains("My applications", ro);              // under the name menu
        Assert.Contains("<details class=\"nav-group", ro);   // grouped: many links

        var hod = await SignedInAsync("m.hod@grel.test");
        var hodHome = await hod.GetStringAsync("/");
        Assert.DoesNotContain("Hiring &amp; offers", hodHome);
        Assert.DoesNotContain(">Screening<", await hod.GetStringAsync("/Applications"));

        var panel = await (await SignedInAsync("m.panel@grel.test")).GetStringAsync("/");
        Assert.Contains("Interviews", panel);
        Assert.DoesNotContain("<details class=\"nav-group", panel); // flat: few links

        var admin = await SignedInAsync("m.admin@grel.test");
        Assert.Contains("Administration", await admin.GetStringAsync("/"));
        var adminPage = await admin.GetStringAsync("/Administration");
        foreach (var screen in new[] { "Departments", "Induction areas", "Onboarding documents", "Users &amp; roles", "Audit trail" })
            Assert.Contains(screen, adminPage);
        Assert.DoesNotContain("Backups", adminPage); // Super Admin only
        var panelClient = await SignedInAsync("m.panel@grel.test");
        Assert.Contains("/Account/AccessDenied", (await panelClient.GetAsync("/Administration")).Headers.Location!.ToString()); // nothing to administer
    }
}
