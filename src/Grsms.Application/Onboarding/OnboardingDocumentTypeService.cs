using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Documents;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;

namespace Grsms.Application.Onboarding;

/// <summary>One row of the Onboarding documents screen (and the form to add or change a type).</summary>
public class OnboardingDocumentTypeForm
{
    public int? Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DocumentGroup Group { get; set; }
    public DocumentRequirement Requirement { get; set; }
    public bool AllowsWordFiles { get; set; }
    public int MinFiles { get; set; } = 1;
    public int PersonnelFileCategoryId { get; set; } = PersonnelFileCategories.EmployeeRecords;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLegacy { get; set; }
    public Guid Version { get; set; }

    /// <summary>Onboardings in progress that have this document (shown on the screen).</summary>
    public int InUse { get; set; }
}

/// <summary>
/// Phase 27: the configurable onboarding document checklist (<see cref="OnboardingDocumentType"/>).
///
/// Changing it needs "Maintain organisation data" (HR Administrator, Admin, Super Admin). A change applies AT ONCE to every
/// onboarding not yet activated (user's choice, 10 November 2026): a new or re-activated type is added to their checklists as
/// "Not submitted"; each document's requirement is re-applied (<see cref="OnboardingWorkflow.ApplyRequirement"/>); a retired type
/// stops counting, but files already uploaded stay. Legacy types (the old "School certificates") cannot be changed.
/// </summary>
public class OnboardingDocumentTypeService(
    IRepository<OnboardingDocumentType> types,
    IRepository<OnboardingRecord> onboardings,
    IRepository<EmployeeDocument> documents,
    IRepository<Attachment> attachments,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    OnboardingProgressService progress)
{
    private const string ConflictMessage = "Someone else changed this document type at the same moment. Please reload the page and try again.";

    public bool CanManage => me.HasPermission(Permissions.ManageMasterData);

    /// <summary>Every type (also retired and legacy ones) by code, for names and rules.</summary>
    public async Task<IReadOnlyDictionary<string, OnboardingDocumentType>> AllByCodeAsync(CancellationToken ct = default) =>
        (await types.ListAsync(ct: ct)).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>The settings screen, in checklist order.</summary>
    public async Task<IReadOnlyList<OnboardingDocumentTypeForm>> GetAllAsync(CancellationToken ct = default)
    {
        var running = (await onboardings.ListAsync(ct: ct)).Where(o => OnboardingWorkflow.BeforeActivation(o.Status)).Select(o => o.Id).ToList();
        var inUse = (await documents.ListAsync(d => running.Contains(d.OnboardingRecordId) && d.IsApplicable, ct))
            .GroupBy(d => d.DocumentTypeCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return (await types.ListAsync(ct: ct)).OrderBy(t => t.IsLegacy).ThenBy(t => t.Group).ThenBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => ToForm(t, inUse.GetValueOrDefault(t.Code))).ToList();
    }

    public async Task<OnboardingDocumentTypeForm?> GetAsync(int id, CancellationToken ct = default) =>
        await types.GetByIdAsync(id, ct) is { } t ? ToForm(t, 0) : null;

    /// <summary>Adds (Id null) or changes a type, then applies the change to the onboardings in progress.</summary>
    public async Task<Result<int>> SaveAsync(OnboardingDocumentTypeForm form, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure<int>("Only an HR Administrator, Admin or Super Admin can change the onboarding documents.");
        var name = Clean(form.Name);
        var description = Clean(form.Description);
        if (name is null) return Result.Failure<int>("Give the document a name.");
        if (name.Length > 150) return Result.Failure<int>("Keep the name under 150 characters.");
        if (description?.Length > 500) return Result.Failure<int>("Keep the description under 500 characters.");
        if (!Enum.IsDefined(form.Group) || !Enum.IsDefined(form.Requirement)) return Result.Failure<int>("Choose the group and when the document is required.");
        if (form.MinFiles is < 1 or > OnboardingWorkflow.MaxFilesPerDocument)
            return Result.Failure<int>($"The number of files needed must be between 1 and {OnboardingWorkflow.MaxFilesPerDocument}.");
        if (PersonnelFileCategories.All.All(c => c.Id != form.PersonnelFileCategoryId)) return Result.Failure<int>("Choose where the document is filed.");

        OnboardingDocumentType t;
        if (form.Id is int id)
        {
            var existing = await types.GetByIdAsync(id, ct);
            if (existing is null) return Result.Failure<int>("Document type not found.");
            if (existing.IsLegacy) return Result.Failure<int>("This document belongs to the old checklist and can no longer be changed.");
            if (existing.Version != form.Version) return Result.Failure<int>(ConflictMessage);
            t = existing;
        }
        else
        {
            var code = Code(form.Code ?? name);
            if (code.Length < 2) return Result.Failure<int>("Give the document a name of at least two letters.");
            if (await types.AnyAsync(x => x.Code == code, ct)) return Result.Failure<int>($"There is already a document type with the code \"{code}\". Choose another name.");
            t = new OnboardingDocumentType { Code = code };
            types.Add(t);
        }
        if (await types.AnyAsync(x => x.Name == name && x.Id != t.Id && !x.IsLegacy, ct)) return Result.Failure<int>($"There is already a document called \"{name}\".");

        t.Name = name;
        t.Description = description;
        t.Group = form.Group;
        t.Requirement = form.Requirement;
        t.AllowsWordFiles = form.AllowsWordFiles;
        t.MinFiles = form.MinFiles;
        t.PersonnelFileCategoryId = form.PersonnelFileCategoryId;
        t.SortOrder = form.SortOrder;
        t.IsActive = form.IsActive;
        t.Version = Guid.NewGuid();
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<int>(ConflictMessage);
        }

        await ApplyToRunningAsync(t, ct);
        return Result.Success(t.Id);
    }

    /// <summary>Adds the type to (or re-applies it on) every onboarding not yet activated, then refreshes their stage.</summary>
    private async Task ApplyToRunningAsync(OnboardingDocumentType t, CancellationToken ct)
    {
        var running = (await onboardings.ListAsync(ct: ct)).Where(o => OnboardingWorkflow.BeforeActivation(o.Status)).ToList();
        if (running.Count == 0) return;
        var ids = running.Select(o => o.Id).ToList();
        var docs = (await documents.ListAsync(d => ids.Contains(d.OnboardingRecordId) && d.DocumentTypeCode == t.Code, ct)).ToDictionary(d => d.OnboardingRecordId);
        var docIds = docs.Values.Select(d => d.Id).ToList();
        var withFiles = (await attachments.ListAsync(a => a.OwnerType == AttachmentOwners.EmployeeDocument && docIds.Contains(a.OwnerId), ct))
            .Select(a => a.OwnerId).ToHashSet();

        var changed = new List<OnboardingRecord>();
        foreach (var o in running)
        {
            if (!docs.TryGetValue(o.Id, out var d))
            {
                if (!t.IsActive) continue; // a retired type is never added
                d = new EmployeeDocument { OnboardingRecordId = o.Id, EmployeeUserId = o.EmployeeUserId, DocumentTypeCode = t.Code };
                documents.Add(d);
            }
            var before = (d.IsRequired, d.IsApplicable);
            OnboardingWorkflow.ApplyRequirement(t, o, d, d.Id != 0 && withFiles.Contains(d.Id));
            if (d.Id == 0 || before != (d.IsRequired, d.IsApplicable))
            {
                d.Version = Guid.NewGuid();
                changed.Add(o);
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var o in changed) await progress.RefreshAsync(o.Id, ct);
    }

    private static OnboardingDocumentTypeForm ToForm(OnboardingDocumentType t, int inUse) => new()
    {
        Id = t.Id, Code = t.Code, Name = t.Name, Description = t.Description, Group = t.Group, Requirement = t.Requirement,
        AllowsWordFiles = t.AllowsWordFiles, MinFiles = t.MinFiles, PersonnelFileCategoryId = t.PersonnelFileCategoryId, SortOrder = t.SortOrder,
        IsActive = t.IsActive, IsLegacy = t.IsLegacy, Version = t.Version, InUse = inUse
    };

    /// <summary>"Driver's licence (Class C)" → "DriversLicenceClassC".</summary>
    private static string Code(string text)
    {
        var words = new string(text.Replace("'", "").Replace("’", "").Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        var code = string.Concat(words);
        return code.Length > 40 ? code[..40] : code;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
