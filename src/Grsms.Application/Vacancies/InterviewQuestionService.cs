using Grsms.Application.Ai;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Ai;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Vacancies;

/// <summary>One row of the interview guide form.</summary>
public class InterviewQuestionRow
{
    public InterviewQuestionCategory Category { get; set; }
    public string? Question { get; set; }
    public string? Guidance { get; set; }
}

public sealed record InterviewGuideDto(int VacancyId, string VacancyNumber, string JobTitle, IReadOnlyList<InterviewQuestionRow> Questions, bool CanEdit,
    bool AiOn, bool IsAiDraft = false, bool AiFromCache = false);

/// <summary>What the AI returns: questions with guidance, by category.</summary>
public sealed record GeneratedQuestion(InterviewQuestionCategory Category, string Question, string Guidance);
public sealed record GeneratedQuestionSet(List<GeneratedQuestion> Questions);

/// <summary>
/// 13 November 2026 (docs/36 step AI-2): a vacancy's interview guide. The Recruitment Officer or HR Manager writes it, or drafts it
/// with AI from the vacancy's description and requirements (no personal data), edits it and saves it. Anyone who can see the vacancy
/// can read it, and panel members see it on the interview page. Saving replaces the whole set.
/// </summary>
public class InterviewQuestionService(
    IVacancyRepository vacancies,
    IRepository<VacancyInterviewQuestion> questions,
    IRepository<Domain.Organization.Department> departments,
    IUnitOfWork unitOfWork,
    AccessScope scope,
    AiGateway ai)
{
    public const string PromptVersion = "questions-1";

    private bool CanEdit => VacancyWorkflow.ManagerRoles.Any(scope.IsInRole);

    private async Task<Vacancy?> VisibleAsync(int vacancyId, CancellationToken ct)
    {
        var v = await vacancies.GetWithDetailsAsync(vacancyId, ct);
        return v is not null && AccessScope.CanSee(await scope.VisibilityAsync(ct), v.DepartmentId, v.RecruiterUserId) ? v : null;
    }

    public async Task<InterviewGuideDto?> GetAsync(int vacancyId, CancellationToken ct = default)
    {
        var v = await VisibleAsync(vacancyId, ct);
        if (v is null) return null;
        return new InterviewGuideDto(v.Id, v.VacancyNumber, v.JobTitle, await RowsAsync(v.Id, ct), CanEdit, CanEdit && ai.IsOn(AiFeature.InterviewQuestions));
    }

    /// <summary>The guide for the interview page (no visibility check: the interview page has done it).</summary>
    public Task<IReadOnlyList<InterviewQuestionRow>> ForInterviewAsync(int vacancyId, CancellationToken ct = default) => RowsAsync(vacancyId, ct);

    private async Task<IReadOnlyList<InterviewQuestionRow>> RowsAsync(int vacancyId, CancellationToken ct) =>
        (await questions.ListAsync(q => q.VacancyId == vacancyId, ct)).OrderBy(q => q.Category).ThenBy(q => q.SortOrder).ThenBy(q => q.Id)
        .Select(q => new InterviewQuestionRow { Category = q.Category, Question = q.Question, Guidance = q.Guidance }).ToList();

    public async Task<Result> SaveAsync(int vacancyId, IEnumerable<InterviewQuestionRow> rows, CancellationToken ct = default)
    {
        var v = await VisibleAsync(vacancyId, ct);
        if (v is null) return Result.Failure("Vacancy not found.");
        if (!CanEdit) return Result.Failure("Only a Recruitment Officer or HR Manager can change the interview questions.");
        var list = rows.Where(r => !string.IsNullOrWhiteSpace(r.Question))
            .Select(r => new InterviewQuestionRow { Category = r.Category, Question = r.Question!.Trim(), Guidance = string.IsNullOrWhiteSpace(r.Guidance) ? null : r.Guidance.Trim() })
            .ToList();
        if (list.Count > VacancyInterviewQuestion.MaxQuestions) return Result.Failure($"At most {VacancyInterviewQuestion.MaxQuestions} questions.");
        if (list.Any(r => r.Question!.Length > VacancyInterviewQuestion.MaxQuestionLength || r.Guidance?.Length > VacancyInterviewQuestion.MaxGuidanceLength))
            return Result.Failure("Each question and its guidance must be 1,000 characters or fewer.");
        if (list.Any(r => !Enum.IsDefined(r.Category))) return Result.Failure("Choose a category for each question.");

        foreach (var old in await questions.ListAsync(q => q.VacancyId == v.Id, ct)) questions.Remove(old);
        var order = 0;
        foreach (var r in list)
            questions.Add(new VacancyInterviewQuestion { VacancyId = v.Id, Category = r.Category, Question = r.Question!, Guidance = r.Guidance, SortOrder = order++ });
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private const string Instructions = AiPrompts.Company + AiPrompts.DataOnly + AiPrompts.FairHiring +
        "Write an interview guide for the vacancy described: 4 technical, 3 behavioural, 3 situational, 3 competency and 2 practical " +
        "assessment questions (category: Technical, Behavioural, Situational, Competency or Practical). Questions test the job's real duties " +
        "and stated requirements at the right level, are open-ended and can be asked of every candidate in the same way. Behavioural " +
        "questions ask for a past example (STAR). Practical questions describe a short task or exercise the panel can set. Never ask " +
        "about age, family, pregnancy, religion, ethnicity, health or politics. guidance: one or two sentences on what a strong answer shows.";

    /// <summary>The AI draft, as rows for the form (not saved).</summary>
    public async Task<(InterviewGuideDto? Guide, string? Problem)> GenerateAsync(int vacancyId, bool regenerate, CancellationToken ct = default)
    {
        var v = await VisibleAsync(vacancyId, ct);
        if (v is null) return (null, "Vacancy not found.");
        if (!CanEdit) return (null, "Only a Recruitment Officer or HR Manager can change the interview questions.");
        var input = AiPrompts.Details(("Job title", v.JobTitle), ("Department", (await departments.GetByIdAsync(v.DepartmentId, ct))?.Name),
                        ("Employment type", EnumText.Label(v.EmploymentType)), ("Job description", v.JobDescription), ("Requirements", AdvertWriter.RequirementsText(v)))
                    + (regenerate ? $"\n(Another version, request {Guid.NewGuid():N})" : "");
        var r = await ai.RunAsync<GeneratedQuestionSet>(AiFeature.InterviewQuestions, PromptVersion, Instructions, input, ("Vacancy", v.Id),
            useCache: !regenerate, maxOutputTokens: 6000, ct: ct);
        if (!r.Succeeded) return (null, r.Problem);
        var rows = r.Value!.Questions.Where(q => !string.IsNullOrWhiteSpace(q.Question)).Take(VacancyInterviewQuestion.MaxQuestions)
            .OrderBy(q => q.Category)
            .Select(q => new InterviewQuestionRow { Category = q.Category, Question = Clip(q.Question), Guidance = Clip(q.Guidance) }).ToList();
        return (new InterviewGuideDto(v.Id, v.VacancyNumber, v.JobTitle, rows, true, true, IsAiDraft: true, AiFromCache: r.FromCache), null);
    }

    private static string Clip(string s) => s.Trim().Length > 1000 ? s.Trim()[..1000] : s.Trim();
}
