using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Domain.Documents;
using Grsms.Domain.Employees;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;

namespace Grsms.Application.Employees;

/// <summary>
/// 17 November 2026 (employee directory, E1): employee photos.
///   * HR (employees.manage) uploads or replaces a photo (JPEG or PNG, at most 3 MB, the usual upload checks); earlier photos stay as
///     history; removing it clears the link. Since 22 Nov 2026 each employee does the same for their own photo ("My photo").
///   * With no photo uploaded, the <b>passport-size photograph</b> the new employee uploaded during onboarding is used once the
///     Recruitment Officer has verified it.
///   * Photos are shown to whoever may open the employee's record (same visibility as <see cref="EmployeeService"/>) and to the
///     employee themselves. Everyone else gets initials.
/// </summary>
public class EmployeePhotoService(
    IRepository<Employee> employees,
    IRepository<EmployeeDocument> documents,
    IRepository<Attachment> attachments,
    AttachmentService files,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope)
{
    public const long MaxBytes = 3 * 1024 * 1024;
    public static readonly IReadOnlyList<string> Types = [".jpg", ".jpeg", ".png"];
    public const string OnboardingPhotoCode = "PassportPicture";

    private static bool IsImage(string fileName) => Types.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    private async Task<bool> MaySeeAsync(Employee e, CancellationToken ct)
    {
        if (e.UserId == me.UserId) return true;
        if (!me.HasPermission(Permissions.ViewEmployees)) return false;
        var v = await scope.VisibilityAsync(ct);
        return v.SeeAll || v.HodDepartmentIds.Contains(e.DepartmentId);
    }

    /// <summary>22 Nov 2026: HR changes anyone's photo; an employee changes their own.</summary>
    private bool MayChange(Employee e) => me.HasPermission(Permissions.ManageEmployees) || (me.UserId is not null && e.UserId == me.UserId);

    /// <summary>22 Nov 2026: the signed-in employee's record (current staff only), for "My photo"; null when they have none.</summary>
    public async Task<Employee?> MineAsync(CancellationToken ct = default)
    {
        if (me.UserId is null || me.IsInRole(Roles.Candidate)) return null;
        var found = (await employees.ListAsync(x => x.UserId == me.UserId && x.Status != EmployeeStatus.Separated, ct)).FirstOrDefault();
        return found is null ? null : await employees.GetByIdAsync(found.Id, ct);
    }

    /// <summary>True when the employee has a photo to show (uploaded, or the verified onboarding photograph).</summary>
    public async Task<bool> HasPhotoAsync(Employee e, CancellationToken ct = default) =>
        (await WithPhotoAsync([(e.Id, e.PhotoAttachmentId, e.OnboardingRecordId)], ct)).Contains(e.Id);

    public async Task<Result> UploadAsync(int employeeId, string? fileName, Stream? content, CancellationToken ct = default)
    {
        var e = await employees.GetByIdAsync(employeeId, ct);
        if (e is null) return Result.Failure("Employee not found.");
        if (!MayChange(e)) return Result.Failure("You can only change your own photo.");
        if (content is null || string.IsNullOrWhiteSpace(fileName)) return Result.Failure("Choose a photo (JPEG or PNG).");
        if (!IsImage(fileName)) return Result.Failure("The photo must be a JPEG or PNG picture.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxBytes) return Result.Failure("The photo is larger than 3 MB. Use a smaller picture.");
        buffer.Position = 0;
        var saved = await files.AddAsync(AttachmentOwners.EmployeePhoto, e.Id, AttachmentCategory.EmployeePhoto, fileName, buffer, ct, Types);
        if (!saved.Succeeded) return Result.Failure([.. saved.Errors]);
        e.PhotoAttachmentId = saved.Value;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveAsync(int employeeId, CancellationToken ct = default)
    {
        var e = await employees.GetByIdAsync(employeeId, ct);
        if (e is null) return Result.Failure("Employee not found.");
        if (!MayChange(e)) return Result.Failure("You can only change your own photo.");
        e.PhotoAttachmentId = null; // the file stays as history
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>The photo to show, or null (then initials are shown).</summary>
    public async Task<FileDownload?> OpenAsync(int employeeId, CancellationToken ct = default)
    {
        var e = await employees.GetByIdAsync(employeeId, ct);
        if (e is null || !await MaySeeAsync(e, ct)) return null;
        return await PhotoOfAsync(e, ct);
    }

    /// <summary>
    /// 17 Nov 2026: the photo on a birthday or anniversary card, which every member of staff sees: current employees who have not
    /// opted out of celebrations only. The caller checks the viewer is staff.
    /// </summary>
    public async Task<FileDownload?> OpenForCelebrationAsync(int employeeId, CancellationToken ct = default)
    {
        var e = await employees.GetByIdAsync(employeeId, ct);
        if (e is null || e.Status == EmployeeStatus.Separated || e.CelebrationsOptOut || me.UserId is null || me.IsInRole(Roles.Candidate)) return null;
        return await PhotoOfAsync(e, ct);
    }

    private async Task<FileDownload?> PhotoOfAsync(Employee e, CancellationToken ct)
    {
        if (e.PhotoAttachmentId is { } pid) return await files.OpenAsync(AttachmentOwners.EmployeePhoto, e.Id, pid, ct);
        if (e.OnboardingRecordId is not { } oid) return null;
        var doc = (await documents.ListAsync(d => d.OnboardingRecordId == oid && d.DocumentTypeCode == OnboardingPhotoCode
                                                  && d.Status == EmployeeDocumentStatus.Verified, ct)).FirstOrDefault();
        if (doc is null) return null;
        var file = (await attachments.ListAsync(a => a.OwnerType == AttachmentOwners.EmployeeDocument && a.OwnerId == doc.Id, ct))
            .Where(a => IsImage(a.OriginalFileName)).OrderBy(a => a.Id).FirstOrDefault();
        return file is null ? null : await files.OpenAsync(AttachmentOwners.EmployeeDocument, doc.Id, file.Id, ct);
    }

    /// <summary>
    /// Which of these employees have a photo to show (an uploaded one, or a verified onboarding photograph), for the directory: at
    /// most two queries for a whole page.
    /// </summary>
    public async Task<IReadOnlySet<int>> WithPhotoAsync(IReadOnlyCollection<(int Id, int? PhotoAttachmentId, int? OnboardingRecordId)> people, CancellationToken ct = default)
    {
        var result = people.Where(p => p.PhotoAttachmentId is not null).Select(p => p.Id).ToHashSet();
        var byOnboarding = people.Where(p => p.PhotoAttachmentId is null && p.OnboardingRecordId is not null)
            .ToDictionary(p => p.OnboardingRecordId!.Value, p => p.Id);
        if (byOnboarding.Count == 0) return result;
        var ids = byOnboarding.Keys.ToList();
        var docs = await documents.ListAsync(d => ids.Contains(d.OnboardingRecordId) && d.DocumentTypeCode == OnboardingPhotoCode
                                                  && d.Status == EmployeeDocumentStatus.Verified, ct);
        if (docs.Count == 0) return result;
        var docIds = docs.Select(d => d.Id).ToList();
        var withImage = (await attachments.ListAsync(a => a.OwnerType == AttachmentOwners.EmployeeDocument && docIds.Contains(a.OwnerId), ct))
            .Where(a => IsImage(a.OriginalFileName)).Select(a => a.OwnerId).ToHashSet();
        foreach (var d in docs.Where(d => withImage.Contains(d.Id))) result.Add(byOnboarding[d.OnboardingRecordId]);
        return result;
    }

    /// <summary>"Kwame Asante" → "KA"; one word → its first two letters.</summary>
    public static string Initials(string name)
    {
        var words = name.Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetter(w[0])).ToList();
        return words.Count switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}"
        };
    }
}
