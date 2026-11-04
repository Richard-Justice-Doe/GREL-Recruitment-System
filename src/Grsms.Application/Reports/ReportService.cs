using System.Globalization;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Reports;

/// <summary>
/// Recruitment reports and the executive overview (Module 11).
///
///   * Every report has the same filters (GREL requirement): division, department, vacancy, recruitment status, period.
///   * Visibility follows <see cref="AccessScope"/>: HR, the MD and the HR Administrator see every department; a Head of
///     Department sees only the departments they head.
///   * Figures are worked out from the live records each time (nothing is stored), so a report is never out of date.
///     This is comfortable for GREL's volumes (thousands of applications); see docs/20-reports.md.
/// </summary>
public class ReportService(
    IRepository<RecruitmentRequest> requests,
    IRepository<Vacancy> vacancies,
    IRepository<JobApplication> applications,
    IRepository<ApplicationActionEntry> applicationActions,
    IRepository<Offer> offers,
    IRepository<OnboardingRecord> onboardings,
    IRepository<OnboardingTask> onboardingTasks,
    IRepository<Department> departments,
    IRepository<Division> divisions,
    AccessScope scope)
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>The funnel stages in order. An application "reached" a stage if it was ever there.</summary>
    public static readonly IReadOnlyList<ApplicationStatus> Stages =
        [ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Hired];

    public async Task<ReportResult> RunAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var data = await LoadAsync(filter, ct);
        var result = filter.Report switch
        {
            ReportKeys.Requests => Requests(data, filter),
            ReportKeys.Vacancies => Vacancies(data, filter),
            ReportKeys.Applications => Applications(data, filter),
            ReportKeys.Sources => Sources(data, filter),
            ReportKeys.Offers => Offers(data, filter),
            ReportKeys.TimeToHire => TimeToHire(data, filter),
            ReportKeys.Onboarding => Onboarding(data, filter),
            _ => Overview(data, filter)
        };
        var info = ReportKeys.All.FirstOrDefault(r => r.Key == result.Key);
        return result with
        {
            Title = info.Title ?? "",
            Description = info.Description ?? "",
            VacancyOptions = data.AllVacancies.OrderByDescending(v => v.Id).Select(v => new LookupItem(v.Id.ToString(), $"{v.VacancyNumber} {v.JobTitle}")).ToList(),
            LimitedToMyDepartments = !data.SeeAll
        };
    }

    // ===================================================================== Loading (visibility + division / department / vacancy)

    private sealed class Data
    {
        public bool SeeAll;
        public List<RecruitmentRequest> Requests = [];
        public List<Vacancy> Vacancies = [];
        public List<Vacancy> AllVacancies = [];
        public List<JobApplication> Applications = [];
        public Dictionary<int, int> Reached = [];          // application id → index in Stages
        public List<Offer> Offers = [];
        public List<OnboardingRecord> Onboardings = [];
        public List<OnboardingTask> Tasks = [];
        public Dictionary<int, string> Departments = [];
        public Dictionary<int, string> Divisions = [];
        public Dictionary<int, Vacancy> VacancyById = [];
        public Dictionary<int, RecruitmentRequest> RequestById = [];
    }

    private async Task<Data> LoadAsync(ReportFilter f, CancellationToken ct)
    {
        var v = await scope.VisibilityAsync(ct);
        var mine = v.HodDepartmentIds.ToList();
        var all = v.SeeAll;
        int? div = f.DivisionId, dep = f.DepartmentId, vac = f.VacancyId;

        var d = new Data { SeeAll = all };
        d.AllVacancies = (await vacancies.ListAsync(x => all || mine.Contains(x.DepartmentId), ct)).ToList();
        d.Vacancies = d.AllVacancies.Where(x => (div == null || x.DivisionId == div) && (dep == null || x.DepartmentId == dep) && (vac == null || x.Id == vac)).ToList();
        d.VacancyById = d.AllVacancies.ToDictionary(x => x.Id);

        var requestOfVacancy = vac is null ? (int?)null : d.VacancyById.GetValueOrDefault(vac.Value)?.RecruitmentRequestId ?? -1;
        var allRequests = await requests.ListAsync(x => all || mine.Contains(x.DepartmentId), ct);
        d.RequestById = allRequests.ToDictionary(x => x.Id);
        d.Requests = allRequests.Where(x => x.Status != RecruitmentRequestStatus.Draft &&
                                            (div == null || x.DivisionId == div) && (dep == null || x.DepartmentId == dep) &&
                                            (requestOfVacancy == null || x.Id == requestOfVacancy)).ToList();

        d.Applications = (await applications.ListAsync(x => (all || mine.Contains(x.DepartmentId)) && (div == null || x.DivisionId == div) &&
                                                            (dep == null || x.DepartmentId == dep) && (vac == null || x.VacancyId == vac), ct)).ToList();
        var appIds = d.Applications.Select(a => a.Id).ToList();
        var history = appIds.Count == 0 ? [] : await applicationActions.ListAsync(a => appIds.Contains(a.JobApplicationId), ct);
        var byApp = history.GroupBy(h => h.JobApplicationId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var a in d.Applications)
        {
            var seen = (byApp.GetValueOrDefault(a.Id) ?? []).SelectMany(h => new[] { h.FromStatus, h.ToStatus }).Append(a.Status);
            d.Reached[a.Id] = seen.Select(StageIndex).Max();
        }

        d.Offers = (await offers.ListAsync(x => (all || mine.Contains(x.DepartmentId)) && (div == null || x.DivisionId == div) &&
                                                (dep == null || x.DepartmentId == dep) && (vac == null || x.VacancyId == vac), ct)).ToList();
        d.Onboardings = (await onboardings.ListAsync(x => (all || mine.Contains(x.DepartmentId)) && (div == null || x.DivisionId == div) &&
                                                          (dep == null || x.DepartmentId == dep) && (vac == null || x.VacancyId == vac), ct)).ToList();
        var onbIds = d.Onboardings.Select(o => o.Id).ToList();
        d.Tasks = onbIds.Count == 0 ? [] : (await onboardingTasks.ListAsync(t => onbIds.Contains(t.OnboardingRecordId), ct)).ToList();

        d.Departments = (await departments.ListAsync(ct: ct)).ToDictionary(x => x.Id, x => x.Name);
        d.Divisions = (await divisions.ListAsync(ct: ct)).ToDictionary(x => x.Id, x => x.Name);
        return d;
    }

    /// <summary>Position of a status in the funnel; "offer declined" counts as having reached the offer.</summary>
    private static int StageIndex(ApplicationStatus s) => s switch
    {
        ApplicationStatus.OfferDeclined => 4,
        _ => Math.Max(0, Stages.ToList().IndexOf(s))
    };

    private static bool InPeriod(ReportFilter f, DateTime? when) =>
        when is { } w && (f.From is null || DateOnly.FromDateTime(w) >= f.From) && (f.To is null || DateOnly.FromDateTime(w) <= f.To);

    private static bool InPeriod(ReportFilter f, DateOnly when) => (f.From is null || when >= f.From) && (f.To is null || when <= f.To);

    private static T? ParseStatus<T>(ReportFilter f) where T : struct, Enum => Enum.TryParse<T>(f.Status, out var s) ? s : null;

    private static IReadOnlyList<LookupItem> Options<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Select(s => new LookupItem(s.ToString(), EnumText.Label(s))).ToList();

    private static int? Days(DateTime? from, DateTime? to) => from is { } a && to is { } b && b >= a ? (int)Math.Round((b - a).TotalDays) : null;

    private static string Average(IEnumerable<int?> values)
    {
        var list = values.OfType<int>().ToList();
        return list.Count == 0 ? "—" : $"{list.Average():0} days";
    }

    private static string Percent(int part, int whole) => whole == 0 ? "—" : $"{part * 100.0 / whole:0}%";

    private static DateTime? Accepted(Offer o) => o.Status == OfferStatus.Accepted ? o.RespondedAt ?? o.SentAt : null;

    // ===================================================================== Overview (the executive dashboard)

    private static ReportResult Overview(Data d, ReportFilter f)
    {
        var reqs = d.Requests.Where(r => InPeriod(f, r.DateRequested ?? r.CreatedAt)).ToList();
        var approved = d.Requests.Where(r => r.Status == RecruitmentRequestStatus.Approved && InPeriod(f, r.ApprovedAt)).ToList();
        var published = d.Vacancies.Where(v => InPeriod(f, v.PublishedAt)).ToList();
        var apps = d.Applications.Where(a => InPeriod(f, a.SubmittedAt)).ToList();
        var hires = d.Offers.Where(o => InPeriod(f, Accepted(o))).ToList();
        var answered = d.Offers.Where(o => o.Status is OfferStatus.Accepted or OfferStatus.Declined or OfferStatus.Expired && InPeriod(f, o.RespondedAt ?? o.SentAt)).ToList();
        var openPositions = d.Vacancies.Where(v => v.Status is VacancyStatus.Published or VacancyStatus.Closed)
            .Sum(v => Math.Max(0, v.NumberOfPositions - d.Offers.Count(o => o.VacancyId == v.Id && o.Status == OfferStatus.Accepted)));

        var kpis = new List<ReportKpi>
        {
            new("Requests raised", reqs.Count.ToString(Gb), "Requests sent for approval in the period (drafts excluded)"),
            new("Positions approved", approved.Sum(r => r.NumberOfPositions).ToString(Gb), "Positions on requests fully approved in the period"),
            new("Vacancies published", published.Count.ToString(Gb)),
            new("Applications", apps.Count.ToString(Gb), "Applications received in the period"),
            new("Hires", hires.Count.ToString(Gb), "Offers accepted in the period"),
            new("Offer acceptance", Percent(answered.Count(o => o.Status == OfferStatus.Accepted), answered.Count), "Accepted out of accepted + declined + expired"),
            new("Average time to hire", Average(hires.Select(o => Days(RequestDate(d, o), Accepted(o)))), "From the date requested to the accepted offer"),
            new("Positions still open", openPositions.ToString(Gb), "On published or closed vacancies, not yet filled (whatever the period)")
        };

        var funnel = new ReportChart("Recruitment funnel: applications that reached each stage", "applications",
            Stages.Select((s, i) => new ChartBar(EnumText.Label(s), apps.Count(a => d.Reached[a.Id] >= i),
                i == 0 ? null : Percent(apps.Count(a => d.Reached[a.Id] >= i), apps.Count) + " of applications")).ToList());

        // The 12 months up to the end of the period (or this month).
        var end = f.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var months = Enumerable.Range(0, 12).Select(i => new DateOnly(end.Year, end.Month, 1).AddMonths(i - 11)).ToList();
        string M(DateOnly m) => m.ToString("MMM yy", Gb);
        bool Same(DateTime? t, DateOnly m) => t is { } x && x.Year == m.Year && x.Month == m.Month;
        var monthlyApps = new ReportChart("Applications per month", "applications",
            months.Select(m => new ChartBar(M(m), d.Applications.Count(a => Same(a.SubmittedAt, m)))).ToList());
        var monthlyHires = new ReportChart("Hires per month", "hires",
            months.Select(m => new ChartBar(M(m), d.Offers.Count(o => Same(Accepted(o), m)))).ToList());

        var byDivision = d.Divisions.OrderBy(x => x.Key).Select(div =>
        {
            var dHires = hires.Where(o => o.DivisionId == div.Key).ToList();
            return new ReportRow([
                div.Value,
                reqs.Count(r => r.DivisionId == div.Key),
                approved.Where(r => r.DivisionId == div.Key).Sum(r => r.NumberOfPositions),
                apps.Count(a => a.DivisionId == div.Key),
                dHires.Count,
                Average(dHires.Select(o => Days(RequestDate(d, o), Accepted(o))))
            ]);
        }).ToList();

        return new ReportResult
        {
            Key = ReportKeys.Overview,
            PeriodAppliesTo = "the date of each event (request raised, application received, offer accepted ...)",
            Kpis = kpis,
            Charts = [funnel, monthlyApps, monthlyHires],
            Table = new ReportTable(
                [new("Division"), new("Requests raised", true), new("Positions approved", true), new("Applications", true), new("Hires", true), new("Average time to hire", true)],
                byDivision,
                ["Total", reqs.Count, approved.Sum(r => r.NumberOfPositions), apps.Count, hires.Count, Average(hires.Select(o => Days(RequestDate(d, o), Accepted(o))))])
        };
    }

    private static DateTime? RequestDate(Data d, Offer o) =>
        d.VacancyById.GetValueOrDefault(o.VacancyId) is { } v && d.RequestById.GetValueOrDefault(v.RecruitmentRequestId) is { } r ? r.DateRequested ?? r.CreatedAt : null;

    // ===================================================================== Requests

    private static ReportResult Requests(Data d, ReportFilter f)
    {
        var status = ParseStatus<RecruitmentRequestStatus>(f);
        var list = d.Requests.Where(r => InPeriod(f, r.DateRequested ?? r.CreatedAt) && (status is null || r.Status == status))
            .OrderByDescending(r => r.DateRequested ?? r.CreatedAt).ToList();
        var rows = list.Select(r => new ReportRow([
            r.RequestNumber, r.PositionTitle, d.Divisions.GetValueOrDefault(r.DivisionId), d.Departments.GetValueOrDefault(r.DepartmentId),
            r.NumberOfPositions, r.EstimatedAnnualCost, EnumText.Label(r.Status), DateOf(r.DateRequested ?? r.CreatedAt), DateOf(r.ApprovedAt),
            Days(r.DateRequested ?? r.CreatedAt, r.ApprovedAt)
        ], new ReportLink("RecruitmentRequests", "Details", r.Id))).ToList();

        var approved = list.Where(r => r.ApprovedAt is not null).ToList();
        return new ReportResult
        {
            Key = ReportKeys.Requests,
            PeriodAppliesTo = "the date requested",
            StatusOptions = Options<RecruitmentRequestStatus>().Where(o => o.Value != nameof(RecruitmentRequestStatus.Draft)).ToList(),
            Kpis =
            [
                new("Requests", list.Count.ToString(Gb)),
                new("Positions requested", list.Sum(r => r.NumberOfPositions).ToString(Gb)),
                new("Approved", approved.Count.ToString(Gb)),
                new("In approval", list.Count(r => r.Status is RecruitmentRequestStatus.RecruitmentReview or RecruitmentRequestStatus.HrApproval or RecruitmentRequestStatus.MdApproval).ToString(Gb)),
                new("Estimated annual cost", $"GHS {list.Sum(r => r.EstimatedAnnualCost):N0}"),
                new("Average time to approve", Average(approved.Select(r => Days(r.DateRequested ?? r.CreatedAt, r.ApprovedAt))))
            ],
            Table = new ReportTable(
                [new("Request"), new("Position"), new("Division"), new("Department"), new("Positions", true), new("Est. annual cost (GHS)", true),
                 new("Status"), new("Requested"), new("Approved"), new("Days to approve", true)],
                rows, ["Total", null, null, null, list.Sum(r => r.NumberOfPositions), list.Sum(r => r.EstimatedAnnualCost), null, null, null, null])
        };
    }

    private static DateOnly? DateOf(DateTime? t) => t is { } x ? DateOnly.FromDateTime(x) : null;

    // ===================================================================== Vacancies (with the funnel per vacancy)

    private static ReportResult Vacancies(Data d, ReportFilter f)
    {
        var status = ParseStatus<VacancyStatus>(f);
        var list = d.Vacancies.Where(v => InPeriod(f, v.PublishedAt ?? v.CreatedAt) && (status is null || v.Status == status))
            .OrderByDescending(v => v.PublishedAt ?? v.CreatedAt).ToList();
        var appsByVacancy = d.Applications.GroupBy(a => a.VacancyId).ToDictionary(g => g.Key, g => g.ToList());
        int Reached(int vacancyId, ApplicationStatus stage) =>
            (appsByVacancy.GetValueOrDefault(vacancyId) ?? []).Count(a => d.Reached[a.Id] >= StageIndex(stage));
        int Filled(int vacancyId) => d.Offers.Count(o => o.VacancyId == vacancyId && o.Status == OfferStatus.Accepted);
        int? Open(Vacancy v) => v.Status is VacancyStatus.Published or VacancyStatus.Closed ? Math.Max(0, v.NumberOfPositions - Filled(v.Id)) : null;

        var rows = list.Select(v => new ReportRow([
            v.VacancyNumber, v.JobTitle, d.Departments.GetValueOrDefault(v.DepartmentId), EnumText.Label(v.Status), DateOf(v.PublishedAt), v.ClosingDate,
            v.NumberOfPositions, Reached(v.Id, ApplicationStatus.Submitted), Reached(v.Id, ApplicationStatus.Shortlisted), Reached(v.Id, ApplicationStatus.Interview),
            Reached(v.Id, ApplicationStatus.Offer), Filled(v.Id), Open(v)
        ], new ReportLink("Vacancies", "Details", v.Id))).ToList();

        return new ReportResult
        {
            Key = ReportKeys.Vacancies,
            PeriodAppliesTo = "the date published (or created, if not yet published)",
            StatusOptions = Options<VacancyStatus>(),
            Kpis =
            [
                new("Vacancies", list.Count.ToString(Gb)),
                new("Positions", list.Sum(v => v.NumberOfPositions).ToString(Gb)),
                new("Filled", list.Sum(v => Filled(v.Id)).ToString(Gb), "Offers accepted"),
                new("Still open", list.Sum(v => Open(v) ?? 0).ToString(Gb), "Positions on published or closed vacancies not yet filled"),
                new("Applications per vacancy", list.Count == 0 ? "—" : $"{list.Average(v => (double)Reached(v.Id, ApplicationStatus.Submitted)):0.0}")
            ],
            Table = new ReportTable(
                [new("Vacancy"), new("Job title"), new("Department"), new("Status"), new("Published"), new("Closing date"), new("Positions", true),
                 new("Applications", true), new("Shortlisted", true), new("Interviewed", true), new("Offered", true), new("Filled", true), new("Open", true)],
                rows,
                ["Total", null, null, null, null, null, list.Sum(v => v.NumberOfPositions), list.Sum(v => Reached(v.Id, ApplicationStatus.Submitted)),
                 list.Sum(v => Reached(v.Id, ApplicationStatus.Shortlisted)), list.Sum(v => Reached(v.Id, ApplicationStatus.Interview)),
                 list.Sum(v => Reached(v.Id, ApplicationStatus.Offer)), list.Sum(v => Filled(v.Id)), list.Sum(v => Open(v) ?? 0)])
        };
    }

    // ===================================================================== Applications

    private static ReportResult Applications(Data d, ReportFilter f)
    {
        var status = ParseStatus<ApplicationStatus>(f);
        var list = d.Applications.Where(a => InPeriod(f, a.SubmittedAt) && (status is null || a.Status == status)).OrderByDescending(a => a.SubmittedAt).ToList();
        var rows = list.Select(a => new ReportRow([
            a.ApplicationNumber, a.FullName, d.VacancyById.GetValueOrDefault(a.VacancyId)?.VacancyNumber, d.Departments.GetValueOrDefault(a.DepartmentId),
            a.IsInternal ? "Staff" : "External", EnumText.Label(a.Source), DateOf(a.SubmittedAt), a.ScreeningScore, EnumText.Label(Stages[d.Reached[a.Id]]),
            EnumText.Label(a.Status)
        ], new ReportLink("Applications", "Details", a.Id))).ToList();

        return new ReportResult
        {
            Key = ReportKeys.Applications,
            PeriodAppliesTo = "the date received",
            StatusOptions = Options<ApplicationStatus>(),
            Kpis =
            [
                new("Applications", list.Count.ToString(Gb)),
                new("From staff", list.Count(a => a.IsInternal).ToString(Gb)),
                new("Shortlisted or further", Percent(list.Count(a => d.Reached[a.Id] >= 2), list.Count)),
                new("Hired", list.Count(a => a.Status == ApplicationStatus.Hired).ToString(Gb)),
                new("Average screening score", list.Any(a => a.ScreeningScore is not null) ? $"{list.Where(a => a.ScreeningScore is not null).Average(a => a.ScreeningScore!.Value):0}" : "—")
            ],
            Table = new ReportTable(
                [new("Application"), new("Applicant"), new("Vacancy"), new("Department"), new("Staff / external"), new("Source"), new("Received"),
                 new("Screening score", true), new("Furthest stage"), new("Status")],
                rows)
        };
    }

    // ===================================================================== Sources

    private static ReportResult Sources(Data d, ReportFilter f)
    {
        var status = ParseStatus<ApplicationStatus>(f);
        var list = d.Applications.Where(a => InPeriod(f, a.SubmittedAt) && (status is null || a.Status == status)).ToList();
        var rows = Enum.GetValues<ApplicationSource>().Select(s =>
        {
            var group = list.Where(a => a.Source == s).ToList();
            var hired = group.Count(a => d.Reached[a.Id] >= 5);
            return new ReportRow([EnumText.Label(s), group.Count, group.Count(a => d.Reached[a.Id] >= 2), group.Count(a => d.Reached[a.Id] >= 3), hired,
                Percent(hired, group.Count)]);
        }).Where(r => (int)r.Cells[1]! > 0).OrderByDescending(r => (int)r.Cells[1]!).ToList();

        return new ReportResult
        {
            Key = ReportKeys.Sources,
            PeriodAppliesTo = "the date received",
            StatusOptions = Options<ApplicationStatus>(),
            Kpis = [new("Applications", list.Count.ToString(Gb)), new("Sources used", rows.Count.ToString(Gb))],
            Charts = [new ReportChart("Applications by source", "applications", rows.Select(r => new ChartBar((string)r.Cells[0]!, (int)r.Cells[1]!, $"{r.Cells[4]} hired")).ToList())],
            Table = new ReportTable(
                [new("Source"), new("Applications", true), new("Shortlisted", true), new("Interviewed", true), new("Hired", true), new("Hired per application", true)],
                rows,
                ["Total", list.Count, list.Count(a => d.Reached[a.Id] >= 2), list.Count(a => d.Reached[a.Id] >= 3), list.Count(a => d.Reached[a.Id] >= 5),
                 Percent(list.Count(a => d.Reached[a.Id] >= 5), list.Count)])
        };
    }

    // ===================================================================== Offers

    private static ReportResult Offers(Data d, ReportFilter f)
    {
        var status = ParseStatus<OfferStatus>(f);
        var list = d.Offers.Where(o => InPeriod(f, o.SentAt) && (status is null || o.Status == status)).OrderByDescending(o => o.SentAt).ToList();
        var names = d.Applications.ToDictionary(a => a.Id, a => a.FullName);
        var rows = list.Select(o => new ReportRow([
            o.Number, names.GetValueOrDefault(o.JobApplicationId), o.JobTitle, d.Departments.GetValueOrDefault(o.DepartmentId), o.MonthlySalary,
            DateOf(o.SentAt), o.ExpiresOn, EnumText.Label(o.Status), DateOf(o.RespondedAt), Days(o.SentAt, o.RespondedAt)
        ], new ReportLink("Hiring", "Case", o.JobApplicationId))).ToList();
        var answered = list.Count(o => o.Status is OfferStatus.Accepted or OfferStatus.Declined or OfferStatus.Expired);

        return new ReportResult
        {
            Key = ReportKeys.Offers,
            PeriodAppliesTo = "the date sent",
            StatusOptions = Options<OfferStatus>(),
            Kpis =
            [
                new("Offers sent", list.Count.ToString(Gb)),
                new("Accepted", list.Count(o => o.Status == OfferStatus.Accepted).ToString(Gb)),
                new("Declined", list.Count(o => o.Status == OfferStatus.Declined).ToString(Gb)),
                new("Expired / withdrawn", list.Count(o => o.Status is OfferStatus.Expired or OfferStatus.Withdrawn).ToString(Gb)),
                new("Acceptance rate", Percent(list.Count(o => o.Status == OfferStatus.Accepted), answered), "Accepted out of accepted + declined + expired"),
                new("Average monthly salary", list.Count == 0 ? "—" : $"GHS {list.Average(o => o.MonthlySalary):N0}")
            ],
            Table = new ReportTable(
                [new("Offer"), new("Candidate"), new("Job"), new("Department"), new("Monthly salary (GHS)", true), new("Sent"), new("Reply by"), new("Status"),
                 new("Answered"), new("Days to answer", true)],
                rows)
        };
    }

    // ===================================================================== Time to hire

    private static ReportResult TimeToHire(Data d, ReportFilter f)
    {
        var hires = d.Offers.Where(o => o.Status == OfferStatus.Accepted && InPeriod(f, Accepted(o))).OrderByDescending(o => Accepted(o)).ToList();
        var names = d.Applications.ToDictionary(a => a.Id, a => a.FullName);
        var lines = hires.Select(o =>
        {
            var v = d.VacancyById.GetValueOrDefault(o.VacancyId);
            var r = v is null ? null : d.RequestById.GetValueOrDefault(v.RecruitmentRequestId);
            var requested = r?.DateRequested ?? r?.CreatedAt;
            return (Offer: o, Requested: requested, Approved: r?.ApprovedAt, Published: v?.PublishedAt, Accepted: Accepted(o));
        }).ToList();
        var rows = lines.Select(x => new ReportRow([
            names.GetValueOrDefault(x.Offer.JobApplicationId), x.Offer.JobTitle, d.Departments.GetValueOrDefault(x.Offer.DepartmentId),
            DateOf(x.Requested), DateOf(x.Approved), DateOf(x.Published), DateOf(x.Accepted), x.Offer.StartDate,
            Days(x.Requested, x.Approved), Days(x.Approved, x.Published), Days(x.Published, x.Accepted), Days(x.Requested, x.Accepted)
        ], new ReportLink("Hiring", "Case", x.Offer.JobApplicationId))).ToList();

        var byDept = lines.GroupBy(x => x.Offer.DepartmentId)
            .Select(g => (Name: d.Departments.GetValueOrDefault(g.Key, "?"), Hires: g.Count(), Days: g.Select(x => Days(x.Requested, x.Accepted)).OfType<int>().ToList()))
            .Where(x => x.Days.Count > 0)
            .Select(x => new ChartBar(x.Name, (decimal)Math.Round(x.Days.Average()), $"{x.Hires} hire(s)"))
            .OrderByDescending(b => b.Value).ToList();

        return new ReportResult
        {
            Key = ReportKeys.TimeToHire,
            PeriodAppliesTo = "the date the offer was accepted",
            Kpis =
            [
                new("Hires", hires.Count.ToString(Gb)),
                new("Request → approval", Average(lines.Select(x => Days(x.Requested, x.Approved)))),
                new("Approval → published", Average(lines.Select(x => Days(x.Approved, x.Published)))),
                new("Published → offer accepted", Average(lines.Select(x => Days(x.Published, x.Accepted)))),
                new("Total time to hire", Average(lines.Select(x => Days(x.Requested, x.Accepted))), "From the date requested to the accepted offer")
            ],
            Charts = byDept.Count == 0 ? [] : [new ReportChart("Average time to hire by department", "days", byDept)],
            Table = new ReportTable(
                [new("New employee"), new("Job"), new("Department"), new("Requested"), new("Approved"), new("Published"), new("Offer accepted"), new("Start date"),
                 new("Days to approve", true), new("Days to publish", true), new("Days to fill", true), new("Total days", true)],
                rows)
        };
    }

    // ===================================================================== Onboarding

    private static ReportResult Onboarding(Data d, ReportFilter f)
    {
        var status = ParseStatus<OnboardingStatus>(f);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = d.Onboardings.Where(o => InPeriod(f, o.StartDate) && (status is null || o.Status == status)).OrderByDescending(o => o.StartDate).ToList();
        var tasks = d.Tasks.GroupBy(t => t.OnboardingRecordId).ToDictionary(g => g.Key, g => g.ToList());
        int Done(int id) => (tasks.GetValueOrDefault(id) ?? []).Count(t => t.Status != OnboardingTaskStatus.Open);
        int Total(int id) => (tasks.GetValueOrDefault(id) ?? []).Count;
        int Overdue(OnboardingRecord o) => o.Status != OnboardingStatus.InProgress ? 0 :
            (tasks.GetValueOrDefault(o.Id) ?? []).Count(t => t.Status == OnboardingTaskStatus.Open && t.DueDate < today);

        var rows = list.Select(o => new ReportRow([
            o.Number, o.EmployeeName, o.JobTitle, d.Departments.GetValueOrDefault(o.DepartmentId), o.StartDate, EnumText.Label(o.Status),
            o.LetterAcknowledged ? "Signed" : o.LetterIssued ? "Issued" : "Not issued", $"{Done(o.Id)} / {Total(o.Id)}", Overdue(o)
        ], new ReportLink("Onboarding", "Details", o.Id))).ToList();

        return new ReportResult
        {
            Key = ReportKeys.Onboarding,
            PeriodAppliesTo = "the start date",
            StatusOptions = Options<OnboardingStatus>(),
            Kpis =
            [
                new("Onboardings", list.Count.ToString(Gb)),
                new("In progress", list.Count(o => o.Status == OnboardingStatus.InProgress).ToString(Gb)),
                new("Completed", list.Count(o => o.Status == OnboardingStatus.Completed).ToString(Gb)),
                new("Letters not yet signed", list.Count(o => o.Status == OnboardingStatus.InProgress && !o.LetterAcknowledged).ToString(Gb)),
                new("Overdue checklist items", list.Sum(Overdue).ToString(Gb))
            ],
            Table = new ReportTable(
                [new("Onboarding"), new("New employee"), new("Job"), new("Department"), new("Start date"), new("Status"), new("Letter"), new("Checklist"),
                 new("Overdue items", true)],
                rows)
        };
    }
}
