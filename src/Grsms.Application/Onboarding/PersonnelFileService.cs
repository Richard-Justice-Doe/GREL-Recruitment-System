using System.Globalization;
using System.Net;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Users;
using Grsms.Domain.Documents;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Organization;
using Grsms.Domain.Security;

namespace Grsms.Application.Onboarding;

public record PersonnelFileListItemDto(int Id, string FileNumber, string EmployeeName, string? StaffNumber, string JobTitle, string DepartmentName,
    DateTime OpenedAt, int Documents);

public record PersonnelFileDocumentDto(int Id, string Title, PersonnelDocumentKind Kind, string? FileName, long? SizeBytes, DateTime DocumentDate,
    string? AddedBy, string? SourceType, int? SourceId);

public record PersonnelFileFolderDto(int CategoryId, string Name, IReadOnlyList<PersonnelFileDocumentDto> Documents);

public record PersonnelFileDetailsDto(int Id, string FileNumber, string EmployeeName, string? StaffNumber, string JobTitle, string DepartmentName,
    DateTime OpenedAt, int OnboardingRecordId, IReadOnlyList<PersonnelFileFolderDto> Folders);

/// <summary>A letter filed in a personnel file, ready to show (the HTML is cleaned again before display).</summary>
public record FiledLetterDto(int PersonnelFileId, string FileNumber, string EmployeeName, string Title, string Html, DateTime DocumentDate);

/// <summary>
/// Phase 20: the electronic personnel file: a file per employee with seven categories ("folders"), all database records.
///
///   * Opened automatically when an onboarding is completed (<see cref="OpenForOnboardingAsync"/>), filing the verified
///     new-hire documents, the application's files, the signed Personal Record Declaration, the appointment letter and the
///     completed induction checklist. Running it again only adds what is not filed yet (nothing is ever filed twice).
///   * Later phases file their own records through <see cref="FileLetterAsync"/> (probation, confirmation letters).
///   * Files are never copied: a document points at the Attachments row (the stored path, name, type, size, SHA-256).
///   * Open to holders of "Open personnel files" (Recruitment Officers and HR Managers by default), who may also add files.
/// </summary>
public class PersonnelFileService(
    IRepository<PersonnelFile> files,
    IRepository<PersonnelFileDocument> documents,
    IRepository<OnboardingRecord> onboardings,
    IRepository<OnboardingTask> tasks,
    IRepository<EmployeeDocument> employeeDocuments,
    IRepository<EmployeeDeclaration> declarations,
    IRepository<Attachment> attachments,
    IRepository<Department> departments,
    IRepository<Site> sites,
    AttachmentService storage,
    IUserDirectory users,
    INumberGenerator numbers,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    IRichTextSanitizer sanitizer)
{
    private static readonly CultureInfo Gb = CultureInfo.GetCultureInfo("en-GB");
    public bool CanView => me.HasPermission(Permissions.ViewPersonnelFiles);

    // ===================================================================== Opening and filing

    /// <summary>
    /// Opens the employee's personnel file (or reuses it) and files everything from the onboarding that is not filed yet.
    /// Called by <see cref="OnboardingService.ActivateAsync"/>; not a user action, so no permission check here.
    /// </summary>
    public async Task<int> OpenForOnboardingAsync(OnboardingRecord o, CancellationToken ct = default)
    {
        var file = (await files.ListAsync(f => f.EmployeeUserId == o.EmployeeUserId, ct)).FirstOrDefault();
        var now = DateTime.UtcNow;
        if (file is null)
        {
            var n = await numbers.NextAsync($"PF-{now.Year}", ct);
            file = new PersonnelFile
            {
                FileNumber = $"PF-{now.Year}-{n:0000}", EmployeeUserId = o.EmployeeUserId, EmployeeName = o.EmployeeName,
                StaffNumber = o.EmployeeNumber, JobTitle = o.JobTitle, DepartmentId = o.DepartmentId, OnboardingRecordId = o.Id, OpenedAt = now
            };
            files.Add(file);
            await unitOfWork.SaveChangesAsync(ct);
        }
        else
        {
            // Rehired or moved: the file follows the latest position.
            (file.StaffNumber, file.JobTitle, file.DepartmentId) = (o.EmployeeNumber ?? file.StaffNumber, o.JobTitle, o.DepartmentId);
        }

        var filed = (await documents.ListAsync(d => d.PersonnelFileId == file.Id, ct))
            .Select(d => (d.SourceType, d.SourceId)).ToHashSet();
        void Add(int category, string title, PersonnelDocumentKind kind, DateTime date, string source, int sourceId, int? attachmentId = null, string? content = null)
        {
            if (!filed.Add((source, sourceId))) return;
            documents.Add(new PersonnelFileDocument
            {
                PersonnelFileId = file.Id, CategoryId = category, Title = title, Kind = kind, AttachmentId = attachmentId, Content = content,
                SourceType = source, SourceId = sourceId, DocumentDate = date
            });
        }

        // The new-hire documents (verified ones only), each file in its category.
        foreach (var d in (await employeeDocuments.ListAsync(x => x.OnboardingRecordId == o.Id && x.Status == EmployeeDocumentStatus.Verified, ct))
                     .OrderBy(x => x.DocumentType))
            foreach (var a in await storage.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct))
                Add(PersonnelFileCategories.For(d.DocumentType), $"{EnumText.Label(d.DocumentType)}: {a.FileName}", PersonnelDocumentKind.File,
                    a.UploadedAt, nameof(Attachment), a.Id, attachmentId: a.Id);

        // The application's own files (CV, certificates, cover letter as sent).
        foreach (var a in await storage.ListAsync(AttachmentOwners.JobApplication, o.JobApplicationId, ct))
            Add(PersonnelFileCategories.RecruitmentDocuments, $"Application: {EnumText.Label(a.Category)}: {a.FileName}", PersonnelDocumentKind.File,
                a.UploadedAt, nameof(Attachment), a.Id, attachmentId: a.Id);

        // The signed Personal Record Declaration (the latest signature).
        if ((await declarations.ListAsync(x => x.EmployeeUserId == o.EmployeeUserId, ct)).OrderByDescending(x => x.SignedAt).FirstOrDefault() is { } decl)
            Add(PersonnelFileCategories.EmployeeRecords, "Employee Personal Record Declaration (signed)", PersonnelDocumentKind.Record,
                decl.SignedAt, nameof(EmployeeDeclaration), decl.Id);

        // The signed appointment letter, frozen as it was signed.
        if (o.LetterAcknowledged)
            Add(PersonnelFileCategories.GeneratedLetters, $"Letter of appointment {o.LetterNumber}", PersonnelDocumentKind.Letter,
                o.LetterAcknowledgedAt!.Value, "AppointmentLetter", o.Id, content: await AppointmentLetterHtmlAsync(o, ct));

        // The completed induction checklist: who did and verified each task.
        Add(PersonnelFileCategories.OnboardingRecords, $"Onboarding and induction record {o.Number}", PersonnelDocumentKind.Letter,
            now, nameof(OnboardingRecord), o.Id, content: await InductionRecordHtmlAsync(o, ct));

        await unitOfWork.SaveChangesAsync(ct);
        return file.Id;
    }

    /// <summary>Files a generated letter (frozen HTML) in an employee's personnel file. For later phases (probation, confirmation).</summary>
    public async Task<Result<int>> FileLetterAsync(string employeeUserId, int categoryId, string title, string html, string sourceType, int sourceId,
        DateTime documentDate, CancellationToken ct = default)
    {
        var file = (await files.ListAsync(f => f.EmployeeUserId == employeeUserId, ct)).FirstOrDefault();
        if (file is null) return Result.Failure<int>("This employee has no personnel file yet.");
        if (await documents.AnyAsync(d => d.PersonnelFileId == file.Id && d.SourceType == sourceType && d.SourceId == sourceId, ct))
            return Result.Failure<int>("This document is already in the personnel file.");
        var doc = new PersonnelFileDocument
        {
            PersonnelFileId = file.Id, CategoryId = categoryId, Title = title, Kind = PersonnelDocumentKind.Letter, Content = sanitizer.Sanitize(html),
            SourceType = sourceType, SourceId = sourceId, DocumentDate = documentDate, AddedByUserId = me.UserId
        };
        documents.Add(doc);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(doc.Id);
    }

    // ===================================================================== Reading (HR)

    public async Task<IReadOnlyList<PersonnelFileListItemDto>> SearchAsync(string? search, int? departmentId, CancellationToken ct = default)
    {
        if (!CanView) return [];
        IEnumerable<PersonnelFile> q = await files.ListAsync(ct: ct);
        if (departmentId is { } dep) q = q.Where(f => f.DepartmentId == dep);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(f => f.EmployeeName.Contains(s, StringComparison.OrdinalIgnoreCase) || f.FileNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                             (f.StaffNumber ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        var list = q.ToList();
        var ids = list.Select(f => f.Id).ToList();
        var counts = (await documents.ListAsync(d => ids.Contains(d.PersonnelFileId), ct)).GroupBy(d => d.PersonnelFileId).ToDictionary(g => g.Key, g => g.Count());
        var deptNames = (await departments.ListAsync(ct: ct)).ToDictionary(d => d.Id, d => d.Name);
        return list.OrderBy(f => f.EmployeeName)
            .Select(f => new PersonnelFileListItemDto(f.Id, f.FileNumber, f.EmployeeName, f.StaffNumber, f.JobTitle, deptNames.GetValueOrDefault(f.DepartmentId, ""),
                f.OpenedAt, counts.GetValueOrDefault(f.Id)))
            .ToList();
    }

    public async Task<PersonnelFileDetailsDto?> GetAsync(int id, CancellationToken ct = default)
    {
        if (!CanView) return null;
        var f = await files.GetByIdAsync(id, ct);
        if (f is null) return null;
        var docs = await documents.ListAsync(d => d.PersonnelFileId == f.Id, ct);
        var attachmentIds = docs.Select(d => d.AttachmentId).OfType<int>().ToList();
        var atts = (await attachments.ListAsync(a => attachmentIds.Contains(a.Id), ct)).ToDictionary(a => a.Id);
        var names = await users.GetDisplayNamesAsync(docs.Select(d => d.AddedByUserId).OfType<string>().Distinct(), ct);
        PersonnelFileDocumentDto Dto(PersonnelFileDocument d)
        {
            var a = d.AttachmentId is int aid ? atts.GetValueOrDefault(aid) : null;
            return new PersonnelFileDocumentDto(d.Id, d.Title, d.Kind, a?.OriginalFileName, a?.SizeBytes, d.DocumentDate,
                d.AddedByUserId is null ? null : names.GetValueOrDefault(d.AddedByUserId), d.SourceType, d.SourceId);
        }
        return new PersonnelFileDetailsDto(f.Id, f.FileNumber, f.EmployeeName, f.StaffNumber, f.JobTitle,
            (await departments.GetByIdAsync(f.DepartmentId, ct))?.Name ?? "", f.OpenedAt, f.OnboardingRecordId,
            PersonnelFileCategories.All.OrderBy(c => c.SortOrder)
                .Select(c => new PersonnelFileFolderDto(c.Id, c.Name, docs.Where(d => d.CategoryId == c.Id).OrderBy(d => d.DocumentDate).Select(Dto).ToList()))
                .ToList());
    }

    /// <summary>The personnel file of an employee, if they have one (for links).</summary>
    public async Task<int?> IdForEmployeeAsync(string employeeUserId, CancellationToken ct = default) =>
        (await files.ListAsync(f => f.EmployeeUserId == employeeUserId, ct)).FirstOrDefault()?.Id;

    /// <summary>A filed letter, cleaned again before display.</summary>
    public async Task<FiledLetterDto?> GetLetterAsync(int documentId, CancellationToken ct = default)
    {
        if (!CanView) return null;
        var d = await documents.GetByIdAsync(documentId, ct);
        if (d is null || d.Kind != PersonnelDocumentKind.Letter) return null;
        var f = (await files.GetByIdAsync(d.PersonnelFileId, ct))!;
        return new FiledLetterDto(f.Id, f.FileNumber, f.EmployeeName, d.Title, sanitizer.Sanitize(d.Content), d.DocumentDate);
    }

    /// <summary>Opens a filed file for download (HR only).</summary>
    public async Task<FileDownload?> OpenAsync(int documentId, CancellationToken ct = default)
    {
        if (!CanView) return null;
        var d = await documents.GetByIdAsync(documentId, ct);
        if (d?.AttachmentId is not int aid || await attachments.GetByIdAsync(aid, ct) is not { } a) return null;
        return await storage.OpenAsync(a.OwnerType, a.OwnerId, a.Id, ct);
    }

    // ===================================================================== HR adds a document

    /// <summary>HR files an extra document (PDF, Word, Excel or image) in a category of the personnel file.</summary>
    public async Task<Result<int>> UploadAsync(int fileId, int categoryId, string? title, string fileName, Stream content, CancellationToken ct = default)
    {
        if (!CanView) return Result.Failure<int>("You do not have access to personnel files.");
        var f = await files.GetByIdAsync(fileId, ct);
        if (f is null) return Result.Failure<int>("Personnel file not found.");
        if (f.EmployeeUserId == me.UserId) return Result.Failure<int>("This is your own personnel file.");
        if (PersonnelFileCategories.All.All(c => c.Id != categoryId)) return Result.Failure<int>("Choose a category.");
        title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title.Trim();
        if (title.Length > 200) return Result.Failure<int>("Keep the title under 200 characters.");

        var stored = await storage.AddAsync(AttachmentOwners.PersonnelFile, f.Id, AttachmentCategory.PersonnelFileDocument, fileName, content, ct);
        if (!stored.Succeeded) return stored;
        var doc = new PersonnelFileDocument
        {
            PersonnelFileId = f.Id, CategoryId = categoryId, Title = title, Kind = PersonnelDocumentKind.File, AttachmentId = stored.Value,
            SourceType = nameof(Attachment), SourceId = stored.Value, DocumentDate = DateTime.UtcNow, AddedByUserId = me.UserId
        };
        documents.Add(doc);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(doc.Id);
    }

    // ===================================================================== Letters (built from encoded values only)

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private async Task<string> AppointmentLetterHtmlAsync(OnboardingRecord o, CancellationToken ct)
    {
        var dept = (await departments.GetByIdAsync(o.DepartmentId, ct))?.Name;
        var site = (await sites.GetByIdAsync(o.SiteId, ct))?.Name;
        var terms = string.Join("<br>", (o.LetterTerms ?? "").Replace("\r\n", "\n").Split('\n').Select(E));
        return $"<h3>Letter of appointment: {E(o.JobTitle)}</h3>" +
               $"<p><strong>Reference:</strong> {E(o.LetterNumber)}<br><strong>Issued:</strong> {o.LetterIssuedAt?.ToString("d MMMM yyyy", Gb)}</p>" +
               $"<p>Dear {E(o.EmployeeName)},</p>" +
               $"<p>We are pleased to appoint you to the position of <strong>{E(o.JobTitle)}</strong> in {E(dept)}, based at {E(site)}, " +
               $"at a monthly salary of GHS {o.MonthlySalary.ToString("N2", Gb)}, with effect from {o.StartDate.ToString("d MMMM yyyy", Gb)}.</p>" +
               $"<h4>Conditions of appointment</h4><p>{terms}</p>" +
               $"<p><strong>Accepted and signed electronically</strong> by {E(o.LetterAcknowledgedName)} on " +
               $"{o.LetterAcknowledgedAt?.ToString("d MMMM yyyy HH:mm", Gb)} (from {E(o.LetterAcknowledgedIp)}).</p>";
    }

    private async Task<string> InductionRecordHtmlAsync(OnboardingRecord o, CancellationToken ct)
    {
        var list = (await tasks.ListAsync(t => t.OnboardingRecordId == o.Id, ct)).OrderBy(t => t.Area).ThenBy(t => t.SortOrder).ToList();
        var names = await users.GetDisplayNamesAsync(list.SelectMany(t => new[] { t.CompletedByUserId, t.VerifiedByUserId }).OfType<string>().Distinct(), ct);
        string N(string? uid) => uid is null ? "" : E(names.GetValueOrDefault(uid));
        var items = string.Concat(list.Select(t =>
            $"<li><strong>{E(t.Area is { } a ? EnumText.Label(a) : EnumText.Label(t.Team))}: {E(t.Title)}</strong>: {E(EnumText.Label(t.Status))}" +
            (t.CompletedAt is { } done ? $" by {N(t.CompletedByUserId)} on {done.ToString("d MMM yyyy", Gb)}" : "") +
            (t.VerifiedAt is { } v ? $", verified by {N(t.VerifiedByUserId)} on {v.ToString("d MMM yyyy", Gb)}" : "") + "</li>"));
        return $"<h3>Onboarding and induction record {E(o.Number)}</h3>" +
               $"<p>{E(o.EmployeeName)}, {E(o.JobTitle)}. Started {o.StartDate.ToString("d MMMM yyyy", Gb)}; staff number {E(o.EmployeeNumber)}.</p>" +
               $"<ul>{items}</ul>";
    }
}
