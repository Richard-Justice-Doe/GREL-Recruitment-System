using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Vacancies;
using Grsms.Domain.Documents;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Settings;

namespace Grsms.Application.Onboarding;

/// <summary>
/// Phase 27: works out where an onboarding stands before activation, and moves it there.
///
/// Before activation nobody clicks "Submit" or "Send to the HR Manager" any more: after every upload, verification, rejection,
/// letter signature, personal-record signature, correction or requirement change, <see cref="RefreshAsync"/> applies
/// <see cref="OnboardingWorkflow.StageFor"/>. Each move is written to the onboarding's history, and:
///   * → "Awaiting document verification": the RO(s) are emailed once that the new employee has provided everything;
///   * → "Ready for activation": the RO(s) are emailed, and the new employee is told that their requirements are complete.
/// </summary>
public class OnboardingProgressService(
    IRepository<OnboardingRecord> onboardings,
    IRepository<EmployeeDocument> documents,
    IRepository<OnboardingDocumentType> types,
    IRepository<Attachment> attachments,
    IRepository<EmployeePersonalRecord> personalRecords,
    IVacancyRepository vacancies,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    WorkflowMailer mailer,
    IAppLinks links)
{
    /// <summary>Everything needed to show an onboarding's documents and progress.</summary>
    public record Snapshot(
        OnboardingRecord Onboarding,
        IReadOnlyList<(EmployeeDocument Document, OnboardingDocumentType Type, int Files)> Documents,
        bool PersonalRecordSigned,
        OnboardingWorkflow.Readiness Readiness)
    {
        /// <summary>Progress in %: required documents verified, plus the letter and the declaration signed.</summary>
        public int Percent
        {
            get
            {
                var total = Readiness.RequiredTotal + 2;
                var done = Readiness.RequiredVerified + (Onboarding.LetterAcknowledged ? 1 : 0) + (PersonalRecordSigned ? 1 : 0);
                return OnboardingWorkflow.BeforeActivation(Onboarding.Status) ? done * 100 / total : 100;
            }
        }
    }

    /// <summary>The documents (in checklist order, with their type and current file count) and the readiness of one onboarding.</summary>
    public async Task<Snapshot> SnapshotAsync(OnboardingRecord o, CancellationToken ct = default)
    {
        var docs = await documents.ListAsync(d => d.OnboardingRecordId == o.Id, ct);
        var byCode = (await types.ListAsync(ct: ct)).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        var ids = docs.Select(d => d.Id).ToList();
        var counts = (await attachments.ListAsync(a => a.OwnerType == AttachmentOwners.EmployeeDocument && ids.Contains(a.OwnerId), ct))
            .GroupBy(a => a.OwnerId).ToDictionary(g => g.Key, g => g.Count());
        var list = docs
            .Select(d => (Document: d, Type: byCode.GetValueOrDefault(d.DocumentTypeCode) ?? Unknown(d.DocumentTypeCode), Files: counts.GetValueOrDefault(d.Id)))
            .OrderBy(x => x.Type.IsLegacy ? 0 : 1).ThenBy(x => x.Type.Group).ThenBy(x => x.Type.SortOrder).ThenBy(x => x.Type.Name)
            .ToList();
        var signed = (await personalRecords.ListAsync(r => r.OnboardingRecordId == o.Id, ct)).FirstOrDefault()?.DeclaredAt is not null;
        var readiness = OnboardingWorkflow.CheckReadiness(o,
            list.Select(x => new OnboardingWorkflow.DocumentState(x.Document, x.Type.Name, x.Files, x.Type.MinFiles)).ToList(), signed);
        return new Snapshot(o, list, signed, readiness);
    }

    /// <summary>The stage the facts call for, without moving it (used when a step records its own history line).</summary>
    public async Task<OnboardingStatus> TargetAsync(OnboardingRecord o, CancellationToken ct = default) =>
        OnboardingWorkflow.BeforeActivation(o.Status) ? OnboardingWorkflow.StageFor(o, (await SnapshotAsync(o, ct)).Readiness) : o.Status;

    /// <summary>Moves the onboarding to the stage its facts call for (only before activation). Returns the stage it is at.</summary>
    public async Task<OnboardingStatus> RefreshAsync(int onboardingId, CancellationToken ct = default)
    {
        var o = await onboardings.GetByIdAsync(onboardingId, ct);
        if (o is null) return OnboardingStatus.Cancelled;
        if (!OnboardingWorkflow.BeforeActivation(o.Status)) return o.Status;

        var snap = await SnapshotAsync(o, ct);
        var target = OnboardingWorkflow.StageFor(o, snap.Readiness);
        if (target == o.Status) return target;

        var from = o.Status;
        var action = target switch
        {
            OnboardingStatus.AwaitingActivation => OnboardingAction.DocumentsVerified,
            OnboardingStatus.AwaitingDocumentVerification when from is OnboardingStatus.AwaitingEmployeeSubmission or OnboardingStatus.Draft => OnboardingAction.Submitted,
            OnboardingStatus.AwaitingEmployeeSubmission when from != OnboardingStatus.Draft => OnboardingAction.ReturnedToEmployee,
            _ => OnboardingAction.LetterIssued
        };
        var comment = target == OnboardingStatus.AwaitingEmployeeSubmission && snap.Readiness.ForEmployee.Count > 0
            ? $"Outstanding: {string.Join("; ", snap.Readiness.ForEmployee)}" : null;
        if (comment?.Length > 1900) comment = comment[..1900] + "…";
        o.RecordStep(action, target, me.UserId ?? o.CreatedByUserId, me.UserId is null ? "GRSMS" : scope.UserName, comment, DateTime.UtcNow);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return from; // someone else moved it at the same moment; their refresh wins
        }

        if (target == OnboardingStatus.AwaitingActivation)
        {
            foreach (var uid in await HrOwnersAsync(o, ct))
                await mailer.ToUserAsync(uid, $"Onboarding {o.Number}: ready for activation",
                    $"Every required document of <b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}) is verified and the " +
                    "appointment letter and personal record are signed. The employee can now be activated with their staff number.",
                    links.Onboarding(o.Id), "Activate the employee", ct);
            await mailer.TemplateToUserAsync(o.EmployeeUserId, EmailTemplateCatalogue.OnboardingDocumentsComplete,
                new Dictionary<string, string?>(), links.MyApplication(o.JobApplicationId), ct: ct);
        }
        else if (action == OnboardingAction.Submitted)
            foreach (var uid in await HrOwnersAsync(o, ct))
                await mailer.ToUserAsync(uid, $"Onboarding {o.Number}: requirements submitted",
                    $"<b>{WorkflowMailer.Enc(o.EmployeeName)}</b> ({WorkflowMailer.Enc(o.JobTitle)}) has provided every required document and signed the " +
                    $"letter and the personal record. {snap.Readiness.AwaitingVerification} document(s) are waiting for verification.",
                    links.Onboarding(o.Id), "Verify the documents", ct);
        return target;
    }

    /// <summary>The Recruitment Officer(s) to tell about an onboarding: the vacancy's recruiter and whoever started it.</summary>
    public async Task<IReadOnlyList<string>> HrOwnersAsync(OnboardingRecord o, CancellationToken ct = default) =>
        new[] { (await vacancies.GetByIdAsync(o.VacancyId, ct))?.RecruiterUserId, o.CreatedByUserId }
            .OfType<string>().Distinct().Where(id => id != o.EmployeeUserId && id != me.UserId).ToList();

    /// <summary>A document whose type was removed from the database (should not happen): shown under its code.</summary>
    private static OnboardingDocumentType Unknown(string code) =>
        new() { Code = code, Name = code, Group = DocumentGroup.Personal, Requirement = DocumentRequirement.Optional, IsActive = false, SortOrder = int.MaxValue };
}
