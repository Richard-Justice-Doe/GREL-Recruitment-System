using Grsms.Application.Ai;
using Grsms.Application.Applications;
using Grsms.Domain.Ai;
using Grsms.Domain.Applications;
using Grsms.Domain.Vacancies;

namespace Grsms.Infrastructure.Ats;

/// <summary>
/// 13 November 2026 (docs/36 step AI-5): the AI CV reader through the AI gateway. When "CV reading" is switched on in
/// AI settings &amp; usage, CVs are read by the provider chosen there (Anthropic, OpenAI or Azure OpenAI), with soft skills, and every
/// call is logged and counted against the budget. Otherwise the earlier Claude reader (ATS settings + Ats:Ai:ApiKey) is used as
/// before. Either way it only reads: the scores and rejection rules stay GRSMS's published rules, and any failure falls back to the
/// built-in rule-based reader.
/// </summary>
public sealed class GatewayCvReader(AiGateway gateway, ClaudeCvReader legacy) : IAiCvReader
{
    public const string PromptVersion = "cv-1";

    private sealed record Job(string Description, string From, string To, bool IsCurrent);

    private sealed record Cv(string FullName, string Email, string Phone, EducationLevel HighestEducation, List<string> Education,
        List<Job> EmploymentHistory, List<string> Skills, List<string> SoftSkills, List<string> Certifications);

    public bool IsSwitchedOn => gateway.IsOn(AiFeature.CvReading);

    public bool IsConfigured => IsSwitchedOn || legacy.IsConfigured;

    public async Task<ParsedCv?> ReadAsync(string cvText, DateOnly today, CancellationToken ct = default)
    {
        if (!IsSwitchedOn) return await legacy.ReadAsync(cvText, today, ct);
        if (string.IsNullOrWhiteSpace(cvText)) return null;
        var r = await gateway.RunAsync<Cv>(AiFeature.CvReading, PromptVersion,
            ClaudeCvReader.Instructions + " soft_skills: personal and interpersonal skills the CV states (e.g. teamwork, leadership), as written.",
            $"<cv>\n{cvText}\n</cv>", maxOutputTokens: 8000, ct: ct);
        if (!r.Succeeded) return null;
        var cv = r.Value!;
        return ClaudeCvReader.Map(new ClaudeCvReader.AiCv(cv.FullName, cv.Email, cv.Phone, cv.HighestEducation.ToString(), cv.Education,
            cv.EmploymentHistory.Select(j => new ClaudeCvReader.AiJob(j.Description, j.From, j.To, j.IsCurrent)).ToList(), cv.Skills, cv.Certifications),
            today, cv.SoftSkills);
    }
}
