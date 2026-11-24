using Grsms.Application.Organization;

namespace Grsms.Application.Reports;

/// <summary>The reports on the Reports page, in menu order.</summary>
public static class ReportKeys
{
    public const string Overview = "overview", Requests = "requests", Vacancies = "vacancies", Applications = "applications",
        Sources = "sources", Offers = "offers", TimeToHire = "timetohire", Onboarding = "onboarding",
        // Phase 21: onboarding and probation reports (see Application/Workforce/WorkforceReportService.cs)
        MissingDocuments = "missingdocs", DocumentVerification = "docverification", DocumentChecks = "docchecks", Beneficiaries = "beneficiaries",
        Probation = "probation", ProbationExtensions = "extensions", Confirmations = "confirmations",
        // docs/35 R1 (see WorkflowTargetsReportService.cs)
        WorkflowTargets = "targets";

    public static readonly IReadOnlyList<(string Key, string Title, string Description)> All =
    [
        (Overview, "Overview", "Headline figures, the recruitment funnel, divisions compared and the monthly trend."),
        (Requests, "Recruitment requests", "Every request with its status, positions, estimated cost and time to approve."),
        (Vacancies, "Vacancies", "Positions, applications at each stage and positions still open, per vacancy."),
        (Applications, "Applications", "Every application with its source, score and how far it got."),
        (Sources, "Application sources", "Which channels bring applications, shortlists and hires."),
        (Offers, "Offers", "Offers sent, accepted, declined and expired, with salaries and reply times."),
        (TimeToHire, "Time to hire", "Days from request to approval, publication and accepted offer, per hire."),
        (WorkflowTargets, "Workflow targets", "Working days each approval, offer answer and document check took, against the targets in System settings: on time, late or still waiting."),
        (Onboarding, "Onboarding status", "Each new employee's onboarding stage, letter, checklist and overdue items."),
        (MissingDocuments, "Missing documents", "New employees whose new-hire documents are still missing or rejected (HR only: personal data)."),
        (DocumentVerification, "Document verification", "Every new-hire document submitted, with its verification (HR only: personal data)."),
        (DocumentChecks, "Document checks", "Automatic remarks on new-hire documents in progress: expired or expiring IDs, the same file used twice, low-resolution scans (HR only: personal data)."),
        (Beneficiaries, "Beneficiaries", "Each employee's declared beneficiaries and shares (HR only: personal data)."),
        (Probation, "Probation", "Every probation with its stage, assessments and what is due next."),
        (ProbationExtensions, "Probation extensions", "Extended probations: original and new end dates, reasons and improvement plans."),
        (Confirmations, "Confirmations", "Employment confirmations with their letters and who approved them.")
    ];
}

/// <summary>
/// The filters every report shares (GREL requirement): division, department, vacancy, recruitment status and period.
/// <see cref="Status"/> is the name of a status of the report's own records (e.g. "Approved" for requests).
/// The period applies to each report's main date (shown on the page).
/// </summary>
public class ReportFilter
{
    public string Report { get; set; } = ReportKeys.Overview;
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }
    public int? VacancyId { get; set; }
    public string? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>A link from a report row to the record (controller, action, id).</summary>
public record ReportLink(string Controller, string Action, int Id);

/// <param name="Numeric">Right-aligned on screen.</param>
public record ReportColumn(string Header, bool Numeric = false);

/// <summary>One row. Cells hold typed values (text, numbers, dates, null), formatted by the page or the CSV export.</summary>
public record ReportRow(IReadOnlyList<object?> Cells, ReportLink? Link = null);

public record ReportTable(IReadOnlyList<ReportColumn> Columns, IReadOnlyList<ReportRow> Rows, IReadOnlyList<object?>? Totals = null);

/// <summary>A headline figure above a report. <see cref="Hint"/> explains how it is worked out.</summary>
public record ReportKpi(string Label, string Value, string? Hint = null);

/// <summary>One bar of a simple, one-colour bar chart.</summary>
public record ChartBar(string Label, decimal Value, string? Note = null);

public record ReportChart(string Title, string Unit, IReadOnlyList<ChartBar> Bars);

public record ReportResult
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>What the period filter applies to, e.g. "date requested".</summary>
    public string PeriodAppliesTo { get; init; } = "";

    public IReadOnlyList<ReportKpi> Kpis { get; init; } = [];
    public IReadOnlyList<ReportChart> Charts { get; init; } = [];
    public ReportTable Table { get; init; } = new([], []);

    /// <summary>The statuses this report can be filtered by (empty: no status filter).</summary>
    public IReadOnlyList<LookupItem> StatusOptions { get; init; } = [];

    /// <summary>The vacancies the user may see, for the vacancy filter.</summary>
    public IReadOnlyList<LookupItem> VacancyOptions { get; init; } = [];

    /// <summary>True when the user sees only the departments they head.</summary>
    public bool LimitedToMyDepartments { get; init; }
}
