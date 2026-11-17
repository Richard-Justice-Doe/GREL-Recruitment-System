using Grsms.Application.Ai;
using Grsms.Application.Common;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// docs/36: the "Draft with AI" buttons (wwwroot/js/ai-panel.js). Each endpoint returns JSON for the panel: a preview in sections,
/// and the values to put in the page's form fields when the person clicks "Use this draft". Nothing is saved here: the person edits
/// the form and saves it the usual way, so the usual checks and approvals apply. Anti-forgery is checked (global filter); each
/// endpoint needs the same permission as the form it serves.
/// </summary>
[Authorize]
public class AiController(JobDescriptionDrafter jobDescriptions, AdvertWriter adverts, OnboardingAssistantService assistant) : Controller
{
    /// <summary>The panel's answer. <see cref="Fields"/>: form field name → value.</summary>
    public sealed record PanelResponse(bool Ok, string? Problem, bool FromCache, string? Confidence, string? ConfidenceNote,
        IReadOnlyList<PanelSection> Sections, IReadOnlyDictionary<string, string> Fields);

    public sealed record PanelSection(string Title, string Text);

    private static PanelResponse Failed(string problem) => new(false, problem, false, null, null, [], new Dictionary<string, string>());

    /// <summary>AI-1a: job description and requirements for the vacancy form.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Vacancies)]
    public async Task<IActionResult> JobDescription(string? jobTitle, int? recruitmentRequestId, string? department, string? site, string? employmentType,
        string? grade, string? level, string? responsibilities, bool regenerate, CancellationToken ct)
    {
        var r = await jobDescriptions.DraftAsync(new JobDescriptionRequest(jobTitle?.Trim() ?? "", department ?? "", site, employmentType,
            Clip(grade, 100), Clip(level, 100), responsibilities?.Trim(), recruitmentRequestId), regenerate, ct);
        if (!r.Succeeded) return Json(Failed(r.Problem!));
        var d = r.Value!;

        var given = new[] { grade, level, responsibilities }.Count(x => !string.IsNullOrWhiteSpace(x));
        var (confidence, note) = given switch
        {
            3 => ("High", "Based on the grade, level and the responsibilities you gave."),
            >= 1 => ("Medium", "Add the grade, level and main responsibilities for a closer draft."),
            _ => ("Low", "Based on the job title and department only: check every line.")
        };
        static string Lines(IEnumerable<string> l) => string.Join("\n", l.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        var fields = new Dictionary<string, string>
        {
            ["JobDescription"] = d.DescriptionText(),
            ["MinimumEducation"] = ((int)d.MinimumEducation).ToString(), // the select uses the enum number
            ["FieldOfStudy"] = d.FieldOfStudy.Trim(),
            ["MinYearsExperience"] = Math.Clamp(d.MinYearsExperience, 0, 40).ToString(),
            ["EssentialSkills"] = Lines(d.EssentialSkills),
            ["DesirableSkills"] = Lines(d.DesirableSkills),
            ["EssentialCertifications"] = Lines(d.EssentialCertifications),
            ["DesirableCertifications"] = Lines(d.DesirableCertifications)
        };
        var sections = new List<PanelSection>
        {
            new("Job description", d.DescriptionText()),
            new("Requirements", $"Minimum education: {EnumText.Label(d.MinimumEducation)}{(string.IsNullOrWhiteSpace(d.FieldOfStudy) ? "" : $" in {d.FieldOfStudy}")}\n" +
                                $"Experience: at least {d.MinYearsExperience} year(s)\n" +
                                $"Essential skills: {string.Join(", ", d.EssentialSkills)}\nDesirable skills: {string.Join(", ", d.DesirableSkills)}\n" +
                                $"Essential certifications: {(d.EssentialCertifications.Count == 0 ? "none" : string.Join(", ", d.EssentialCertifications))}\n" +
                                $"Desirable certifications: {(d.DesirableCertifications.Count == 0 ? "none" : string.Join(", ", d.DesirableCertifications))}")
        };
        return Json(new PanelResponse(true, null, r.FromCache, confidence, note, sections, fields));
    }

    /// <summary>AI-1b: four advert versions for the advert form.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Adverts)]
    public async Task<IActionResult> AdvertVersions(int vacancyId, string? title, string? benefits, bool regenerate, CancellationToken ct)
    {
        var r = await adverts.WriteForVacancyAsync(vacancyId, Clip(title, 200), benefits?.Trim(), regenerate, ct);
        if (!r.Succeeded) return Json(Failed(r.Problem!));
        var v = r.Value!;
        var fields = new Dictionary<string, string>
        {
            ["Body"] = v.CareersPortal.Trim(), ["ExternalText"] = v.External.Trim(), ["InternalNotice"] = v.Internal.Trim(),
            ["LinkedInText"] = v.LinkedIn.Trim().Length > 3000 ? v.LinkedIn.Trim()[..3000] : v.LinkedIn.Trim()
        };
        var sections = new List<PanelSection>
        {
            new("Careers portal (advert text)", fields["Body"]), new("Job boards and newspapers", fields["ExternalText"]),
            new("Internal notice for staff", fields["InternalNotice"]), new("LinkedIn post", fields["LinkedInText"])
        };
        return Json(new PanelResponse(true, null, r.FromCache, string.IsNullOrWhiteSpace(benefits) ? "Medium" : "High",
            string.IsNullOrWhiteSpace(benefits) ? "No benefits were given, so none are mentioned: add them for a more attractive advert." : "Based on the vacancy and the benefits you gave.",
            sections, fields));
    }

    /// <summary>17 Nov 2026: suggested probation objectives (with due dates inside the probation) and development plan, from the job.</summary>
    [HttpPost]
    public async Task<IActionResult> ProbationPlan(int id, [FromServices] ProbationPlanDrafter drafter, CancellationToken ct)
    {
        var r = await drafter.SuggestAsync(id, ct);
        if (!r.Succeeded) return Json(new { ok = false, problem = r.Problem });
        return Json(new
        {
            ok = true,
            objectives = r.Value!.Objectives.Select(o => new { objective = o.Objective, successIndicators = o.SuccessIndicators, dueDate = o.DueDate.ToString("yyyy-MM-dd") }),
            plan = r.Value.DevelopmentPlan.Select(d => new { trainingNeed = d.TrainingNeed, developmentNeed = d.DevelopmentNeed, timeline = d.Timeline })
        });
    }

    /// <summary>The onboarding assistant's answer (docs/36 AI-6): plain text and up to three buttons whose URLs come from GRSMS.</summary>
    public sealed record ChatResponse(bool Ok, string? Problem, string? Answer, IReadOnlyList<AssistantAction> Actions);

    private sealed record Turn(bool U, string? T);

    /// <summary>
    /// AI-6: the onboarding assistant. The new employee asks about their own onboarding (applicationId); staff ask about one they may
    /// open (onboardingId). The service applies the same visibility rules as the pages. history: the chat so far, kept by the page
    /// (JSON [{"u":true,"t":"..."}], last 6 turns), never stored.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Onboarding(int? applicationId, int? onboardingId, string? question, string? history, CancellationToken ct)
    {
        var turns = new List<AssistantTurn>();
        if (!string.IsNullOrEmpty(history) && history.Length <= 20_000)
        {
            try
            {
                turns = (System.Text.Json.JsonSerializer.Deserialize<List<Turn>>(history, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
                    .Where(t => !string.IsNullOrWhiteSpace(t.T)).Select(t => new AssistantTurn(t.U, t.T!)).ToList();
            }
            catch (System.Text.Json.JsonException) { /* a broken history is ignored, the question still gets an answer */ }
        }
        var r = applicationId is { } app ? await assistant.AskMineAsync(app, question, turns, ct)
            : onboardingId is { } id ? await assistant.AskStaffAsync(id, question, turns, ct)
            : AiResult<AssistantReply>.Fail("Onboarding not found.");
        return Json(r.Succeeded ? new ChatResponse(true, null, r.Value!.Answer, r.Value.Actions) : new ChatResponse(false, r.Problem, null, []));
    }

    private static string? Clip(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();
}
