using System.IO.Compression;
using System.Security;
using System.Text;
using Grsms.Application.Ai;
using Grsms.Application.Knowledge;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Hiring;
using Grsms.Domain.Knowledge;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-7: the knowledge base. HR uploads policies (PDF/Word, usual upload checks), the text is split into sections and
/// searched (BM25, headings count double); staff read and ask; new employees only see documents marked for them; the AI answers
/// only from the matching sections and its citations are checked; archived documents are no longer searched; the onboarding
/// assistant uses the extracts for policy questions.
/// </summary>
public class KnowledgeBaseTests : OnboardingTestBase
{
    private async Task<T> Kb<T>(Func<KnowledgeBaseService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>());
    }

    /// <summary>A minimal Word file: one paragraph per line.</summary>
    public static MemoryStream Docx(params string[] paragraphs)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
            foreach (var p in paragraphs) w.Write($"<w:p><w:r><w:t xml:space=\"preserve\">{SecurityElement.Escape(p)}</w:t></w:r></w:p>");
            w.Write("</w:body></w:document>");
        }
        ms.Position = 0;
        return ms;
    }

    public static readonly string[] LeavePolicy =
    [
        "1. Purpose",
        "This policy sets out the leave GREL staff may take.",
        "2. Annual leave",
        "Every permanent employee is entitled to 24 working days of annual leave in each calendar year, taken with the Head of Department's approval.",
        "Leave not taken by 31 March of the following year is forfeited unless HR agrees otherwise in writing.",
        "3. Sick leave",
        "An employee who is ill must inform their supervisor on the first day and bring a medical certificate after two days of absence.",
        "4. Maternity leave",
        "Female employees are entitled to 12 weeks of maternity leave with full pay."
    ];

    private static KnowledgeDocumentForm Form(string title, KnowledgeAudience audience = KnowledgeAudience.Staff) =>
        new() { Title = title, Owner = "HR Manager", Version = "2.1", EffectiveDate = new DateOnly(2026, 1, 1), Audience = audience };

    private async Task<int> UploadAsync(string title, KnowledgeAudience audience, params string[] paragraphs)
    {
        AsHrManager();
        var r = await Kb(k => k.UploadAsync(Form(title, audience), $"{title}.docx", Docx(paragraphs)));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        return r.Value;
    }

    private async Task SwitchOnAsync(params AiFeature[] features)
    {
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?>
            { [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiApiKey] = "k" }
            .Concat(features.Select(f => KeyValuePair.Create(SettingCatalogue.AiFeatureKey(f), (string?)"true"))).ToDictionary());
    }

    [Fact]
    public void Text_is_split_at_headings_and_long_sections_are_cut_and_marked_continued()
    {
        var parts = KnowledgeText.Split(string.Join("\n", LeavePolicy), "Leave Policy");
        Assert.Equal(["1. Purpose", "2. Annual leave", "3. Sick leave", "4. Maternity leave"], parts.Select(p => p.Heading));
        Assert.Contains("24 working days", parts[1].Text);

        var longText = "GENERAL RULES\n" + string.Join("\n", Enumerable.Range(1, 30).Select(i => $"Rule {i} applies to every employee at every site and estate without exception, in full."));
        var cut = KnowledgeText.Split(longText, "Code of Conduct");
        Assert.True(cut.Count >= 2);
        Assert.All(cut, c => Assert.True(c.Text.Length <= KnowledgeText.MaxSectionLength));
        Assert.Equal("GENERAL RULES", cut[0].Heading);
        Assert.Equal("GENERAL RULES (continued)", cut[1].Heading);

        // Sentences are not headings; numbered and capitals lines are.
        Assert.False(KnowledgeText.LooksLikeHeading("An employee who is ill must inform their supervisor."));
        Assert.True(KnowledgeText.LooksLikeHeading("5.2 Overtime"));
        Assert.True(KnowledgeText.LooksLikeHeading("CONDITIONS OF SERVICE"));
    }

    [Fact]
    public async Task Hr_uploads_staff_search_and_only_hr_may_change_the_library()
    {
        // Only holders of knowledge.manage may add; only PDF and Word.
        As(_staff, Roles.Employee);
        Assert.False((await Kb(k => k.UploadAsync(Form("Leave Policy"), "leave.docx", Docx(LeavePolicy)))).Succeeded);
        AsHrManager();
        Assert.Contains("file types", (await Kb(k => k.UploadAsync(Form("Leave Policy"), "leave.txt", new MemoryStream("text"u8.ToArray())))).Errors[0]);

        var leave = await UploadAsync("Leave Policy", KnowledgeAudience.Staff, LeavePolicy);
        var conduct = await UploadAsync("Code of Conduct", KnowledgeAudience.StaffAndNewEmployees,
            "1. Dress code", "Protective boots and helmets are worn on the estates and in the factories at all times.",
            "2. Working hours", "Normal working hours are 7:30 to 16:30, Monday to Friday.");
        var details = (await Kb(k => k.GetAsync(leave)))!;
        Assert.Equal(4, details.Document.SectionCount);
        Assert.Null(details.Document.ReadProblem);

        // Staff search: the annual-leave section comes first; the heading counts.
        As(_staff, Roles.Employee);
        var hits = await Kb(k => k.SearchAsync("How many days of annual leave do I get?", forNewEmployees: false));
        Assert.Equal("2. Annual leave", hits[0].Section.Heading);
        Assert.Equal("2. Working hours", (await Kb(k => k.SearchAsync("What are the working hours?", false)))[0].Section.Heading);
        Assert.Empty(await Kb(k => k.SearchAsync("the and of", false))); // only common words
        Assert.Equal(2, (await Kb(k => k.ListAsync())).Count);

        // New employees (through the assistant) only get documents marked for them.
        Assert.All(await Kb(k => k.SearchAsync("leave days working hours", forNewEmployees: true)), h => Assert.Equal(conduct, h.Section.DocumentId));

        // A candidate account sees no library.
        As(_candidate, Roles.Candidate);
        Assert.Empty(await Kb(k => k.ListAsync()));
        Assert.Empty(await Kb(k => k.SearchAsync("annual leave", forNewEmployees: false)));

        // Archived: no longer searched; HR still lists it.
        AsHrManager();
        Assert.True((await Kb(k => k.SetActiveAsync(leave, false))).Succeeded);
        As(_staff, Roles.Employee);
        Assert.DoesNotContain(await Kb(k => k.SearchAsync("annual leave", false)), h => h.Section.DocumentId == leave);
        Assert.Single(await Kb(k => k.ListAsync()));
        AsHrManager();
        Assert.Equal(2, (await Kb(k => k.ListAsync())).Count);
    }

    [Fact]
    public async Task A_new_version_is_read_again_keeps_the_old_file_and_a_scanned_file_is_flagged()
    {
        var id = await UploadAsync("Leave Policy", KnowledgeAudience.Staff, LeavePolicy);
        AsHrManager();
        Assert.True((await Kb(k => k.ReplaceFileAsync(id, "leave-v3.docx", Docx("1. Annual leave", "Every permanent employee is entitled to 26 working days."), "3.0"))).Succeeded);
        var d = (await Kb(k => k.GetAsync(id)))!;
        Assert.Equal(("3.0", 1, "leave-v3.docx", 2), (d.Document.Version, d.Document.SectionCount, d.Document.FileName, d.Files.Count));
        As(_staff, Roles.Employee);
        Assert.Contains("26 working days", (await Kb(k => k.SearchAsync("annual leave days", false)))[0].Section.Text);

        // Only the latest file opens for staff; HR can open the older one.
        Assert.Equal("leave-v3.docx", (await Kb(k => k.OpenFileAsync(id, null)))!.FileName);
        Assert.Equal("leave-v3.docx", (await Kb(k => k.OpenFileAsync(id, d.Files[^1].Id)))!.FileName);
        AsHrManager();
        Assert.Equal("Leave Policy.docx", (await Kb(k => k.OpenFileAsync(id, d.Files[^1].Id)))!.FileName);

        // A Word file with no text (like a scanned PDF): kept, but flagged and not searched.
        var empty = await UploadAsync("Scanned Handbook", KnowledgeAudience.Staff);
        var e = (await Kb(k => k.GetAsync(empty)))!;
        Assert.Equal(0, e.Document.SectionCount);
        Assert.Contains("No text could be read", e.Document.ReadProblem);

        // Deleted: gone for everyone.
        Assert.True((await Kb(k => k.DeleteAsync(empty))).Succeeded);
        Assert.Null(await Kb(k => k.GetAsync(empty)));
    }

    [Fact]
    public async Task New_employees_open_only_documents_marked_for_them_while_onboarding()
    {
        var staffOnly = await UploadAsync("Leave Policy", KnowledgeAudience.Staff, LeavePolicy);
        var forNewHires = await UploadAsync("Code of Conduct", KnowledgeAudience.StaffAndNewEmployees, "1. Dress code", "Boots on the estates.");

        As(_candidate, Roles.Candidate); // no onboarding yet
        Assert.Null(await Kb(k => k.OpenFileAsync(forNewHires, null)));

        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        await StartAsync(app);
        As(_candidate, Roles.Candidate);
        Assert.NotNull(await Kb(k => k.OpenFileAsync(forNewHires, null)));
        Assert.Null(await Kb(k => k.OpenFileAsync(staffOnly, null)));
        Assert.Null(await Kb(k => k.GetAsync(forNewHires))); // the reading page is for staff
    }

    [Fact]
    public async Task Answers_come_only_from_the_matching_sections_with_checked_citations()
    {
        await UploadAsync("Leave Policy", KnowledgeAudience.Staff, LeavePolicy);
        As(_staff, Roles.Employee);

        // AI off: the matching sections only, nothing sent.
        var off = await Kb(k => k.AskAsync("How many days of annual leave?"));
        Assert.Null(off.AiAnswer);
        Assert.Equal("2. Annual leave", off.Matches[0].Heading);
        Assert.Empty(_db.Ai.Calls);

        await SwitchOnAsync(AiFeature.KnowledgeAnswer);
        _db.Ai.Reply = _ => new AiReply("""{"answer":"You get 24 working days a year [1].","found":true,"sources":[1,9]}""", 500, 40, "m");
        As(_staff, Roles.Employee);
        var r = await Kb(k => k.AskAsync("How many days of annual leave?"));
        Assert.Null(r.Problem);
        Assert.Equal("You get 24 working days a year [1].", r.AiAnswer);
        Assert.True(r.Found);
        var cited = Assert.Single(r.Citations); // [9] does not exist and is dropped
        Assert.Equal(("Leave Policy", "2.1", "2. Annual leave"), (cited.DocumentTitle, cited.Version, cited.Heading));
        var call = Assert.Single(_db.Ai.Calls).Call;
        Assert.Contains("Answer ONLY from the sources", call.Instructions);
        Assert.Contains("[1] Leave Policy, version 2.1", call.Input);
        Assert.Contains("<question>How many days of annual leave?</question>", call.Input);

        // Not covered: no source claimed means not found.
        _db.Ai.Reply = _ => new AiReply("""{"answer":"GREL's documents don't cover this. HR can help.","found":false,"sources":[]}""", 500, 20, "m");
        Assert.False((await Kb(k => k.AskAsync("What is the sick leave pay rate?"))).Found);

        // No matching words at all: nothing is sent.
        var none = await Kb(k => k.AskAsync("xylophone zebra"));
        Assert.Contains("Nothing in GREL's documents matches", none.Problem);
        Assert.Equal(2, _db.Ai.Calls.Count);

        // Candidates can't ask.
        As(_candidate, Roles.Candidate);
        Assert.Equal("The knowledge base is for GREL staff.", (await Kb(k => k.AskAsync("annual leave"))).Problem);
    }

    [Fact]
    public async Task The_onboarding_assistant_answers_policy_questions_from_documents_marked_for_new_employees()
    {
        await UploadAsync("Leave Policy", KnowledgeAudience.Staff, LeavePolicy);
        var conduct = await UploadAsync("Code of Conduct", KnowledgeAudience.StaffAndNewEmployees,
            "1. Working hours", "Normal working hours are 7:30 to 16:30, Monday to Friday. Annual leave is in the Leave Policy.");
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        await StartAsync(app);
        await SwitchOnAsync(AiFeature.OnboardingAssistant, AiFeature.KnowledgeAnswer);
        _db.Ai.Reply = _ => new AiReply($$"""{"answer":"Work runs 7:30 to 16:30 (Code of Conduct, Working hours).","actions":["policy-{{conduct}}"]}""", 900, 30, "m");

        As(_candidate, Roles.Candidate);
        using var scope = _db.NewScope();
        var r = await scope.ServiceProvider.GetRequiredService<OnboardingAssistantService>().AskMineAsync(app, "What are the working hours and annual leave?", null);
        Assert.True(r.Succeeded, r.Problem);
        Assert.Equal($"/Knowledge/Open/{conduct}", Assert.Single(r.Value!.Actions).Url);
        var call = _db.Ai.Calls.Last().Call;
        Assert.Contains("<policies>", call.Input);
        Assert.Contains("Code of Conduct", call.Input);
        Assert.DoesNotContain("24 working days", call.Input); // the staff-only Leave Policy is never sent for a new employee
        Assert.Contains("use ONLY the numbered extracts", call.Instructions);
    }
}
