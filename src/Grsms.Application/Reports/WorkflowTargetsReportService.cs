using System.Globalization;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Application.Settings;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Reports;

/// <summary>
/// 12 November 2026 (docs/35 R1): the Workflow targets report. For each workflow step it measures how many working days
/// (Monday to Friday) the record waited, against the target set in System settings → Workflow targets, and says whether the
/// step finished on time, late, or is still waiting (within or past the target).
///
///   * Request steps (HOD review, HR Manager review, MD approval), vacancy approval and advert review (HR review, IT approval)
///     come from each record's history: a step starts when the record enters that status and ends when it leaves it. A record
///     returned and resubmitted counts each wait separately.
///   * Offer answer: sent → accepted or declined (an expired offer counts as late; a withdrawn one without an answer is left out).
///   * Document verification: uploaded → verified or rejected.
/// The period filter applies to the date each step started; the status filter to the result. Visibility follows
/// <see cref="AccessScope"/> as in every report.
/// </summary>
public class WorkflowTargetsReportService(
    IRepository<RecruitmentRequest> requests,
    IRepository<RecruitmentRequestAction> requestActions,
    IRepository<Vacancy> vacancies,
    IRepository<VacancyActionEntry> vacancyActions,
    IRepository<JobAdvert> adverts,
    IRepository<AdvertActionEntry> advertActions,
    IRepository<Offer> offers,
    IRepository<OnboardingRecord> onboardings,
    IRepository<EmployeeDocument> documents,
    IRepository<Department> departments,
    ISettingsStore settings,
    AccessScope scope)
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");

    public const string OnTime = "OnTime", Late = "Late", Waiting = "Waiting", Overdue = "Overdue";

    private static readonly IReadOnlyList<LookupItem> ResultOptions =
    [
        new(OnTime, "Finished on time"), new(Late, "Finished late"), new(Waiting, "Waiting, within target"), new(Overdue, "Waiting, past target")
    ];

    /// <summary>One wait at one workflow step.</summary>
    /// <param name="AlwaysLate">Finished without the step being done (an offer that expired unanswered).</param>
    public sealed record StepWait(string Step, int Target, string Record, int DepartmentId, DateTime Started, DateTime? Finished, ReportLink Link,
        bool AlwaysLate = false);

    /// <summary>The steps in report order, with their target setting.</summary>
    public static readonly IReadOnlyList<(string Step, string SettingKey)> Steps =
    [
        ("Request: HOD review", SettingCatalogue.TargetHodReview),
        ("Request: HR Manager review", SettingCatalogue.TargetHrApproval),
        ("Request: MD approval", SettingCatalogue.TargetMdApproval),
        ("Vacancy approval", SettingCatalogue.TargetVacancyApproval),
        ("Advert: HR review", SettingCatalogue.TargetAdvertReview),
        ("Advert: IT approval", SettingCatalogue.TargetAdvertReview),
        ("Offer answer", SettingCatalogue.TargetOfferAnswer),
        ("Document verification", SettingCatalogue.TargetDocumentVerification)
    ];

    /// <summary>Working days from <paramref name="started"/> to <paramref name="finished"/> (or today), not counting the start day.</summary>
    public static int WorkingDays(DateTime started, DateTime? finished, DateTime now) =>
        OnboardingWorkflow.WorkingDaysBetween(DateOnly.FromDateTime(started), DateOnly.FromDateTime(finished ?? now));

    public static string ResultOf(StepWait w, DateTime now)
    {
        var days = WorkingDays(w.Started, w.Finished, now);
        return w.Finished is null ? (days > w.Target ? Overdue : Waiting) : (w.AlwaysLate || days > w.Target ? Late : OnTime);
    }

    public async Task<ReportResult> RunAsync(ReportFilter f, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var waits = await CollectAsync(f, ct);
        var depts = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id);
        var v = await scope.VisibilityAsync(ct);
        bool Visible(int deptId) =>
            (v.SeeAll || v.HodDepartmentIds.Contains(deptId)) && (f.DepartmentId is null || f.DepartmentId == deptId) &&
            (f.DivisionId is null || depts.GetValueOrDefault(deptId)?.DivisionId == f.DivisionId);

        var shown = waits
            .Where(w => Visible(w.DepartmentId) && InPeriod(f, w.Started))
            .Select(w => (Wait: w, Days: WorkingDays(w.Started, w.Finished, now), Result: ResultOf(w, now)))
            .ToList();
        var counted = shown; // KPIs and chart use every step in the filters, whatever the result filter
        if (f.Status is { Length: > 0 } status) shown = shown.Where(x => x.Result == status).ToList();

        var finished = counted.Where(x => x.Wait.Finished is not null).ToList();
        var onTime = finished.Count(x => x.Result == OnTime);
        string Pct(int part, int whole) => whole == 0 ? "—" : (100m * part / whole).ToString("0", Gb) + " %";

        var bars = Steps.Select(s => s.Step).Distinct().Select(step =>
        {
            var done = finished.Where(x => x.Wait.Step == step).ToList();
            var ok = done.Count(x => x.Result == OnTime);
            var target = Target(step);
            return new ChartBar(step, done.Count == 0 ? 0 : Math.Round(100m * ok / done.Count), done.Count == 0
                ? $"nothing finished yet (target {target} working days)"
                : $"{ok} of {done.Count} on time, average {done.Average(x => x.Days).ToString("0.#", Gb)} days (target {target})");
        }).ToList();

        var rows = shown
            .OrderBy(x => x.Result switch { Overdue => 0, Waiting => 1, Late => 2, _ => 3 })
            .ThenByDescending(x => x.Days - x.Wait.Target)
            .Select(x => new ReportRow(
            [
                x.Wait.Step, x.Wait.Record, depts.GetValueOrDefault(x.Wait.DepartmentId)?.Name ?? "", x.Wait.Started, x.Wait.Finished,
                x.Days, x.Wait.Target, ResultOptions.First(o => o.Value == x.Result).Text
            ], x.Wait.Link)).ToList();

        return new ReportResult
        {
            Key = ReportKeys.WorkflowTargets,
            PeriodAppliesTo = "the date each step started",
            StatusOptions = ResultOptions,
            Kpis =
            [
                new("Steps finished on time", Pct(onTime, finished.Count), $"{onTime} of {finished.Count} finished steps took no longer than their target."),
                new("Finished late", finished.Count(x => x.Result == Late).ToString(Gb)),
                new("Waiting past target now", counted.Count(x => x.Result == Overdue).ToString(Gb), "Still waiting and already longer than the target: chase these first."),
                new("Waiting within target", counted.Count(x => x.Result == Waiting).ToString(Gb))
            ],
            Charts = [new ReportChart("Steps finished on time", "%", bars)],
            Table = new ReportTable([new("Step"), new("Record"), new("Department"), new("Started"), new("Finished"),
                new("Working days", true), new("Target", true), new("Result")], rows)
        };
    }

    private int Target(string step) => settings.GetInt(Steps.First(s => s.Step == step).SettingKey);

    /// <summary>Every wait at every step (before visibility and filters other than the vacancy).</summary>
    public async Task<List<StepWait>> CollectAsync(ReportFilter f, CancellationToken ct = default)
    {
        var list = new List<StepWait>();
        int? vac = f.VacancyId;

        // ---- Recruitment requests
        int? requestOfVacancy = null;
        if (vac is not null) requestOfVacancy = (await vacancies.GetByIdAsync(vac.Value, ct))?.RecruitmentRequestId ?? -1;
        var reqs = (await requests.ListAsync(r => r.Status != RecruitmentRequestStatus.Draft && (requestOfVacancy == null || r.Id == requestOfVacancy), ct))
            .ToDictionary(r => r.Id);
        var reqIds = reqs.Keys.ToList();
        var reqHistory = reqIds.Count == 0 ? [] : await requestActions.ListAsync(a => reqIds.Contains(a.RecruitmentRequestId), ct);
        foreach (var g in reqHistory.GroupBy(a => a.RecruitmentRequestId))
        {
            var r = reqs[g.Key];
            var h = g.OrderBy(a => a.ActionAt).ThenBy(a => a.Id).Select(a => (a.ActionAt, From: a.FromStatus, To: a.ToStatus)).ToList();
            var link = new ReportLink("RecruitmentRequests", "Details", r.Id);
            Add(list, "Request: HOD review", r.RequestNumber, r.DepartmentId, link, Waits(h, RecruitmentRequestStatus.DepartmentHeadReview, r.Status));
            Add(list, "Request: HR Manager review", r.RequestNumber, r.DepartmentId, link, Waits(h, RecruitmentRequestStatus.HrApproval, r.Status));
            Add(list, "Request: MD approval", r.RequestNumber, r.DepartmentId, link, Waits(h, RecruitmentRequestStatus.MdApproval, r.Status));
        }

        // ---- Vacancies
        var vacs = (await vacancies.ListAsync(x => vac == null || x.Id == vac, ct)).ToDictionary(x => x.Id);
        var vacIds = vacs.Keys.ToList();
        var vacHistory = vacIds.Count == 0 ? [] : await vacancyActions.ListAsync(a => vacIds.Contains(a.VacancyId), ct);
        foreach (var g in vacHistory.GroupBy(a => a.VacancyId))
        {
            var x = vacs[g.Key];
            var h = g.OrderBy(a => a.ActionAt).ThenBy(a => a.Id).Select(a => (a.ActionAt, From: a.FromStatus, To: a.ToStatus)).ToList();
            Add(list, "Vacancy approval", x.VacancyNumber, x.DepartmentId, new ReportLink("Vacancies", "Details", x.Id), Waits(h, VacancyStatus.PendingApproval, x.Status));
        }

        // ---- Adverts
        var ads = (await adverts.ListAsync(x => vac == null || x.VacancyId == vac, ct)).ToDictionary(x => x.Id);
        var adIds = ads.Keys.ToList();
        var adHistory = adIds.Count == 0 ? [] : await advertActions.ListAsync(a => adIds.Contains(a.JobAdvertId), ct);
        foreach (var g in adHistory.GroupBy(a => a.JobAdvertId))
        {
            var x = ads[g.Key];
            var h = g.OrderBy(a => a.ActionAt).ThenBy(a => a.Id).Select(a => (a.ActionAt, From: a.FromStatus, To: a.ToStatus)).ToList();
            var link = new ReportLink("Adverts", "Details", x.Id);
            Add(list, "Advert: HR review", x.AdvertNumber, x.DepartmentId, link, Waits(h, AdvertStatus.AwaitingHrReview, x.Status));
            Add(list, "Advert: IT approval", x.AdvertNumber, x.DepartmentId, link, Waits(h, AdvertStatus.AwaitingItApproval, x.Status));
        }

        // ---- Offers
        foreach (var o in await offers.ListAsync(x => vac == null || x.VacancyId == vac, ct))
        {
            DateTime? end = o.Status switch
            {
                OfferStatus.Sent => null,
                OfferStatus.Expired => o.RespondedAt ?? o.ExpiresOn.ToDateTime(TimeOnly.MinValue),
                _ => o.RespondedAt
            };
            if (o.Status == OfferStatus.Withdrawn && o.RespondedAt is null) continue;
            // An expired offer was never answered: always late.
            list.Add(new StepWait("Offer answer", Target("Offer answer"), o.Number, o.DepartmentId, o.SentAt, end,
                new ReportLink("Hiring", "Case", o.JobApplicationId), AlwaysLate: o.Status == OfferStatus.Expired));
        }

        // ---- New-hire documents
        var obs = (await onboardings.ListAsync(x => vac == null || x.VacancyId == vac, ct)).ToDictionary(x => x.Id);
        var obIds = obs.Keys.ToList();
        var docs = obIds.Count == 0 ? [] : await documents.ListAsync(d => obIds.Contains(d.OnboardingRecordId) && d.IsApplicable && d.SubmittedAt != null &&
            (d.Status == EmployeeDocumentStatus.Submitted || d.Status == EmployeeDocumentStatus.UnderReview ||
             d.Status == EmployeeDocumentStatus.Verified || d.Status == EmployeeDocumentStatus.Rejected), ct);
        foreach (var d in docs)
        {
            var o = obs[d.OnboardingRecordId];
            var done = d.Status is EmployeeDocumentStatus.Verified or EmployeeDocumentStatus.Rejected;
            // A file re-uploaded after the decision is not measured twice: the last upload counts.
            if (done && d.VerifiedAt is { } at && at < d.SubmittedAt!.Value) continue;
            list.Add(new StepWait("Document verification", Target("Document verification"), o.Number, o.DepartmentId, d.SubmittedAt!.Value,
                done ? d.VerifiedAt ?? d.SubmittedAt : null, new ReportLink("Onboarding", "Details", o.Id)));
        }
        return list;
    }

    private void Add(List<StepWait> list, string step, string record, int deptId, ReportLink link, IEnumerable<(DateTime Start, DateTime? End)> waits)
    {
        var target = Target(step);
        list.AddRange(waits.Select(w => new StepWait(step, target, record, deptId, w.Start, w.End, link)));
    }

    /// <summary>
    /// The waits at <paramref name="stage"/> from a record's history (oldest first): each starts when the record entered the
    /// stage and ends when it left; one still open is unfinished if the record is still at that stage.
    /// </summary>
    public static IEnumerable<(DateTime Start, DateTime? End)> Waits<T>(IReadOnlyList<(DateTime At, T From, T To)> history, T stage, T current)
        where T : struct, Enum
    {
        DateTime? start = null;
        foreach (var (at, from, to) in history)
        {
            if (start is { } s && EqualityComparer<T>.Default.Equals(from, stage) && !EqualityComparer<T>.Default.Equals(to, stage))
            {
                yield return (s, at);
                start = null;
            }
            if (start is null && EqualityComparer<T>.Default.Equals(to, stage) && !EqualityComparer<T>.Default.Equals(from, stage)) start = at;
        }
        if (start is { } open && EqualityComparer<T>.Default.Equals(current, stage)) yield return (open, null);
    }

    private static bool InPeriod(ReportFilter f, DateTime when) =>
        (f.From is null || DateOnly.FromDateTime(when) >= f.From) && (f.To is null || DateOnly.FromDateTime(when) <= f.To);
}
