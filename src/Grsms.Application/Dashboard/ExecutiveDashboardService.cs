using System.Globalization;
using System.Text;
using Grsms.Application.Ai;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Reports;
using Grsms.Domain.Ai;
using Grsms.Domain.Applications;
using Grsms.Domain.Employees;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Probation;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Dashboard;

/// <summary>One figure on the executive dashboard. <see cref="Previous"/> is the same point in the previous quarter (null = no trend).</summary>
/// <param name="HigherIsBetter">For the arrow's colour: true (green when up), false (green when down), null (neutral).</param>
public sealed record ExecutiveKpi(string Key, string Label, decimal Value, decimal? Previous, string Unit, bool? HigherIsBetter, string Hint,
    string? Controller = null, string? Action = null, string? RouteQuery = null)
{
    public decimal? ChangePercent => Previous is { } p && p != 0 ? Math.Round((Value - p) / p * 100m, 0) : null;
}

public sealed record DepartmentFigure(string Department, int Headcount, int OpenPositions)
{
    /// <summary>Open positions as a share of (headcount + open positions).</summary>
    public decimal VacancyRate => Headcount + OpenPositions == 0 ? 0 : Math.Round(OpenPositions * 100m / (Headcount + OpenPositions), 1);
}

public sealed record ExecutiveDashboardDto(
    string QuarterLabel,
    string ComparedWith,
    DateOnly Today,
    IReadOnlyList<ExecutiveKpi> Recruitment,
    IReadOnlyList<ExecutiveKpi> Onboarding,
    IReadOnlyList<ExecutiveKpi> Workforce,
    IReadOnlyList<DepartmentFigure> Departments,
    bool AiOn);

/// <summary>The AI's written summary: every sentence built only from the figures sent.</summary>
public sealed record ExecutiveSummary(string Headline, List<string> Highlights, List<string> Risks, List<string> Actions);

/// <summary>
/// 13 November 2026 (docs/36 step AI-3): the executive dashboard for the MD and HR Managers ("executive.view"): recruitment, onboarding
/// and workforce in one page, every count compared with the same point of the previous quarter, departments by headcount and open
/// positions, and a written summary. Figures are worked out from the live records each time (as the reports are). The summary is
/// written by AI from the totals only (no names) and every number in it comes from GRSMS; it is cached until the figures change.
/// </summary>
public class ExecutiveDashboardService(
    IRepository<Vacancy> vacancies,
    IRepository<JobApplication> applications,
    IRepository<Offer> offers,
    IRepository<RecruitmentRequest> requests,
    IRepository<OnboardingRecord> onboardings,
    IRepository<ProbationRecord> probations,
    IRepository<Employee> employees,
    IRepository<Department> departments,
    WorkflowTargetsReportService targets,
    AiGateway ai)
{
    public const string PromptVersion = "exec-1";
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    public static (DateOnly Start, DateOnly PrevStart, DateOnly PrevSamePoint) Quarter(DateOnly today)
    {
        var start = new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
        var prevStart = start.AddMonths(-3);
        var elapsed = today.DayNumber - start.DayNumber;
        return (start, prevStart, DateOnly.FromDayNumber(Math.Min(prevStart.DayNumber + elapsed, start.DayNumber - 1)));
    }

    public async Task<ExecutiveDashboardDto> GetAsync(DateOnly today, CancellationToken ct = default)
    {
        var (qStart, pStart, pSame) = Quarter(today);
        bool InQ(DateTime? t) => t is { } x && DateOnly.FromDateTime(x) >= qStart && DateOnly.FromDateTime(x) <= today;
        bool InP(DateTime? t) => t is { } x && DateOnly.FromDateTime(x) >= pStart && DateOnly.FromDateTime(x) <= pSame;

        // ---- Recruitment
        var allOffers = await offers.ListAsync(o => o.Status != OfferStatus.Draft, ct);
        var accepted = allOffers.Where(o => o.Status == OfferStatus.Accepted).ToList();
        var published = await vacancies.ListAsync(v => v.Status == VacancyStatus.Published || v.Status == VacancyStatus.Closed, ct);
        int OpenPositions(Vacancy v) => Math.Max(0, v.NumberOfPositions - accepted.Count(o => o.VacancyId == v.Id));
        var openVacancies = published.Where(v => v.Status == VacancyStatus.Published).ToList();
        var stillOpen = published.Where(v => OpenPositions(v) > 0).ToList(); // published or in selection, positions not yet filled

        var since = pStart.ToDateTime(TimeOnly.MinValue);
        var apps = await applications.ListAsync(a => a.SubmittedAt >= since, ct);
        var requestById = (await requests.ListAsync(r => r.DateRequested != null, ct)).ToDictionary(r => r.Id);
        var vacancyRequest = (await vacancies.ListAsync(ct: ct)).ToDictionary(v => v.Id, v => v.RecruitmentRequestId);
        decimal? AvgTimeToHire(Func<DateTime?, bool> inPeriod)
        {
            var days = accepted.Where(o => inPeriod(o.RespondedAt))
                .Select(o => vacancyRequest.TryGetValue(o.VacancyId, out var rid) && requestById.TryGetValue(rid, out var r) && o.RespondedAt is { } at
                    ? (decimal?)(at - r.DateRequested!.Value).TotalDays : null)
                .OfType<decimal>().ToList();
            return days.Count == 0 ? null : Math.Round(days.Average(), 0);
        }
        decimal? Acceptance(Func<DateTime?, bool> inPeriod)
        {
            var answered = allOffers.Where(o => o.Status is OfferStatus.Accepted or OfferStatus.Declined or OfferStatus.Expired && inPeriod(o.RespondedAt ?? o.SentAt)).ToList();
            return answered.Count == 0 ? null : Math.Round(answered.Count(o => o.Status == OfferStatus.Accepted) * 100m / answered.Count, 0);
        }
        var now = DateTime.UtcNow;
        var steps = (await targets.CollectAsync(new ReportFilter(), ct)).Where(w => w.Finished is not null).ToList();
        decimal? OnTime(Func<DateTime?, bool> inPeriod)
        {
            var done = steps.Where(w => inPeriod(w.Started)).ToList();
            return done.Count == 0 ? null : Math.Round(done.Count(w => WorkflowTargetsReportService.ResultOf(w, now) == WorkflowTargetsReportService.OnTime) * 100m / done.Count, 0);
        }

        var recruitment = new List<ExecutiveKpi>
        {
            new("open-vacancies", "Open vacancies", openVacancies.Count, null, "", null, "Published and accepting applications now.", "Vacancies", "Index"),
            new("open-positions", "Positions still to fill", stillOpen.Sum(OpenPositions), null, "", false,
                "Positions of published or closed vacancies without an accepted offer yet."),
            new("applications", "Applications received", apps.Count(a => InQ(a.SubmittedAt)), apps.Count(a => InP(a.SubmittedAt)), "", true,
                "This quarter so far.", "Applications", "Index", "tab=all"),
            new("hires", "Offers accepted", accepted.Count(o => InQ(o.RespondedAt)), accepted.Count(o => InP(o.RespondedAt)), "", true, "This quarter so far."),
            new("time-to-hire", "Average time to hire", AvgTimeToHire(InQ) ?? 0, AvgTimeToHire(InP), "days", false,
                "Days from the recruitment request to the accepted offer, for offers accepted this quarter."),
            new("acceptance", "Offer acceptance", Acceptance(InQ) ?? 0, Acceptance(InP), "%", true, "Accepted out of accepted, declined and expired offers."),
            new("on-time", "Approval steps on time", OnTime(InQ) ?? 0, OnTime(InP), "%", true,
                "Finished workflow steps that met their target (Workflow targets report).", "Reports", "Index", "report=targets")
        };

        // ---- Onboarding and probation
        var obs = (await onboardings.ListAsync(o => o.Status != OnboardingStatus.Completed && o.Status != OnboardingStatus.Cancelled, ct)).ToList();
        var overdueDocs = obs.Count(o => o.DocumentsDueDate is { } due && due < today &&
                                         o.Status is OnboardingStatus.Draft or OnboardingStatus.AwaitingEmployeeSubmission or OnboardingStatus.AwaitingDocumentVerification);
        var probs = (await probations.ListAsync(p => p.Status != ProbationStatus.Confirmed && p.Status != ProbationStatus.NotConfirmed, ct)).ToList();
        var onboarding = new List<ExecutiveKpi>
        {
            new("onboarding", "Onboardings in progress", obs.Count, null, "", null, "New employees not yet activated or still in induction.", "Onboarding", "Index"),
            new("docs-overdue", "Onboardings with documents overdue", overdueDocs, null, "", false, "Past the document deadline and not all verified."),
            new("starting", "Starting in the next 30 days", obs.Count(o => o.StartDate >= today && o.StartDate <= today.AddDays(30)), null, "", null, "First days coming up."),
            new("probation", "Probations in progress", probs.Count, null, "", null, "Employees on probation.", "Probation", "Index"),
            new("probation-ending", "Probations ending in 30 days", probs.Count(p => p.EndDate >= today && p.EndDate <= today.AddDays(30)), null, "", null,
                "A confirmation decision is due."),
            new("probation-overdue", "Probations past their end date", probs.Count(p => p.EndDate < today), null, "", false, "Waiting for the assessment or HR's decision.")
        };

        // ---- Workforce
        var staff = (await employees.ListAsync(e => e.Status != EmployeeStatus.Separated, ct)).ToList();
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        var workforce = new List<ExecutiveKpi>
        {
            new("headcount", "Headcount", staff.Count, null, "", null, "Employees on GRSMS who have not left.", "Employees", "Index"),
            new("new-hires", "Joined this quarter", staff.Count(e => e.HireDate >= qStart && e.HireDate <= today), staff.Count(e => e.HireDate >= pStart && e.HireDate <= pSame), "", null,
                "By hire date."),
            new("on-probation", "On probation", staff.Count(e => e.Status == EmployeeStatus.OnProbation), null, "", null, "Employee status."),
            new("contracts", "Contracts ending in 90 days", staff.Count(e => e.ContractEndDate is { } end && end >= today && end <= today.AddDays(90)), null, "", false,
                "Fixed-term contracts to renew or end.")
        };

        var deptIds = staff.Select(e => e.DepartmentId).Concat(stillOpen.Select(v => v.DepartmentId)).Distinct();
        var figures = deptIds.Select(id => new DepartmentFigure(deptNames.GetValueOrDefault(id) ?? $"Department {id}",
                staff.Count(e => e.DepartmentId == id), stillOpen.Where(v => v.DepartmentId == id).Sum(OpenPositions)))
            .OrderByDescending(f => f.Headcount).ThenByDescending(f => f.OpenPositions).ToList();

        var q = (qStart.Month - 1) / 3 + 1;
        return new ExecutiveDashboardDto($"Q{q} {qStart.Year}", $"the same point of Q{(pStart.Month - 1) / 3 + 1} {pStart.Year} ({pStart:d MMM}–{pSame:d MMM})",
            today, recruitment, onboarding, workforce, figures, ai.IsOn(AiFeature.ExecutiveSummary));
    }

    private const string Instructions = AiPrompts.Company + AiPrompts.DataOnly +
        "Write a short executive summary of GREL's recruitment, onboarding and workforce for the Managing Director, from the figures " +
        "given. Use ONLY these figures: never invent, estimate or extrapolate a number, cause or name, and quote numbers exactly as given " +
        "(you may say \"up 22 %\" only when the change is given). If a figure has no comparison, do not describe a trend. headline: one sentence " +
        "with the most important point. highlights: 3 to 5 sentences on what is going well or changed most. risks: up to 4 sentences on " +
        "what needs attention (overdue items, low acceptance, departments with the highest vacancy rate, slow steps). actions: up to 4 short, " +
        "practical next steps for HR. Plain sentences, no Markdown.";

    /// <summary>The figures as text for the AI (totals only).</summary>
    public static string Facts(ExecutiveDashboardDto d)
    {
        var sb = new StringBuilder("<details>\n");
        sb.Append($"Period: {d.QuarterLabel} to {d.Today:d MMMM yyyy}; comparisons are with {d.ComparedWith}.\n");
        void Group(string title, IEnumerable<ExecutiveKpi> kpis)
        {
            sb.Append(title).Append(":\n");
            foreach (var k in kpis)
            {
                sb.Append($"- {k.Label}: {k.Value.ToString("0.#", Gb)}{(k.Unit == "" ? "" : " " + k.Unit)}");
                if (k.Previous is { } p) sb.Append($" (previous: {p.ToString("0.#", Gb)}{(k.ChangePercent is { } c ? $", change {(c > 0 ? "+" : "")}{c} %" : "")})");
                sb.Append('\n');
            }
        }
        Group("Recruitment", d.Recruitment);
        Group("Onboarding and probation", d.Onboarding);
        Group("Workforce", d.Workforce);
        sb.Append("Departments (headcount, positions still to fill, vacancy rate):\n");
        foreach (var f in d.Departments.Take(15))
            sb.Append($"- {f.Department}: {f.Headcount}, {f.OpenPositions}, {f.VacancyRate.ToString("0.#", Gb)} %\n");
        return sb.Append("</details>").ToString();
    }

    /// <summary>The written summary (cached until the figures change, or a new version on request).</summary>
    public async Task<AiResult<ExecutiveSummary>> SummaryAsync(DateOnly today, bool regenerate, CancellationToken ct = default)
    {
        var d = await GetAsync(today, ct);
        return await ai.RunAsync<ExecutiveSummary>(AiFeature.ExecutiveSummary, PromptVersion, Instructions,
            Facts(d) + (regenerate ? $"\n(Another version, request {Guid.NewGuid():N})" : ""), useCache: !regenerate, maxOutputTokens: 2000, ct: ct);
    }
}
