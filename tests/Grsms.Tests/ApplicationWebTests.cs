using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// The application journey through the real web pages: a visitor is sent to sign in from the job page; the candidate
/// fills in their profile, uploads a CV, applies and sees the tracker; the Recruitment Officer finds the application,
/// downloads the CV and starts the review, which the candidate then sees.
/// </summary>
public class ApplicationWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<string> UserAsync(string email, bool candidate, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is { } existing) return existing.Id;

        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], IsCandidate = candidate,
            DepartmentId = candidate ? null : 1, CreatedAt = DateTime.UtcNow
        };
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

    [Fact]
    public async Task Candidate_applies_from_the_careers_page_and_the_recruiter_reviews_it()
    {
        var hod = await UserAsync("w.hod@grel.test", false, Roles.HeadOfDepartment);
        var ro = await UserAsync("w.ro@grel.test", false, Roles.RecruitmentOfficer);
        await UserAsync("w.ama@mail.test", true, Roles.Candidate);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // A live advert on the careers page (its own workflow is covered by the advert tests).
        string advertNumber;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-GAR-2026-0097", DivisionId = 2, DepartmentId = 27, SiteId = 2, PositionTitle = "Mechanic",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Fleet growth.", BudgetCode = "GAR-26", EstimatedAnnualCost = 50000, RequestedByUserId = hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0097", RecruitmentRequestId = request.Id, JobTitle = "Heavy Duty Mechanic", DivisionId = 2,
                DepartmentId = 27, SiteId = 2, RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent,
                VacancyType = VacancyType.InternalAndExternal, JobDescription = "Maintains tractors and trucks.",
                MinimumEducation = EducationLevel.Certificate, ClosingDate = today.AddDays(7),
                Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var advert = new JobAdvert
            {
                AdvertNumber = "ADV-2026-0097", VacancyId = vacancy.Id, DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod,
                Title = "Heavy Duty Mechanic – Garage", Body = "Maintains tractors and trucks at Abura.", HowToApply = "Apply online.",
                StartDate = today, EndDate = today.AddDays(7), Status = AdvertStatus.Live, StatusChangedAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow, CreatedByUserId = ro
            };
            advert.Channels.Add(new AdvertChannelPost { Channel = AdvertChannel.CompanyWebsite, Status = ChannelPostStatus.Posted, PostedAt = DateTime.UtcNow });
            db.JobAdverts.Add(advert);
            await db.SaveChangesAsync();
            advertNumber = advert.AdvertNumber;
        }

        // ---- A visitor is asked to sign in to apply, and comes back to the apply page afterwards
        var visitorPage = await factory.NewClient().GetStringAsync($"/Careers/Details/{advertNumber}");
        Assert.Contains("Sign in to apply", visitorPage);
        Assert.Contains(WebUtility.UrlEncode($"/MyApplications/Apply/{advertNumber}"), visitorPage);

        // ---- The candidate: profile, CV, apply
        var candidate = await SignedInAsync("w.ama@mail.test");
        Assert.Contains("Apply now", await candidate.GetStringAsync($"/Careers/Details/{advertNumber}"));
        Assert.Contains("your CV", await candidate.GetStringAsync($"/MyApplications/Apply/{advertNumber}")); // profile not ready

        Assert.Equal(HttpStatusCode.Redirect, (await candidate.PostFormAsync("/Profile", "/Profile", new()
        {
            ["Phone"] = "0244000000", ["Location"] = "Agona Nkwanta", ["HighestEducation"] = "Certificate",
            ["YearsExperience"] = "6", ["Skills"] = "Diesel engines\nHydraulics"
        })).StatusCode);

        var profilePage = await candidate.GetStringAsync("/Profile");
        var token = Regex.Match(profilePage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var upload = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" },
            { new StringContent("Cv"), "category" }
        };
        var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%cv\n%%EOF"));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(pdf, "file", "Ama CV.pdf");
        Assert.Equal(HttpStatusCode.Redirect, (await candidate.PostAsync("/Profile/Upload", upload)).StatusCode);
        Assert.Contains("Your profile is ready", await candidate.GetStringAsync("/Profile"));

        var applied = await candidate.PostFormAsync($"/MyApplications/Apply/{advertNumber}", "/MyApplications/Apply", new()
        {
            ["AdvertNumber"] = advertNumber, ["Source"] = "Jobberman", ["CoverLetter"] = "Six years on Caterpillar fleets.", ["Confirm"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, applied.StatusCode);
        var trackerUrl = applied.Headers.Location!.ToString();
        var tracker = await candidate.GetStringAsync(trackerUrl);
        Assert.Contains("We have received your application", tracker);
        Assert.Contains("Ama CV.pdf", tracker);
        Assert.Contains("Heavy Duty Mechanic", await candidate.GetStringAsync("/MyApplications"));
        Assert.Contains("You have applied for this vacancy", await candidate.GetStringAsync($"/Careers/Details/{advertNumber}"));

        // Candidates cannot open the staff screens.
        Assert.Contains("/Account/AccessDenied", (await candidate.GetAsync("/Applications")).Headers.Location!.ToString());

        // ---- The Recruitment Officer: finds it, downloads the CV, starts the review
        var recruiter = await SignedInAsync("w.ro@grel.test");
        var list = await recruiter.GetStringAsync("/Applications?tab=new");
        var appId = Regex.Match(list, @"/Applications/Details/(\d+)").Groups[1].Value;
        var details = await recruiter.GetStringAsync($"/Applications/Details/{appId}");
        Assert.Contains("Six years on Caterpillar fleets.", details);
        Assert.Contains("Hydraulics", details);
        var attachmentId = Regex.Match(details, @"attachmentId=(\d+)").Groups[1].Value;
        var cv = await recruiter.GetAsync($"/Applications/Download/{appId}?attachmentId={attachmentId}");
        Assert.Equal(HttpStatusCode.OK, cv.StatusCode);
        Assert.StartsWith("%PDF", await cv.Content.ReadAsStringAsync());
        Assert.Equal("attachment", cv.Content.Headers.ContentDisposition!.DispositionType);
        // docs/37 CR4: the preview: a Preview button, the same file shown inline (not cached), PDF.js served as a JavaScript module.
        Assert.Contains("data-preview-kind=\"pdf\"", details);
        var inline = await recruiter.GetAsync($"/Applications/Download/{appId}?attachmentId={attachmentId}&view=inline");
        Assert.Equal("inline", inline.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("application/pdf", inline.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", inline.Headers.CacheControl!.ToString());
        var notWord = await recruiter.GetAsync($"/Applications/Download/{appId}?attachmentId={attachmentId}&view=html");
        Assert.Equal("attachment", notWord.Content.Headers.ContentDisposition!.DispositionType); // not a Word file: downloaded as usual
        var module = await recruiter.GetAsync("/lib/pdfjs/pdf.min.mjs");
        Assert.Equal(HttpStatusCode.OK, module.StatusCode);
        Assert.Contains("javascript", module.Content.Headers.ContentType!.MediaType);

        Assert.Equal(HttpStatusCode.Redirect,
            (await recruiter.PostFormAsync($"/Applications/Details/{appId}", $"/Applications/StartReview/{appId}", new())).StatusCode);
        Assert.Contains("Your application is being reviewed", await candidate.GetStringAsync(trackerUrl));

        // Another candidate cannot open this application or its CV.
        await UserAsync("w.kofi@mail.test", true, Roles.Candidate);
        var other = await SignedInAsync("w.kofi@mail.test");
        var myAppId = Regex.Match(trackerUrl, @"/(\d+)$").Groups[1].Value;
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(trackerUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/MyApplications/Download/{myAppId}?attachmentId={attachmentId}")).StatusCode);
    }
}
