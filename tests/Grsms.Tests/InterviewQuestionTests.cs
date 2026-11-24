using Grsms.Application.Ai;
using Grsms.Application.Settings;
using Grsms.Application.Vacancies;
using Grsms.Domain.Ai;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-2: a vacancy's interview guide. The RO or HR Manager drafts it with AI (not saved), edits and saves it (replacing
/// the set); others who can see the vacancy only read it; the AI prompt sends the vacancy only; limits are checked.
/// </summary>
public class InterviewQuestionTests : IAsyncLifetime
{
    private const int Apm = 31;
    private readonly TestDatabase _db = new();
    private string _ro = "", _hod = "";
    private int _vacancyId;

    public async Task InitializeAsync()
    {
        _ro = await _db.CreateUserAsync("q.ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hod = await _db.CreateUserAsync("q.hod@grel.test", Apm, Roles.HeadOfDepartment);
        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
        var request = new RecruitmentRequest
        {
            RequestNumber = "RR-Q-1", DivisionId = 2, DepartmentId = Apm, SiteId = 3, PositionTitle = "Process Engineer", NumberOfPositions = 1,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "Q",
            EstimatedAnnualCost = 1, RequestedByUserId = _hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
        };
        db.RecruitmentRequests.Add(request);
        await db.SaveChangesAsync();
        var v = new Vacancy
        {
            VacancyNumber = "VAC-Q-1", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm, SiteId = 3,
            RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
            VacancyType = VacancyType.External, JobDescription = "Runs the rubber processing line.", MinimumEducation = EducationLevel.Bachelors,
            ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow
        };
        db.Vacancies.Add(v);
        await db.SaveChangesAsync();
        _vacancyId = v.Id;
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<T> Run<T>(Func<InterviewQuestionService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<InterviewQuestionService>());
    }

    [Fact]
    public async Task Recruiter_drafts_with_ai_edits_and_saves_and_the_hod_can_only_read()
    {
        _db.CurrentUser.SignInAs(_ro, "q.ro@grel.test", Roles.RecruitmentOfficer);
        var guide = (await Run(s => s.GetAsync(_vacancyId)))!;
        Assert.True(guide.CanEdit);
        Assert.False(guide.AiOn); // AI switched off
        Assert.Contains("switched off", (await Run(s => s.GenerateAsync(_vacancyId, false))).Problem);

        using (var scope = _db.NewScope())
            await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?>
            {
                [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiApiKey] = "k", [SettingCatalogue.AiFeatureKey(AiFeature.InterviewQuestions)] = "true"
            });
        _db.Ai.Reply = _ => new AiReply("""
            {"questions":[{"category":"Behavioural","question":"Tell us about a line stoppage you resolved.","guidance":"Calm diagnosis."},
                          {"category":"Technical","question":"How do you control coagulation?","guidance":"Knows acid dosing."}]}
            """, 300, 200, "m");
        var (draft, problem) = await Run(s => s.GenerateAsync(_vacancyId, false));
        Assert.Null(problem);
        Assert.True(draft!.IsAiDraft);
        Assert.Equal([InterviewQuestionCategory.Technical, InterviewQuestionCategory.Behavioural], draft.Questions.Select(q => q.Category)); // sorted by category
        Assert.Contains("Runs the rubber processing line.", _db.Ai.Calls[^1].Call.Input);
        Assert.Contains("Never ask about age, family", _db.Ai.Calls[^1].Call.Instructions);
        Assert.Empty((await Run(s => s.GetAsync(_vacancyId)))!.Questions); // a draft is not saved

        var rows = draft.Questions.Append(new InterviewQuestionRow { Category = InterviewQuestionCategory.Practical, Question = "  Inspect this sample sheet.  " })
            .Append(new InterviewQuestionRow { Question = "  " }); // empty rows are ignored
        Assert.True((await Run(s => s.SaveAsync(_vacancyId, rows))).Succeeded);
        var saved = (await Run(s => s.ForInterviewAsync(_vacancyId)));
        Assert.Equal(3, saved.Count);
        Assert.Equal("Inspect this sample sheet.", saved[^1].Question);

        // Saving again replaces the set.
        Assert.True((await Run(s => s.SaveAsync(_vacancyId, [new InterviewQuestionRow { Category = InterviewQuestionCategory.Competency, Question = "Q" }]))).Succeeded);
        Assert.Single(await Run(s => s.ForInterviewAsync(_vacancyId)));
        Assert.False((await Run(s => s.SaveAsync(_vacancyId, Enumerable.Range(0, 41).Select(i => new InterviewQuestionRow { Question = $"Q{i}" })))).Succeeded);
        Assert.False((await Run(s => s.SaveAsync(_vacancyId, [new InterviewQuestionRow { Category = (InterviewQuestionCategory)99, Question = "Q" }]))).Succeeded);

        // The department's HOD reads it but can't change or generate it.
        _db.CurrentUser.SignInAs(_hod, "q.hod@grel.test", Roles.HeadOfDepartment);
        var hodView = (await Run(s => s.GetAsync(_vacancyId)))!;
        Assert.False(hodView.CanEdit);
        Assert.Single(hodView.Questions);
        Assert.False((await Run(s => s.SaveAsync(_vacancyId, []))).Succeeded);
        Assert.NotNull((await Run(s => s.GenerateAsync(_vacancyId, false))).Problem);
    }
}
