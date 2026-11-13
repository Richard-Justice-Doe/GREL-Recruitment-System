using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Documents;
using Grsms.Domain.Onboarding;

namespace Grsms.Application.Onboarding;

/// <summary>One automatic remark on a new-hire document. <see cref="Severity"/>: "danger", "warning" or "info".</summary>
/// <param name="StaffOnly">Mentions another person's upload: never shown to the new employee.</param>
public sealed record DocumentFlag(string Code, string Severity, string Message, bool StaffOnly = false);

/// <summary>
/// 13 November 2026 (docs/36 step AI-4): automatic checks on new-hire documents, by rules only (no AI, nothing leaves GREL):
///   * expired / expiring within 90 days / no "valid until" date, for ID-type documents (Ghana Card, passport, permits);
///   * the same file (same SHA-256) used for two documents, or uploaded by another new employee;
///   * photos and scans of low resolution (longer side under 1,000 pixels, read from the file header).
/// Missing documents and deadlines are already followed by the checklist and Phase 27 stage C. Flags only inform the Recruitment
/// Officer, who still verifies or rejects each document.
/// </summary>
public class DocumentCheckService(IRepository<EmployeeDocument> documents, IRepository<Attachment> attachments, IRepository<OnboardingRecord> onboardings,
    IRepository<OnboardingDocumentType> documentTypes)
{
    /// <summary>Flags per document id, for the given onboardings.</summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<DocumentFlag>>> ForOnboardingsAsync(IReadOnlyCollection<int> onboardingIds, DateOnly today,
        CancellationToken ct = default)
    {
        var docs = (await documents.ListAsync(d => onboardingIds.Contains(d.OnboardingRecordId) && d.IsApplicable, ct)).ToList();
        var docIds = docs.Select(d => d.Id).ToList();
        var files = docIds.Count == 0 ? [] : await attachments.ListAsync(a => a.OwnerType == AttachmentOwners.EmployeeDocument && docIds.Contains(a.OwnerId), ct);

        // Same file elsewhere: any current new-hire document file with the same fingerprint.
        var hashes = files.Select(f => f.Sha256).Where(h => h.Length > 0).Distinct().ToList();
        var twins = hashes.Count == 0 ? [] : await attachments.ListAsync(a => a.OwnerType == AttachmentOwners.EmployeeDocument && hashes.Contains(a.Sha256), ct);
        var twinDocIds = twins.Select(t => t.OwnerId).Distinct().ToList();
        var twinDocs = (await documents.ListAsync(d => twinDocIds.Contains(d.Id), ct)).ToDictionary(d => d.Id);
        var otherObIds = twinDocs.Values.Select(d => d.OnboardingRecordId).Distinct().ToList();
        var obNumbers = (await onboardings.ListAsync(o => otherObIds.Contains(o.Id), ct)).ToDictionary(o => o.Id, o => o.Number);
        var types = (await documentTypes.ListAsync(ct: ct)).GroupBy(t => t.Code).ToDictionary(g => g.Key, g => g.First().Name);

        var result = new Dictionary<int, IReadOnlyList<DocumentFlag>>();
        foreach (var d in docs)
        {
            var flags = new List<DocumentFlag>();
            var mine = files.Where(f => f.OwnerId == d.Id).ToList();

            if (OnboardingWorkflow.HasExpiry(d.DocumentTypeCode))
            {
                if (d.ExpiresOn is { } exp)
                {
                    if (exp < today) flags.Add(new("expired", "danger", $"Expired on {exp:d MMM yyyy}."));
                    else if (exp <= today.AddDays(OnboardingWorkflow.ExpiresSoonDays)) flags.Add(new("expires-soon", "warning", $"Expires soon: {exp:d MMM yyyy}."));
                }
                else if (mine.Count > 0) flags.Add(new("no-expiry", "info", "\"Valid until\" date not given."));
            }

            foreach (var f in mine)
            {
                foreach (var twin in twins.Where(t => t.Sha256 == f.Sha256 && t.Id != f.Id && t.OwnerId != d.Id))
                {
                    if (!twinDocs.TryGetValue(twin.OwnerId, out var other)) continue;
                    if (other.OnboardingRecordId == d.OnboardingRecordId)
                        flags.Add(new("duplicate", "warning", $"The same file is also uploaded as \"{types.GetValueOrDefault(other.DocumentTypeCode, other.DocumentTypeCode)}\"."));
                    else
                        flags.Add(new("duplicate-other", "danger",
                            $"The same file was uploaded by another new employee (onboarding {obNumbers.GetValueOrDefault(other.OnboardingRecordId, "?")}).", StaffOnly: true));
                }
                if (f.ImageWidth is { } w && f.ImageHeight is { } h && Math.Max(w, h) < OnboardingWorkflow.LowResolutionPixels)
                    flags.Add(new("low-resolution", "warning", $"{f.OriginalFileName}: low resolution ({w} × {h} pixels), may be hard to read."));
            }
            if (flags.Count > 0) result[d.Id] = flags.DistinctBy(x => x.Message).ToList();
        }
        return result;
    }
}
