using Grsms.Application.Probation;
using System.Globalization;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Onboarding;
using Grsms.Application.Organization;
using Grsms.Application.Reports;
using Grsms.Application.Users;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Probation;
using Grsms.Domain.Security;

namespace Grsms.Application.Workforce;

/// <summary>
/// Phase 21: the onboarding and probation reports, in the same shape as the other reports (KPIs and a table, exported to
/// CSV by the Reports page). <see cref="ReportService"/> hands these keys over to this class.
///   * Missing documents, Document verification and Beneficiaries hold personal data: only holders of "Open personnel
///     files" see them (everyone else gets an empty report saying so).
///   * Probation, Probation extensions and Confirmations: HR and "See all departments" see everything, a HOD their departments.
/// The Division and Department filters apply to every report; the period to each report's main date (shown on the page).
/// </summary>
public class WorkforceReportService(
    IRepository<OnboardingRecord> onboardings,
    IRepository<EmployeeDocument> documents,
    IRepository<OnboardingDocumentType> documentTypes,
    IPersonalRecordRepository personalRecords,
    IRepository<ProbationRecord> probations,
    IRepository<ProbationAssessment> assessments,
    IRepository<ConfirmationRecord> confirmations,
    IRepository<Department> departments,
    IUserDirectory users,
    ICurrentUserService me,
    AccessScope scope)
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static readonly IReadOnlySet<string> Keys = new HashSet<string>
    {
        ReportKeys.MissingDocuments, ReportKeys.DocumentVerification, ReportKeys.Beneficiaries,
        ReportKeys.Probation, ReportKeys.ProbationExtensions, ReportKeys.Confirmations
    };

    private static readonly IReadOnlySet<string> PersonalDataKeys = new HashSet<string>
        { ReportKeys.MissingDocuments, ReportKeys.DocumentVerification, ReportKeys.Beneficiaries };

    public async Task<ReportResult> RunAsync(ReportFilter f, CancellationToken ct = default)
    {
        if (PersonalDataKeys.Contains(f.Report) && !me.HasPermission(Permissions.ViewPersonnelFiles))
            return new ReportResult
            {
                Key = f.Report,
                Kpis = [new("Not available", "—", "This report holds personal data: it needs the permission \"Open personnel files\".")]
            };

        var depts = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id);
        var all = me.HasPermission(Permissions.ViewOnboarding) || me.HasPermission(Permissions.ViewProbation) || me.HasPermission(Permissions.SeeAllDepartments);
        var mine = me.IsInRole(Roles.HeadOfDepartment) ? await scope.HodDepartmentIdsAsync(ct) : [];
        bool Visible(int deptId) =>
            (all || mine.Contains(deptId)) && (f.DepartmentId is null || f.DepartmentId == deptId) &&
            (f.DivisionId is null || depts.GetValueOrDefault(deptId)?.DivisionId == f.DivisionId);
        string Dept(int id) => depts.GetValueOrDefault(id)?.Name ?? "";

        return f.Report switch
        {
            ReportKeys.MissingDocuments => await MissingDocumentsAsync(f, Visible, Dept, ct),
            ReportKeys.DocumentVerification => await DocumentVerificationAsync(f, Visible, Dept, ct),
            ReportKeys.Beneficiaries => await BeneficiariesAsync(f, Visible, Dept, ct),
            ReportKeys.Probation => await ProbationAsync(f, Visible, Dept, ct),
            ReportKeys.ProbationExtensions => await ExtensionsAsync(f, Visible, Dept, ct),
            _ => await ConfirmationsAsync(f, Visible, Dept, ct)
        };
    }

    // ===================================================================== Onboarding documents

    private async Task<ReportResult> MissingDocumentsAsync(ReportFilter f, Func<int, bool> visible, Func<int, string> dept, CancellationToken ct)
    {
        var waiting = (await onboardings.ListAsync(o => o.Status == OnboardingStatus.Draft || o.Status == OnboardingStatus.AwaitingEmployeeSubmission ||
                                                        o.Status == OnboardingStatus.AwaitingDocumentVerification, ct))
            .Where(o => visible(o.DepartmentId) && InPeriod(f, o.StartDate)).ToList();
        var ids = waiting.Select(o => o.Id).ToList();
        var docs = (await documents.ListAsync(d => ids.Contains(d.OnboardingRecordId) && d.IsApplicable, ct)).GroupBy(d => d.OnboardingRecordId).ToDictionary(g => g.Key, g => g.ToList());
        var types = await TypesAsync(ct);
        var rows = waiting.Select(o =>
            {
                var missing = (docs.GetValueOrDefault(o.Id) ?? []).Where(d => d.IsRequired && d.Status == EmployeeDocumentStatus.Pending || d.Status == EmployeeDocumentStatus.Rejected)
                    .OrderBy(d => types.GetValueOrDefault(d.DocumentTypeCode)?.SortOrder ?? int.MaxValue).ToList();
                return (o, missing);
            })
            .Where(x => x.missing.Count > 0)
            .OrderBy(x => x.o.StartDate)
            .Select(x => new ReportRow([
                x.o.Number, x.o.EmployeeName, dept(x.o.DepartmentId), x.o.StartDate, EnumText.Label(x.o.Status), x.missing.Count,
                x.missing.Count(d => d.Status == EmployeeDocumentStatus.Rejected),
                string.Join("; ", x.missing.Select(d => TypeName(types, d) + (d.Status == EmployeeDocumentStatus.Rejected ? " (rejected)" : "")))
            ], new ReportLink("Onboarding", "Details", x.o.Id))).ToList();
        return new ReportResult
        {
            Key = ReportKeys.MissingDocuments,
            PeriodAppliesTo = "the start date",
            Kpis = [new("New employees with missing documents", rows.Count.ToString(Gb)), new("Documents missing", rows.Sum(r => (int)r.Cells[5]!).ToString(Gb))],
            Table = new ReportTable([new("Onboarding"), new("New employee"), new("Department"), new("Start date"), new("Stage"), new("Missing", true),
                new("Rejected", true), new("Documents")], rows)
        };
    }

    private async Task<ReportResult> DocumentVerificationAsync(ReportFilter f, Func<int, bool> visible, Func<int, string> dept, CancellationToken ct)
    {
        var obs = (await onboardings.ListAsync(o => f.DepartmentId == null || o.DepartmentId == f.DepartmentId, ct)).Where(o => visible(o.DepartmentId)).ToDictionary(o => o.Id);
        var ids = obs.Keys.ToList();
        var docs = (await documents.ListAsync(d => ids.Contains(d.OnboardingRecordId) && d.Status != EmployeeDocumentStatus.Pending, ct))
            .Where(d => InPeriod(f, d.SubmittedAt is { } s ? DateOnly.FromDateTime(s) : null))
            .Where(d => f.Status is null || d.Status.ToString() == f.Status).ToList();
        var names = await users.GetDisplayNamesAsync(docs.Select(d => d.VerifiedByUserId).OfType<string>().Distinct(), ct);
        var types = await TypesAsync(ct);
        var rows = docs.OrderBy(d => obs[d.OnboardingRecordId].Number).ThenBy(d => types.GetValueOrDefault(d.DocumentTypeCode)?.SortOrder ?? int.MaxValue).Select(d =>
        {
            var o = obs[d.OnboardingRecordId];
            return new ReportRow([o.Number, o.EmployeeName, dept(o.DepartmentId), TypeName(types, d), EnumText.Label(d.Status),
                d.SubmittedAt, d.VerifiedByUserId is null ? null : names.GetValueOrDefault(d.VerifiedByUserId), d.VerifiedAt,
                d.RejectionReason ?? d.NotApplicableReason], new ReportLink("Onboarding", "Details", o.Id));
        }).ToList();
        return new ReportResult
        {
            Key = ReportKeys.DocumentVerification,
            PeriodAppliesTo = "the date the document was submitted",
            StatusOptions = Enum.GetValues<EmployeeDocumentStatus>().Where(s => s != EmployeeDocumentStatus.Pending)
                .Select(s => new LookupItem(s.ToString(), EnumText.Label(s))).ToList(),
            Kpis =
            [
                new("Documents submitted", docs.Count.ToString(Gb)),
                new("Verified", docs.Count(d => d.Status == EmployeeDocumentStatus.Verified).ToString(Gb)),
                new("Awaiting verification", docs.Count(d => OnboardingWorkflow.AwaitsReview(d.Status)).ToString(Gb)),
                new("Rejected", docs.Count(d => d.Status == EmployeeDocumentStatus.Rejected).ToString(Gb))
            ],
            Table = new ReportTable([new("Onboarding"), new("New employee"), new("Department"), new("Document"), new("Status"), new("Submitted"),
                new("Verified by"), new("Verified on"), new("Reason")], rows)
        };
    }

    private async Task<ReportResult> BeneficiariesAsync(ReportFilter f, Func<int, bool> visible, Func<int, string> dept, CancellationToken ct)
    {
        var obs = (await onboardings.ListAsync(o => f.DepartmentId == null || o.DepartmentId == f.DepartmentId, ct)).Where(o => visible(o.DepartmentId) && InPeriod(f, o.StartDate)).ToDictionary(o => o.Id);
        var records = (await personalRecords.ListWithBeneficiariesAsync(ct)).Where(r => obs.ContainsKey(r.OnboardingRecordId)).ToList();
        var rows = records.OrderBy(r => obs[r.OnboardingRecordId].EmployeeName).SelectMany(r => r.Beneficiaries.OrderBy(b => b.Id).Select(b =>
        {
            var o = obs[r.OnboardingRecordId];
            return new ReportRow([o.EmployeeName, o.EmployeeNumber, dept(o.DepartmentId), b.Name, b.Relationship, b.DateOfBirth, b.Percentage,
                r.DeclaredAt is null ? "Not signed" : "Signed"], new ReportLink("Onboarding", "PersonalRecord", o.Id));
        })).ToList();
        return new ReportResult
        {
            Key = ReportKeys.Beneficiaries,
            PeriodAppliesTo = "the employee's start date",
            Kpis =
            [
                new("Employees", records.Count.ToString(Gb)), new("Beneficiaries", rows.Count.ToString(Gb)),
                new("Records not totalling 100%", records.Count(r => r.Beneficiaries.Count > 0 && r.Beneficiaries.Sum(b => b.Percentage) != 100m).ToString(Gb),
                    "Should be 0: the form refuses anything else.")
            ],
            Table = new ReportTable([new("Employee"), new("Staff no."), new("Department"), new("Beneficiary"), new("Relationship"), new("Date of birth"),
                new("Share (%)", true), new("Declaration")], rows)
        };
    }

    // ===================================================================== Probation

    private async Task<ReportResult> ProbationAsync(ReportFilter f, Func<int, bool> visible, Func<int, string> dept, CancellationToken ct)
    {
        var list = (await probations.ListAsync(p => f.DepartmentId == null || p.DepartmentId == f.DepartmentId, ct)).Where(p => visible(p.DepartmentId) && InPeriod(f, p.StartDate))
            .Where(p => f.Status is null || p.Status.ToString() == f.Status).OrderBy(p => p.EndDate).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var byProbation = (await assessments.ListAsync(a => ids.Contains(a.ProbationRecordId), ct)).GroupBy(a => a.ProbationRecordId).ToDictionary(g => g.Key, g => g.ToList());
        var rows = list.Select(p =>
        {
            var a = byProbation.GetValueOrDefault(p.Id) ?? [];
            var first = a.FirstOrDefault(x => x.Kind == ProbationAssessmentKind.First);
            var final = a.Where(x => x.Kind == ProbationAssessmentKind.Final).OrderBy(x => x.AssessedAt).LastOrDefault();
            var (step, due) = ProbationService.NextStep(p, first is not null);
            return new ReportRow([p.Number, p.EmployeeName, dept(p.DepartmentId), p.StartDate, p.EndDate, EnumText.Label(p.Status), p.InitialMeetingDate,
                first?.AssessedAt, final?.Outcome is { } o ? EnumText.Label(o) : null, step, due, due is { } d && d < Today ? "Overdue" : null],
                new ReportLink("Probation", "Details", p.Id));
        }).ToList();
        return new ReportResult
        {
            Key = ReportKeys.Probation,
            PeriodAppliesTo = "the probation start date",
            StatusOptions = Enum.GetValues<ProbationStatus>().Select(s => new LookupItem(s.ToString(), EnumText.Label(s))).ToList(),
            Kpis =
            [
                new("Probations", list.Count.ToString(Gb)),
                new("In progress", list.Count(p => ProbationWorkflow.IsActive(p.Status)).ToString(Gb)),
                new("Reviews overdue", rows.Count(r => r.Cells[11] is "Overdue").ToString(Gb)),
                new("Confirmed", list.Count(p => p.Status == ProbationStatus.Confirmed).ToString(Gb)),
                new("Not confirmed", list.Count(p => p.Status == ProbationStatus.NotConfirmed).ToString(Gb))
            ],
            Table = new ReportTable([new("Probation"), new("Employee"), new("Department"), new("Start"), new("End"), new("Status"), new("Initial meeting"),
                new("First assessment"), new("Final recommendation"), new("Next step"), new("Due"), new("")], rows)
        };
    }

    private async Task<ReportResult> ExtensionsAsync(ReportFilter f, Func<int, bool> visible, Func<int, string> dept, CancellationToken ct)
    {
        var list = (await probations.ListAsync(p => p.ExtendedMonths > 0, ct)).Where(p => visible(p.DepartmentId) && InPeriod(f, p.StartDate))
            .OrderBy(p => p.EndDate).ToList();
        var rows = list.Select(p => new ReportRow([p.Number, p.EmployeeName, dept(p.DepartmentId), p.OriginalEndDate, p.EndDate, p.ExtendedMonths,
            EnumText.Label(p.Status), p.ExtensionReason, p.ExtensionImprovementPlan], new ReportLink("Probation", "Details", p.Id))).ToList();
        return new ReportResult
        {
            Key = ReportKeys.ProbationExtensions,
            PeriodAppliesTo = "the probation start date",
            Kpis = [new("Extended probations", list.Count.ToString(Gb)), new("Still extended", list.Count(p => p.Status == ProbationStatus.Extended).ToString(Gb))],
            Table = new ReportTable([new("Probation"), new("Employee"), new("Department"), new("Original end"), new("New end"), new("Months", true),
                new("Status"), new("Reason"), new("Improvement plan")], rows)
        };
    }

    private async Task<ReportResult> ConfirmationsAsync(ReportFilter f, Func<int, bool> visible, Func<int, string> dept, CancellationToken ct)
    {
        var probs = (await probations.ListAsync(p => f.DepartmentId == null || p.DepartmentId == f.DepartmentId, ct)).Where(p => visible(p.DepartmentId)).ToDictionary(p => p.Id);
        var list = (await confirmations.ListAsync(ct: ct)).Where(c => probs.ContainsKey(c.ProbationRecordId) && InPeriod(f, c.ConfirmationDate))
            .OrderByDescending(c => c.ConfirmationDate).ToList();
        var names = await users.GetDisplayNamesAsync(list.Select(c => c.ApprovedByUserId).Distinct(), ct);
        var rows = list.Select(c =>
        {
            var p = probs[c.ProbationRecordId];
            return new ReportRow([c.LetterNumber, p.EmployeeName, p.StaffNumber, dept(p.DepartmentId), p.JobTitle, p.StartDate, c.ConfirmationDate,
                names.GetValueOrDefault(c.ApprovedByUserId), p.ExtendedMonths], new ReportLink("Probation", "Details", p.Id));
        }).ToList();
        return new ReportResult
        {
            Key = ReportKeys.Confirmations,
            PeriodAppliesTo = "the confirmation date",
            Kpis = [new("Confirmations", list.Count.ToString(Gb)), new("After an extension", rows.Count(r => (int)r.Cells[8]! > 0).ToString(Gb)),
                    new("Awaiting HR decision", probs.Values.Count(p => p.Status == ProbationStatus.AwaitingHrDecision).ToString(Gb))],
            Table = new ReportTable([new("Letter"), new("Employee"), new("Staff no."), new("Department"), new("Position"), new("Started"), new("Confirmed"),
                new("Approved by"), new("Months extended", true)], rows)
        };
    }

    private static bool InPeriod(ReportFilter f, DateOnly? date) =>
        (f.From is null || date >= f.From) && (f.To is null || date <= f.To) || (f.From is null && f.To is null);

    // Phase 27: document names come from the configurable types.
    private async Task<IReadOnlyDictionary<string, OnboardingDocumentType>> TypesAsync(CancellationToken ct) =>
        (await documentTypes.ListAsync(ct: ct)).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

    private static string TypeName(IReadOnlyDictionary<string, OnboardingDocumentType> types, EmployeeDocument d) =>
        types.GetValueOrDefault(d.DocumentTypeCode)?.Name ?? d.DocumentTypeCode;
}
