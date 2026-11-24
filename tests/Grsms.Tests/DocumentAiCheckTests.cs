using Grsms.Application.Ai;
using Grsms.Application.Onboarding;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-8: the optional AI document check. Off unless its own switch is on; only whoever verifies documents asks for it;
/// the document's own files are sent (at most two, pictures or PDFs) with the expected type; the model is told never to repeat
/// names or numbers; the verdict is a staff-only flag on the document, never seen by the new employee, and goes out of date when the
/// files change; nothing is verified or rejected by it.
/// </summary>
public class DocumentAiCheckTests : OnboardingTestBase
{
    private async Task<T> Checks<T>(Func<DocumentAiCheckService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<DocumentAiCheckService>());
    }

    private async Task SwitchOnAsync()
    {
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?>
        {
            [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiApiKey] = "k",
            [SettingCatalogue.AiFeatureKey(AiFeature.DocumentCheck)] = "true"
        });
    }

    private async Task<IReadOnlyList<DocumentFlag>> StaffFlagsAsync(int onboardingId, int documentId)
    {
        using var scope = _db.NewScope();
        var flags = await scope.ServiceProvider.GetRequiredService<DocumentCheckService>().ForOnboardingsAsync([onboardingId], Today);
        return flags.GetValueOrDefault(documentId) ?? [];
    }

    [Fact]
    public async Task The_ro_checks_a_document_with_ai_and_staff_see_the_verdict_as_a_flag()
    {
        var (id, app) = await SubmittedAsync();
        var card = await DocAsync(id, "GhanaCard");

        // Off by default: nothing is sent.
        AsRecruiter();
        Assert.Contains("switched off", (await Checks(c => c.CheckAsync(card.Id, id))).Errors[0]);
        Assert.Empty(_db.Ai.Calls);

        await SwitchOnAsync();
        _db.Ai.Reply = _ => new AiReply("""
            {"matches_expected":false,"detected_type":"a bank statement","readable":true,"confidence":"High","issues":["Not an ID card"]}
            """, 1500, 60, "m");

        // Only whoever verifies documents; the new employee can't; another onboarding's id is refused.
        As(_candidate, Roles.Candidate);
        Assert.False((await Checks(c => c.CheckAsync(card.Id, id))).Succeeded);
        AsHrManager(); // HR Managers view onboarding but don't verify documents
        Assert.False((await Checks(c => c.CheckAsync(card.Id, id))).Succeeded);
        AsRecruiter();
        Assert.Equal("Document not found.", (await Checks(c => c.CheckAsync(card.Id, id + 999))).Errors[0]);
        Assert.Empty(_db.Ai.Calls);

        var r = await Checks(c => c.CheckAsync(card.Id, id));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        Assert.False(r.Value!.MatchesExpected);

        // What was sent: the document's file (a PDF) and the expected type; never asked to transcribe details.
        var call = Assert.Single(_db.Ai.Calls).Call;
        var file = Assert.Single(call.Files!);
        Assert.Equal((AiFile.Pdf, "GhanaCard-0.pdf"), (file.MediaType, file.FileName));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(file.Content));
        Assert.Contains("<expected>Ghana Card", call.Input);
        Assert.Contains("Never write any name, number, date or address", call.Instructions);
        Assert.Equal(AiOutcome.Success, (await Db(db => db.AiRequestLogs.SingleAsync())).Outcome);

        // Staff see a flag; the document's status is unchanged; the new employee never sees it.
        AsRecruiter();
        var flag = Assert.Single(await StaffFlagsAsync(id, card.Id), f => f.Code.StartsWith("ai-"));
        Assert.Equal(("ai-wrong-type", "danger", true), (flag.Code, flag.Severity, flag.StaffOnly));
        Assert.Contains("a bank statement", flag.Message);
        Assert.Equal(EmployeeDocumentStatus.Submitted, (await DocAsync(id, "GhanaCard")).Status);
        As(_candidate, Roles.Candidate);
        var mine = (await RunDocs(s => s.GetMineAsync(app)))!.Documents.Single(d => d.Id == card.Id);
        Assert.DoesNotContain(mine.Flags ?? [], f => f.Code.StartsWith("ai-"));

        // A new file makes the check out of date.
        Assert.True((await RunDocs(s => s.UploadAsync(card.Id, "ghana-card-back.pdf", FakePdf()))).Succeeded);
        AsRecruiter();
        Assert.Equal("ai-outdated", Assert.Single(await StaffFlagsAsync(id, card.Id), f => f.Code.StartsWith("ai-")).Code);

        // Switched off again: the AI flags disappear (and no extra query is made).
        using (var scope = _db.NewScope())
            await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?> { [SettingCatalogue.AiFeatureKey(AiFeature.DocumentCheck)] = "false" });
        Assert.DoesNotContain(await StaffFlagsAsync(id, card.Id), f => f.Code.StartsWith("ai-"));
    }

    [Fact]
    public void Verdicts_become_clear_flags()
    {
        var file = new Domain.Documents.Attachment { Id = 7, OriginalFileName = "card.jpg", SizeBytes = 1000 };
        DocumentFlag Flag(DocumentAiVerdict v) => Assert.Single(DocumentAiCheckService.Flags(new StoredDocumentCheck(v, [7]), DateTime.UtcNow, [file], "Ghana Card"));

        Assert.Equal(("ai-ok", "info"), Flag(new(true, "a Ghana Card", true, AiConfidence.High, [])) is var ok ? (ok.Code, ok.Severity) : default);
        Assert.Equal(("ai-unreadable", "warning"), Flag(new(true, "a Ghana Card", false, AiConfidence.Medium, ["blurred"])) is var u ? (u.Code, u.Severity) : default);
        Assert.Contains("blurred", Flag(new(true, "a Ghana Card", false, AiConfidence.Medium, ["blurred"])).Message);
        Assert.Equal("ai-wrong-type", Flag(new(false, "a passport", true, AiConfidence.High, [])).Code);
        // Files that can't be checked (a Word file, over 5 MB) are left out.
        Assert.Empty(DocumentAiCheckService.Checkable([new() { Id = 1, OriginalFileName = "a.docx", SizeBytes = 10 }, new() { Id = 2, OriginalFileName = "b.png", SizeBytes = 6_000_000 }]));
    }
}
