using Grsms.Application.Ai;

namespace Grsms.Web.Helpers;

/// <summary>docs/36 AI-5: what Views/Shared/_AiInsightCard.cshtml shows for a stored AI summary (interview or CV).</summary>
public sealed record AiInsightCard(
    string Title,
    string Note,
    string GenerateController,
    string GenerateAction,
    int RecordId,
    bool CanGenerate,
    string? Unavailable,
    DateTime? CreatedAt,
    string? CreatedBy,
    string? Verdict,
    string? VerdictTone,
    IReadOnlyList<(string Title, IReadOnlyList<string> Lines)> Sections)
{
    private static IReadOnlyList<(string, IReadOnlyList<string>)> Lists(params (string Title, IReadOnlyList<string> Lines)[] s) =>
        s.Where(x => x.Lines.Count > 0).Select(x => (x.Title, x.Lines)).ToList();

    public static AiInsightCard? Interview(InsightPanel<InterviewSummary>? p, int applicationId)
    {
        if (p is null || (!p.FeatureOn && p.Latest is null)) return null;
        var s = p.Latest?.Value;
        return new AiInsightCard("AI interview summary",
            "Advisory, from the validated scorecards only. The Recruitment Officer decides; the candidate never sees this.",
            "Hiring", "GenerateInterviewSummary", applicationId, p.CanGenerate, p.Unavailable, p.Latest?.CreatedAt, p.Latest?.CreatedBy,
            s is null ? null : $"{s.Recommendation switch { SummaryRecommendation.Hire => "Hire", SummaryRecommendation.HireWithReservations => "Hire with reservations", SummaryRecommendation.DoNotHire => "Do not hire", _ => "Needs more evidence" }}: {s.RecommendationReason}",
            s?.Recommendation switch { SummaryRecommendation.Hire => "success", SummaryRecommendation.DoNotHire => "danger", null => null, _ => "warning" },
            s is null ? [] : [("Overview", [s.Overview]), .. Lists(("Strengths", s.Strengths), ("Weaknesses", s.Weaknesses), ("Check before an offer", s.Risks))]);
    }

    public static AiInsightCard? Cv(InsightPanel<CvMatchSummary>? p, int applicationId)
    {
        if (p is null || (!p.FeatureOn && p.Latest is null)) return null;
        var s = p.Latest?.Value;
        return new AiInsightCard("AI CV summary",
            "Advisory, from the CV and the screening results. It does not change the ATS score; the candidate never sees this.",
            "Applications", "GenerateCvSummary", applicationId, p.CanGenerate, p.Unavailable, p.Latest?.CreatedAt, p.Latest?.CreatedBy,
            s is null ? null : $"{s.Fit} fit",
            s?.Fit switch { CvFit.Strong => "success", CvFit.Weak => "danger", null => null, _ => "warning" },
            s is null ? [] : [("Summary", [s.Summary]), .. Lists(("Strengths", s.Strengths), ("Weaknesses", s.Weaknesses),
                ("Not shown in the CV", s.MissingRequirements), ("Soft skills", s.SoftSkills))]);
    }
}
