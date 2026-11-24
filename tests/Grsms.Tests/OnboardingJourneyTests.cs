using Grsms.Application.Onboarding;
using Grsms.Application.Process;
using Grsms.Domain.Hiring;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// G3 (guided workflow): the onboarding journey follows the real onboarding: the letter, the personal information and the documents
/// (x of y) run side by side; verification, activation, IT set-up and the department induction follow; only the new employee gets
/// their own journey, and the staff page gets the same stages from the details it already has.
/// </summary>
public class OnboardingJourneyTests : OnboardingTestBase
{
    private async Task<OnboardingJourneyDto?> MineAsync(int app)
    {
        using var scope = _db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<OnboardingJourneyService>().GetMineAsync(app);
    }

    private static JourneyStage Stage(OnboardingJourneyDto j, int n) => j.Stages.Single(s => s.Number == n);

    [Fact]
    public async Task The_journey_moves_from_the_letter_and_documents_to_verification_activation_and_induction()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);

        As(_candidate, Roles.Candidate);
        var j = (await MineAsync(app))!;
        Assert.True(j.ForEmployee);
        Assert.Equal(9, j.Stages.Count);
        Assert.Equal(StageState.Done, Stage(j, 1).State);
        Assert.Equal(("Being prepared", "Recruitment Officer"), (Stage(j, 2).Status, Stage(j, 2).Owner));
        Assert.Equal(StageState.Current, Stage(j, 3).State);
        Assert.Matches(@"^0 of \d+ uploaded$", Stage(j, 4).Status);
        Assert.Contains("Medical report: not uploaded yet", Stage(j, 4).Detail);
        Assert.Equal(StageState.Upcoming, Stage(j, 6).State);
        Assert.Equal("Personal information", j.Next!.Title); // the new employee's own first pending step
        Assert.Equal(11, j.Percent); // 1 of 9

        // Someone else can't see it.
        As(_candidate2, Roles.Candidate);
        Assert.Null(await MineAsync(app));

        // Letter issued and signed, personal record and documents in: waiting for the Recruitment Officer.
        await IssueAndSignAsync(id, app);
        await FillPersonalRecordAsync(app);
        await FillDocumentsAsync(id);
        As(_candidate, Roles.Candidate);
        j = (await MineAsync(app))!;
        Assert.All(new[] { 2, 3, 4 }, n => Assert.Equal(StageState.Done, Stage(j, n).State));
        Assert.Equal(("Waiting for verification", StageState.Current), (Stage(j, 5).Status, Stage(j, 5).State));
        Assert.Equal("Recruitment Officer verification", j.Next!.Title);

        // Verified: activation is next; then activated: IT set-up and department induction are live with their tasks.
        await VerifyAllAsync(id);
        As(_candidate, Roles.Candidate);
        Assert.Equal(StageState.Current, Stage((await MineAsync(app))!, 6).State);

        AsRecruiter();
        Assert.True((await Run(s => s.ActivateAsync(id, "GREL-9001"))).Succeeded);
        As(_candidate, Roles.Candidate);
        j = (await MineAsync(app))!;
        Assert.Equal(StageState.Done, Stage(j, 6).State);
        Assert.NotEmpty(Stage(j, 7).Tasks);
        Assert.Contains("Create the email account and system access", Stage(j, 7).Tasks.Select(t => t.Title));
        Assert.NotEqual(StageState.Upcoming, Stage(j, 8).State);

        // The staff page builds the same stages from its details, in staff wording.
        AsRecruiter();
        var details = (await Run(s => s.GetAsync(id)))!;
        var staff = OnboardingJourneyService.ForStaff(details);
        Assert.False(staff.ForEmployee);
        Assert.Equal(j.Stages.Select(s => s.State), staff.Stages.Select(s => s.State));
        Assert.Equal("The new employee", Stage(staff, 1).Owner);
    }
}
