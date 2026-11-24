using Grsms.Application.Common.Interfaces;
using Grsms.Application.Onboarding;
using Grsms.Domain.Documents;
using Grsms.Domain.Employees;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Probation;

namespace Grsms.Application.Employees;

/// <summary>What fixes a missing part of a profile (the page turns it into a link or button).</summary>
public enum CompletionFix { None, UpdateJob, Photo, Onboarding, PersonnelFile }

/// <summary>One part of the profile: done or not, and what is missing.</summary>
public sealed record CompletionItem(string Section, bool Done, string? Missing, CompletionFix Fix = CompletionFix.None);

/// <summary>An employee's profile completion (e.g. 85 %), with the parts still missing.</summary>
public sealed record ProfileCompletion(IReadOnlyList<CompletionItem> Items)
{
    public int Percent => Items.Count == 0 ? 0 : (int)Math.Round(100.0 * Items.Count(i => i.Done) / Items.Count);
    public IReadOnlyList<CompletionItem> Missing => Items.Where(i => !i.Done).ToList();
}

/// <summary>A "pending action" shown on the profile. <see cref="Severity"/>: danger, warning or info.</summary>
public sealed record EmployeeAlert(string Severity, string Text, CompletionFix Fix = CompletionFix.None);

/// <summary>What the completion needs to know about one employee.</summary>
public sealed record CompletionInput(int Id, string UserId, int? OnboardingRecordId, int? GradeId, string? ReportsToUserId, int DepartmentId, bool HasPhoto);

/// <summary>
/// 17 November 2026 (employee directory, E2): the profile completion of each employee and the profile's pending actions, worked
/// out from what GRSMS already holds:
///   1 Personal information (personal record: date of birth, address, SSNIT number, mobile, declaration signed) ·
///   2 Emergency contact · 3 Bank information · 4 Education (highest qualification) ·
///   5 Documents (onboarding documents verified; staff hired before GRSMS: at least one document in the personnel file) ·
///   6 Employment details (grade and line manager or Head of Department) · 7 Photo.
/// Staff registered directly (no onboarding) have no personal record until self-service (docs/30 Phase 29), so those parts show as
/// missing. Only presence is checked: no personal data leaves the services that own it.
/// </summary>
public class ProfileCompletionService(
    IPersonalRecordRepository personalRecords,
    IRepository<EmployeeDocument> documents,
    IRepository<OnboardingDocumentType> documentTypes,
    IRepository<PersonnelFile> personnelFiles,
    IRepository<Attachment> attachments,
    IRepository<Department> departments,
    IRepository<ProbationRecord> probations,
    IRepository<OnboardingTask> tasks)
{
    /// <summary>The completion for many employees at once (a directory page): six queries whatever the page size.</summary>
    public async Task<IReadOnlyDictionary<int, ProfileCompletion>> ForAsync(IReadOnlyCollection<CompletionInput> people, CancellationToken ct = default)
    {
        if (people.Count == 0) return new Dictionary<int, ProfileCompletion>();
        var obIds = people.Select(p => p.OnboardingRecordId).OfType<int>().Distinct().ToList();
        var records = (await personalRecords.SummariesAsync(obIds, ct)).ToDictionary(r => r.OnboardingRecordId);
        var docs = obIds.Count == 0 ? [] : await documents.ListAsync(d => obIds.Contains(d.OnboardingRecordId) && d.IsApplicable && d.IsRequired, ct);
        var typeNames = docs.Count == 0 ? new Dictionary<string, string>()
            : (await documentTypes.ListAsync(ct: ct)).GroupBy(t => t.Code).ToDictionary(g => g.Key, g => g.First().Name);
        var noOnboarding = people.Where(p => p.OnboardingRecordId is null).Select(p => p.UserId).ToList();
        var files = noOnboarding.Count == 0 ? [] : await personnelFiles.ListAsync(f => noOnboarding.Contains(f.EmployeeUserId), ct);
        var fileIds = files.Select(f => f.Id).ToList();
        var filed = fileIds.Count == 0 ? new HashSet<int>()
            : (await attachments.ListAsync(a => a.OwnerType == AttachmentOwners.PersonnelFile && fileIds.Contains(a.OwnerId), ct)).Select(a => a.OwnerId).ToHashSet();
        var hods = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.HeadOfDepartmentUserId);

        var result = new Dictionary<int, ProfileCompletion>();
        foreach (var p in people)
        {
            var items = new List<CompletionItem>();
            var rec = p.OnboardingRecordId is { } ob ? records.GetValueOrDefault(ob) : null;
            var noRecord = p.OnboardingRecordId is null ? "Not in GRSMS yet (hired before GRSMS; self-service will collect it)" : "Personal record not filled in";

            if (rec is null) items.Add(new("Personal information", false, noRecord, CompletionFix.Onboarding));
            else
            {
                var gaps = new[] { rec.HasDateOfBirth ? null : "date of birth", rec.HasAddress ? null : "address", rec.HasSsnit ? null : "SSNIT number",
                    rec.HasMobile ? null : "mobile number", rec.Declared ? null : "declaration not signed" }.OfType<string>().ToList();
                items.Add(new("Personal information", gaps.Count == 0, gaps.Count == 0 ? null : string.Join(", ", gaps), CompletionFix.Onboarding));
            }
            items.Add(new("Emergency contact", rec?.EmergencyContacts > 0, rec is null ? noRecord : "No emergency contact", CompletionFix.Onboarding));
            items.Add(new("Bank information", rec?.BankAccounts > 0, rec is null ? noRecord : "No bank account", CompletionFix.Onboarding));
            items.Add(new("Education", rec?.HasQualification == true, rec is null ? noRecord : "Highest qualification not given", CompletionFix.Onboarding));

            if (p.OnboardingRecordId is { } o)
            {
                var pending = docs.Where(d => d.OnboardingRecordId == o && d.Status != EmployeeDocumentStatus.Verified).ToList();
                items.Add(new("Documents", pending.Count == 0, pending.Count == 0 ? null
                    : $"{pending.Count} not verified: {string.Join(", ", pending.Take(3).Select(d => typeNames.GetValueOrDefault(d.DocumentTypeCode, d.DocumentTypeCode)))}{(pending.Count > 3 ? ", …" : "")}",
                    CompletionFix.Onboarding));
            }
            else
            {
                var file = files.FirstOrDefault(f => f.EmployeeUserId == p.UserId);
                items.Add(new("Documents", file is not null && filed.Contains(file.Id), file is null ? "No personnel file" : "No document in the personnel file",
                    CompletionFix.PersonnelFile));
            }

            var hasManager = p.ReportsToUserId is not null || hods.GetValueOrDefault(p.DepartmentId) is { } h && h != p.UserId;
            var jobGaps = new[] { p.GradeId is null ? "grade" : null, hasManager ? null : "line manager" }.OfType<string>().ToList();
            items.Add(new("Employment details", jobGaps.Count == 0, jobGaps.Count == 0 ? null : $"No {string.Join(" or ", jobGaps)}", CompletionFix.UpdateJob));
            items.Add(new("Photo", p.HasPhoto, "No photo", CompletionFix.Photo));
            result[p.Id] = new ProfileCompletion(items);
        }
        return result;
    }

    /// <summary>The profile's pending actions: what is missing, plus contract, probation and onboarding dates.</summary>
    public async Task<IReadOnlyList<EmployeeAlert>> AlertsAsync(Employee e, ProfileCompletion completion, DateOnly today, CancellationToken ct = default)
    {
        // Parts missing for the same reason (e.g. no personal record yet) make one line: "Emergency contact, Bank information: …".
        var alerts = completion.Missing.Where(m => m.Section != "Photo")
            .GroupBy(m => (m.Missing, m.Fix))
            .Select(g => new EmployeeAlert("warning", $"{string.Join(", ", g.Select(m => m.Section))}: {g.Key.Missing}", g.Key.Fix)).ToList();

        if (e.Status != EmployeeStatus.Separated && e.ContractEndDate is { } end)
        {
            if (end < today) alerts.Insert(0, new("danger", $"Contract ended on {end:d MMM yyyy}: update the job details with the renewal", CompletionFix.UpdateJob));
            else if (end <= today.AddDays(30)) alerts.Insert(0, new("danger", $"Contract expiring in {end.DayNumber - today.DayNumber} days ({end:d MMM yyyy})", CompletionFix.UpdateJob));
            else if (end <= today.AddDays(90)) alerts.Add(new("info", $"Contract ends on {end:d MMM yyyy}"));
        }

        var probation = (await probations.ListAsync(p => p.EmployeeUserId == e.UserId, ct)).OrderByDescending(p => p.Id).FirstOrDefault();
        if (probation is not null && probation.Status is not (ProbationStatus.Confirmed or ProbationStatus.NotConfirmed))
        {
            if (probation.EndDate < today) alerts.Insert(0, new("danger", $"Probation ended on {probation.EndDate:d MMM yyyy}: a decision is overdue"));
            else if (probation.EndDate <= today.AddDays(30)) alerts.Add(new("warning", $"Probation review due: the probation ends on {probation.EndDate:d MMM yyyy}"));
            if (probation.Status == ProbationStatus.InitialMeeting && probation.InitialMeetingDueDate < today)
                alerts.Add(new("warning", $"Probation initial meeting overdue (due {probation.InitialMeetingDueDate:d MMM yyyy})"));
        }

        if (e.OnboardingRecordId is { } ob)
        {
            var late = (await tasks.ListAsync(t => t.OnboardingRecordId == ob && t.Status == OnboardingTaskStatus.Open && t.DueDate < today, ct)).Count;
            if (late > 0) alerts.Add(new("warning", $"{late} onboarding task{(late == 1 ? "" : "s")} overdue", CompletionFix.Onboarding));
        }
        return alerts;
    }
}
