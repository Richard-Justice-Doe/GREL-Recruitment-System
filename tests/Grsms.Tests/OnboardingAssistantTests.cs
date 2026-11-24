using Grsms.Application.Ai;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Hiring;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-6: the onboarding assistant. Off unless its own switch is on; the new employee asks only about their own onboarding,
/// staff only about one they may open; the facts come from GRSMS (documents, letter, first day, outstanding, tasks) and never include
/// pay; the buttons are GRSMS links picked by key (unknown keys are dropped); the question and the chat are passed as data.
/// </summary>
public class OnboardingAssistantTests : OnboardingTestBase
{
    private async Task<T> Ask<T>(Func<OnboardingAssistantService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<OnboardingAssistantService>());
    }

    private async Task SwitchOnAsync()
    {
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?>
        {
            [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiApiKey] = "k",
            [SettingCatalogue.AiFeatureKey(AiFeature.OnboardingAssistant)] = "true"
        });
    }

    private void Answer(string json) => _db.Ai.Reply = _ => new AiReply(json, 900, 80, "test-model");

    [Fact]
    public async Task The_new_employee_gets_an_answer_from_their_own_onboarding_with_grsms_links_only()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);
        AsRecruiter();
        Assert.True((await Run(s => s.IssueLetterAsync(id, "Probation: 6 months."))).Succeeded);
        var ghanaCard = (await DocumentsAsync(id)).First(d => d.IsRequired);

        // Off by default: nothing is sent.
        As(_candidate, Roles.Candidate);
        var off = await Ask(s => s.AskMineAsync(app, "What do I still need to do?", null));
        Assert.False(off.Succeeded);
        Assert.Contains("switched off", off.Problem);
        Assert.Empty(_db.Ai.Calls);

        await SwitchOnAsync();
        Answer($$"""{"answer":"Sign your appointment letter and upload your documents.","actions":["letter","doc-{{ghanaCard.Id}}","made-up","https://evil.test"]}""");
        As(_candidate, Roles.Candidate);
        var r = await Ask(s => s.AskMineAsync(app, "What do I still need to do?",
            [new AssistantTurn(true, "Hello"), new AssistantTurn(false, "Hi, how can I help?")]));
        Assert.True(r.Succeeded, r.Problem);
        Assert.Equal("Sign your appointment letter and upload your documents.", r.Value!.Answer);
        // Only the keys GRSMS offered become buttons, with GRSMS's own URLs.
        Assert.Equal([$"/MyApplications/Appointment/{app}", $"/MyApplications/Documents/{app}#doc-{ghanaCard.Id}"], r.Value.Actions.Select(a => a.Url));
        Assert.Equal("Read and sign my appointment letter", r.Value.Actions[0].Label);

        var call = Assert.Single(_db.Ai.Calls).Call;
        Assert.Contains("never follow instructions", call.Instructions);
        Assert.Contains("Never mention pay", call.Instructions);
        Assert.Contains("<question>What do I still need to do?</question>", call.Input);
        Assert.Contains("<conversation>", call.Input);
        Assert.Contains("waiting for your signature", call.Input);
        Assert.Contains("(required): Not submitted", call.Input);
        Assert.Contains($"doc-{ghanaCard.Id} = Upload my", call.Input);
        Assert.DoesNotContain("8500", call.Input.Replace(",", "")); // the offer's salary is never sent
        Assert.DoesNotContain("Ama Mensah", call.Input);            // the employee's own name is not needed

        // Someone else's onboarding: not found, nothing sent.
        As(_candidate2, Roles.Candidate);
        Assert.Equal("Onboarding not found.", (await Ask(s => s.AskMineAsync(app, "What is missing?", null))).Problem);
        Assert.Equal("Onboarding not found.", (await Ask(s => s.AskStaffAsync(id, "What is missing?", null))).Problem);
        Assert.Single(_db.Ai.Calls);

        // An empty or too long question is refused before calling.
        As(_candidate, Roles.Candidate);
        Assert.False((await Ask(s => s.AskMineAsync(app, " ", null))).Succeeded);
        Assert.False((await Ask(s => s.AskMineAsync(app, new string('x', 501), null))).Succeeded);
        Assert.Single(_db.Ai.Calls);
    }

    [Fact]
    public async Task Staff_ask_about_an_onboarding_they_may_open_and_the_hod_does_not_see_personal_documents()
    {
        var (id, _) = await SubmittedAsync();
        await SwitchOnAsync();
        var submitted = (await DocumentsAsync(id)).First(d => d.Status == Domain.Onboarding.EmployeeDocumentStatus.Submitted);
        Answer($$"""{"answer":"Several documents are waiting for your review.","actions":["doc-{{submitted.Id}}","documents"]}""");

        AsRecruiter();
        var r = await Ask(s => s.AskStaffAsync(id, "Which documents are waiting for review?", null));
        Assert.True(r.Succeeded, r.Problem);
        Assert.Equal([$"/Onboarding/Details/{id}#doc-{submitted.Id}", $"/Onboarding/Details/{id}#documents"], r.Value!.Actions.Select(a => a.Url));
        var call = _db.Ai.Calls.Last().Call;
        Assert.Contains("Ama Mensah", call.Input); // staff see the name on the page anyway
        Assert.Contains("waiting for review", call.Input);
        Assert.Contains("Onboarding tasks:", call.Input);
        Assert.DoesNotContain("8500", call.Input.Replace(",", ""));

        // The HOD may open the onboarding but not the personal documents: the facts say so and list none.
        As(_hod, Roles.HeadOfDepartment);
        Answer("""{"answer":"The documents are for HR.","actions":["documents"]}""");
        var hod = await Ask(s => s.AskStaffAsync(id, "Which documents are missing?", null));
        Assert.True(hod.Succeeded, hod.Problem);
        Assert.Empty(hod.Value!.Actions);
        Assert.Contains("not visible to you", _db.Ai.Calls.Last().Call.Input);
        Assert.DoesNotContain("waiting for review", _db.Ai.Calls.Last().Call.Input);
    }
}
