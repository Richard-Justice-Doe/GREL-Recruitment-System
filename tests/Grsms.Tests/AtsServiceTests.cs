using System.IO.Compression;
using System.Text;
using Grsms.Application.Applications;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Documents;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Grsms.Tests;

/// <summary>
/// Phase 15, the ATS: reading real PDF and Word CVs, the rule-based CV reader, knockout questions, the automatic
/// rejection held for HR (and hidden from the applicant), confirm / override, re-screening, and the settings rules.
/// </summary>
public class AtsServiceTests : IAsyncLifetime
{
    private const int Apm = 31;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly TestDatabase _db = new();
    private string _hod = "", _ro = "", _hrm = "";

    /// <summary>A CV as text, one entry per line. Two jobs overlap (2019), so experience runs from Jan 2017 to now.</summary>
    private static readonly string[] CvLines =
    [
        "KWAME ASANTE MENSAH",
        "Email: kwame.mensah@example.com | Phone: +233 24 123 4567",
        "EDUCATION",
        "BSc Agriculture, KNUST, 2012 - 2016",
        "WORK EXPERIENCE",
        "Field Supervisor, Ghana Cocoa Board Jan 2017 - Dec 2019",
        "Plantation Manager, Golden Rubber Ltd Feb 2019 - Present",
        "SKILLS",
        "Risk assessment, Report writing, Team leadership",
        "CERTIFICATIONS",
        "NEBOSH International General Certificate (IGC)",
        "REFERENCES",
        "Available on request"
    ];

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);

    /// <summary>The saved emails with their bodies decoded (bodies with non-ASCII text are base64-encoded in the .eml files).</summary>
    private IEnumerable<string> DecodedEmails() => _db.SentEmails().Select(m =>
    {
        var split = m.IndexOf("\r\n\r\n", StringComparison.Ordinal) is var i and >= 0 ? i : m.IndexOf("\n\n", StringComparison.Ordinal);
        if (m.Contains("Content-Transfer-Encoding: quoted-printable"))
            return System.Text.RegularExpressions.Regex.Replace(m.Replace("=\r\n", "").Replace("=\n", ""), "=([0-9A-F]{2})",
                x => ((char)Convert.ToInt32(x.Groups[1].Value, 16)).ToString());
        if (split < 0 || !m.Contains("Content-Transfer-Encoding: base64")) return m;
        var body = string.Concat(m[split..].Where(c => !char.IsWhiteSpace(c)));
        return m[..split] + "\n\n" + Encoding.UTF8.GetString(Convert.FromBase64String(body));
    });

    private async Task<T> Scoped<T>(Func<IServiceProvider, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider);
    }

    private Task<JobApplication> LoadAsync(int id) =>
        Scoped(sp => sp.GetRequiredService<ApplicationDbContext>().JobApplications.Include(a => a.Actions).Include(a => a.KnockoutAnswers).SingleAsync(a => a.Id == id));

    /// <summary>A real PDF with one line of text per entry (built with PdfPig's writer).</summary>
    private static byte[] Pdf(IEnumerable<string> lines)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var y = 780.0;
        foreach (var line in lines)
        {
            page.AddText(line, 11, new PdfPoint(40, y), font);
            y -= 18;
        }
        return builder.Build();
    }

    /// <summary>A minimal real Word .docx with one paragraph per entry.</summary>
    private static byte[] Docx(IEnumerable<string> paragraphs)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var w = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
            foreach (var p in paragraphs)
                w.Write($"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(p)}</w:t></w:r></w:p>");
            w.Write("</w:body></w:document>");
        }
        return ms.ToArray();
    }

    /// <summary>
    /// A published vacancy (Diploma, 3 years, required "NEBOSH IGC" and "Risk assessment", preferred "Report writing",
    /// knockout "Do you have a valid driver's licence?") with a live advert. Returns the vacancy id and advert number.
    /// </summary>
    private Task<(int VacancyId, string AdvertNumber)> VacancyAsync() => Scoped(async sp =>
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var request = new RecruitmentRequest
        {
            RequestNumber = "RR-A-1", DivisionId = 2, DepartmentId = Apm, SiteId = 1, PositionTitle = "Safety Officer",
            NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
            Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
            Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
        };
        db.RecruitmentRequests.Add(request);
        await db.SaveChangesAsync();
        var v = new Vacancy
        {
            VacancyNumber = "VAC-A-1", RecruitmentRequestId = request.Id, JobTitle = "Safety Officer", DivisionId = 2, DepartmentId = Apm,
            SiteId = 1, RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent,
            VacancyType = VacancyType.InternalAndExternal, JobDescription = "Inspections.", MinimumEducation = EducationLevel.Diploma,
            MinYearsExperience = 3, ClosingDate = Today.AddDays(10), Status = VacancyStatus.Published,
            StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
        };
        v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Certification, Name = "NEBOSH IGC", IsEssential = true });
        v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = "Risk assessment", IsEssential = true });
        v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = "Report writing", IsEssential = false });
        v.KnockoutQuestions.Add(new VacancyKnockoutQuestion { Text = "Do you have a valid driver's licence?" });
        db.Vacancies.Add(v);
        await db.SaveChangesAsync();
        var a = new JobAdvert
        {
            AdvertNumber = "ADV-A-1", VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, Title = "Safety Officer",
            Body = "Text", HowToApply = "Online", StartDate = Today, EndDate = Today.AddDays(10), Status = AdvertStatus.Live,
            StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow, CreatedByUserId = _ro
        };
        a.Channels.Add(new AdvertChannelPost { Channel = AdvertChannel.CompanyWebsite, Status = ChannelPostStatus.Posted, PostedAt = DateTime.UtcNow });
        db.JobAdverts.Add(a);
        await db.SaveChangesAsync();
        return (v.Id, a.AdvertNumber);
    });

    /// <summary>
    /// A candidate applies with a profile that does NOT list NEBOSH (only the CV does), the given CV file, and the given
    /// knockout answer. Returns the application id and the candidate's id.
    /// </summary>
    private async Task<(int ApplicationId, string CandidateId)> ApplyAsync(string advert, string email, bool licence, string cvName, byte[] cv)
    {
        var id = await _db.CreateUserAsync(email, null, Roles.Candidate);
        As(id, Roles.Candidate);
        await Scoped(async sp =>
        {
            var profiles = sp.GetRequiredService<CandidateProfileService>();
            await profiles.SaveMineAsync(new ProfileFormDto
            {
                Phone = "0240000000", HighestEducation = EducationLevel.Diploma, YearsExperience = 4, Skills = "Risk assessment"
            });
            return await profiles.AddDocumentAsync(AttachmentCategory.Cv, cvName, new MemoryStream(cv));
        });
        var form = (await Scoped(sp => sp.GetRequiredService<ApplicationService>().GetApplyFormAsync(advert))).Value!;
        Assert.Single(form.Questions);
        form.Source = ApplicationSource.CareersPage;
        form.Confirm = true;

        // Unanswered knockout questions are refused.
        Assert.False((await Scoped(sp => sp.GetRequiredService<ApplicationService>().ApplyAsync(form))).Succeeded);

        form.Answers[form.Questions[0].Id] = licence;
        var applied = await Scoped(sp => sp.GetRequiredService<ApplicationService>().ApplyAsync(form));
        Assert.True(applied.Succeeded, string.Join(" ", applied.Errors));
        return (applied.Value, id);
    }

    // ------------------------------------------------------------------ the CV reader

    [Fact]
    public void The_rule_based_reader_extracts_contact_education_history_skills_and_certifications()
    {
        var cv = CvParser.Parse(string.Join("\n", CvLines), new DateOnly(2026, 11, 5));

        Assert.Equal("Kwame Asante Mensah", cv.FullName);
        Assert.Equal("kwame.mensah@example.com", cv.Email);
        Assert.Equal("+233 24 123 4567", cv.Phone);
        Assert.Equal(EducationLevel.Bachelors, cv.HighestEducation);
        Assert.Equal(2, cv.EmploymentHistory.Count);
        Assert.Equal("Field Supervisor, Ghana Cocoa Board", cv.EmploymentHistory[0].Description);
        Assert.True(cv.EmploymentHistory[1].IsCurrent);
        Assert.Equal(9, cv.YearsExperience); // Jan 2017 → Nov 2026, the 2019 overlap counted once
        Assert.Equal(["Risk assessment", "Report writing", "Team leadership"], cv.Skills);
        Assert.Equal(["NEBOSH International General Certificate (IGC)"], cv.Certifications);
        Assert.Empty(cv.Warnings);

        // A scanned CV (no text) is flagged rather than guessed.
        Assert.NotEmpty(CvParser.Parse("", Today).Warnings);
    }

    [Fact]
    public async Task Pdf_and_word_cvs_are_read_on_the_server()
    {
        var extractor = _db.Services.GetRequiredService<ICvTextExtractor>();
        var pdf = await extractor.ExtractAsync(new MemoryStream(Pdf(CvLines)), "cv.pdf");
        Assert.Contains("NEBOSH International General Certificate", pdf);
        Assert.Equal(EducationLevel.Bachelors, CvParser.Parse(pdf, Today).HighestEducation);

        var docx = await extractor.ExtractAsync(new MemoryStream(Docx(CvLines)), "cv.docx");
        Assert.Equal("KWAME ASANTE MENSAH", docx!.Split('\n')[0]);
        Assert.Equal(3, CvParser.Parse(docx, Today).Skills.Count);

        // Anything else, or a broken file, gives no text (screening then uses the profile only).
        Assert.Null(await extractor.ExtractAsync(new MemoryStream("hello"u8.ToArray()), "cv.txt"));
        Assert.Null(await extractor.ExtractAsync(new MemoryStream("%PDF-broken"u8.ToArray()), "cv.pdf"));
    }

    // ------------------------------------------------------------------ screening on submission

    [Fact]
    public async Task The_cv_adds_what_the_profile_misses_so_the_application_is_not_rejected()
    {
        var (_, advert) = await VacancyAsync();
        var (id, _) = await ApplyAsync(advert, "kwame@mail.test", licence: true, "cv.pdf", Pdf(CvLines));

        var a = await LoadAsync(id);
        Assert.Equal(ApplicationStatus.Submitted, a.Status);
        Assert.Equal(AtsDecision.None, a.AtsDecision);
        var r = a.ReadScreening()!;
        // NEBOSH is only in the CV; the CV also shows a Bachelor's degree (higher than the profile's Diploma).
        Assert.Empty(r.MissingRequiredCertifications);
        Assert.Equal(EducationLevel.Bachelors, r.EducationFound);
        Assert.Equal((100, 100, 100, 100), (r.Education, r.Experience, r.Skills, r.Certifications));
        Assert.Equal((100, AtsClassification.HighlyRecommended), (a.ScreeningScore!.Value, a.AtsClassification!.Value));
        Assert.Equal("rules", a.ReadCv()!.Reader);
        Assert.True(a.ReadCv()!.YearsExperience >= 9); // Jan 2017 to today (exact figure checked in the parser test)
        Assert.Contains("NEBOSH", a.CvText);
        Assert.Single(a.KnockoutAnswers, k => k.Answer);
    }

    [Fact]
    public async Task A_knockout_no_is_rejected_automatically_held_for_hr_and_hidden_from_the_applicant_until_confirmed()
    {
        var (vacancyId, advert) = await VacancyAsync();
        var (id, candidate) = await ApplyAsync(advert, "esi@mail.test", licence: false, "cv.docx", Docx(CvLines));

        var a = await LoadAsync(id);
        Assert.Equal((ApplicationStatus.AutoRejected, AtsDecision.AutoRejected), (a.Status, a.AtsDecision));
        Assert.Contains("driver's licence", a.AtsRejectionReason);
        Assert.Contains(a.Actions, x => x.Action == ApplicationAction.AtsRejected && x.ActorName == AtsService.SystemActor);

        // The applicant sees "Under review", no ATS step, and has not been told.
        As(candidate, Roles.Candidate);
        var mine = await Scoped(sp => sp.GetRequiredService<ApplicationService>().GetMineAsync(id));
        Assert.Equal(ApplicationStatus.UnderReview, mine!.Status);
        Assert.DoesNotContain(mine.Timeline, t => t.Item1.Contains("ATS") || t.Item1.Contains("rejected", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ApplicationStatus.UnderReview, (await Scoped(sp => sp.GetRequiredService<ApplicationService>().MyApplicationsAsync())).Single().Status);
        Assert.DoesNotContain(DecodedEmails(), m => m.Contains("esi@mail.test") && m.Contains("will not be taking your application further"));
        Assert.Contains(DecodedEmails(), m => m.Contains("ro@grel.test") && m.Contains("rejected it automatically"));

        // The HOD can see the dashboard but not decide; the recruiter can.
        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Scoped(sp => sp.GetRequiredService<AtsService>().ConfirmRejectionAsync(id, null))).Succeeded);
        var board = (await Scoped(sp => sp.GetRequiredService<AtsService>().GetBoardAsync(vacancyId)))!;
        Assert.Equal(1, board.AwaitingHr);
        Assert.False(board.Rows.Single().CanDecide);
        Assert.Equal(1, board.Coverage.Single(c => c.Name == "NEBOSH IGC").Have);

        As(_ro, Roles.RecruitmentOfficer);
        Assert.Equal(1, await Scoped(sp => sp.GetRequiredService<AtsService>().AwaitingHrCountAsync()));
        Assert.True((await Scoped(sp => sp.GetRequiredService<AtsService>().ConfirmRejectionAsync(id, "Driving is daily"))).Succeeded);

        a = await LoadAsync(id);
        Assert.Equal((ApplicationStatus.NotSuccessful, AtsDecision.Confirmed, _ro), (a.Status, a.AtsDecision, a.AtsDecisionByUserId));
        Assert.StartsWith("ATS:", a.OutcomeReason);
        Assert.Contains(DecodedEmails(), m => m.Contains("esi@mail.test") && m.Contains("will not be taking your application further"));

        // Every step is in the audit trail.
        var audits = await Scoped(sp => sp.GetRequiredService<ApplicationDbContext>().AuditLogs
            .Where(x => x.EntityName == "JobApplication" && x.EntityId == id.ToString()).ToListAsync());
        // The audit trail stores enum values as numbers: AutoRejected = 1, Confirmed = 2.
        Assert.Contains(audits, x => (x.NewValues ?? "").Contains($"\"AtsDecision\":{(int)AtsDecision.AutoRejected}"));
        Assert.Contains(audits, x => (x.NewValues ?? "").Contains($"\"AtsDecision\":{(int)AtsDecision.Confirmed}") && x.UserName == _ro);
        Assert.DoesNotContain(audits, x => (x.NewValues ?? "").Contains("CvText"));
    }

    [Fact]
    public async Task Hr_overrides_with_a_reason_and_relaxed_rules_clear_a_held_rejection_on_re_screening()
    {
        var (vacancyId, advert) = await VacancyAsync();
        var (first, _) = await ApplyAsync(advert, "a1@mail.test", licence: false, "cv.pdf", Pdf(CvLines));
        var (second, _) = await ApplyAsync(advert, "a2@mail.test", licence: false, "cv.pdf", Pdf(CvLines));

        As(_hrm, Roles.HrManager);
        var ats = (Func<IServiceProvider, AtsService>)(sp => sp.GetRequiredService<AtsService>());
        Assert.False((await Scoped(sp => ats(sp).OverrideAsync(first, "no"))).Succeeded); // a real reason is required
        Assert.True((await Scoped(sp => ats(sp).OverrideAsync(first, "A licence can be obtained before starting"))).Succeeded);
        var a = await LoadAsync(first);
        Assert.Equal((ApplicationStatus.UnderReview, AtsDecision.Overridden), (a.Status, a.AtsDecision));
        Assert.Contains(a.Actions, x => x.Action == ApplicationAction.AtsOverridden && x.Comments!.Contains("before starting"));

        // Only a Super Admin changes the rules, and the numbers must make sense.
        var form = await Scoped(sp => ats(sp).GetSettingsFormAsync());
        form.RejectOnKnockout = false;
        Assert.False((await Scoped(sp => ats(sp).SaveSettingsAsync(form))).Succeeded);
        As("super", Roles.SuperAdmin);
        Assert.False((await Scoped(sp => ats(sp).SaveSettingsAsync(new AtsSettingsFormDto { SkillsWeight = 40 }))).Succeeded);
        Assert.False((await Scoped(sp => ats(sp).SaveSettingsAsync(new AtsSettingsFormDto { UseAiReader = true }))).Succeeded); // no API key
        Assert.True((await Scoped(sp => ats(sp).SaveSettingsAsync(form))).Succeeded);

        // Re-screening with knockouts no longer rejecting clears the second one back to "Received".
        As(_ro, Roles.RecruitmentOfficer);
        Assert.Equal(2, (await Scoped(sp => ats(sp).RescreenVacancyAsync(vacancyId))).Value);
        a = await LoadAsync(second);
        Assert.Equal((ApplicationStatus.Submitted, AtsDecision.Cleared), (a.Status, a.AtsDecision));
        Assert.Contains(a.Actions, x => x.Action == ApplicationAction.AtsCleared);
        Assert.Equal(ApplicationStatus.UnderReview, (await LoadAsync(first)).Status); // the overridden one is untouched

        var overview = await Scoped(sp => ats(sp).OverviewAsync());
        Assert.Equal((2, 0), (overview.Single().Applications, overview.Single().AwaitingHr));
    }
}
