using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Users;
using Grsms.Domain.Applications;
using Grsms.Domain.Documents;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Applications;

/// <summary>
/// "My profile" (Module 5): every signed-in person (external candidate or GREL staff) keeps one profile with their
/// contact details, education, experience, skills, certifications, current CV and certificates.
/// People only ever see and change their own profile here; staff see applicants' details on the application instead.
/// </summary>
public class CandidateProfileService(
    IRepository<CandidateProfile> profiles,
    IUserDirectory users,
    AttachmentService files,
    IUnitOfWork unitOfWork,
    ICurrentUserService me)
{
    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");

    /// <summary>The current user's profile form (empty if they have not filled it in yet).</summary>
    public async Task<ProfileFormDto> GetMineAsync(CancellationToken ct = default)
    {
        var p = await FindMineAsync(ct);
        var form = p is null ? new ProfileFormDto() : new ProfileFormDto
        {
            Phone = p.Phone, Location = p.Location, HighestEducation = p.HighestEducation, FieldOfStudy = p.FieldOfStudy,
            YearsExperience = p.YearsExperience, CurrentEmployer = p.CurrentEmployer, CurrentPosition = p.CurrentPosition,
            Skills = p.Skills, Certifications = p.Certifications, LinkedInUrl = p.LinkedInUrl, Summary = p.Summary
        };
        await FillReadOnlyAsync(form, p, ct);
        return form;
    }

    /// <summary>Fills the name, email, documents and "still missing" list (also after a failed save).</summary>
    public async Task FillReadOnlyAsync(ProfileFormDto form, CancellationToken ct = default) =>
        await FillReadOnlyAsync(form, await FindMineAsync(ct), ct);

    public async Task<Result> SaveMineAsync(ProfileFormDto form, CancellationToken ct = default)
    {
        var p = await FindMineAsync(ct) ?? Create();
        p.Phone = Clean(form.Phone);
        p.Location = Clean(form.Location);
        p.HighestEducation = form.HighestEducation ?? EducationLevel.None;
        p.FieldOfStudy = Clean(form.FieldOfStudy);
        p.YearsExperience = form.YearsExperience;
        p.CurrentEmployer = Clean(form.CurrentEmployer);
        p.CurrentPosition = Clean(form.CurrentPosition);
        p.Skills = CleanList(form.Skills);
        p.Certifications = CleanList(form.Certifications);
        p.LinkedInUrl = Clean(form.LinkedInUrl);
        p.Summary = Clean(form.Summary);

        if (p.LinkedInUrl is not null &&
            !(Uri.TryCreate(p.LinkedInUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
            return Result.Failure("The LinkedIn or portfolio link must start with https://");

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Adds a CV or certificate. Uploading a new CV replaces the previous one (only the latest CV is kept on the profile;
    /// applications keep their own copy of the CV they were sent with).
    /// </summary>
    public async Task<Result<int>> AddDocumentAsync(AttachmentCategory category, string fileName, Stream content, CancellationToken ct = default)
    {
        if (category is not (AttachmentCategory.Cv or AttachmentCategory.Certificate))
            return Result.Failure<int>("Choose CV or certificate.");

        var p = await FindMineAsync(ct);
        if (p is null)
        {
            p = Create();
            await unitOfWork.SaveChangesAsync(ct); // the profile needs its id before files can be attached
        }

        var existingCvs = category == AttachmentCategory.Cv
            ? (await files.ListAsync(AttachmentOwners.CandidateProfile, p.Id, ct)).Where(d => d.Category == AttachmentCategory.Cv).ToList()
            : [];
        if (category == AttachmentCategory.Certificate &&
            (await files.ListAsync(AttachmentOwners.CandidateProfile, p.Id, ct)).Count(d => d.Category == AttachmentCategory.Certificate) >= MaxCertificates)
            return Result.Failure<int>($"You can keep at most {MaxCertificates} certificates. Remove one first.");

        var added = await files.AddAsync(AttachmentOwners.CandidateProfile, p.Id, category, fileName, content, ct);
        if (added.Succeeded)
            foreach (var old in existingCvs)
                await files.RemoveAsync(AttachmentOwners.CandidateProfile, p.Id, old.Id, ct);
        return added;
    }

    public const int MaxCertificates = 10;

    public async Task<Result> RemoveDocumentAsync(int attachmentId, CancellationToken ct = default)
    {
        var p = await FindMineAsync(ct);
        return p is null ? Result.Failure("File not found.") : await files.RemoveAsync(AttachmentOwners.CandidateProfile, p.Id, attachmentId, ct);
    }

    public async Task<FileDownload?> OpenDocumentAsync(int attachmentId, CancellationToken ct = default)
    {
        var p = await FindMineAsync(ct);
        return p is null ? null : await files.OpenAsync(AttachmentOwners.CandidateProfile, p.Id, attachmentId, ct);
    }

    // ===================================================================== Used when applying

    /// <summary>The current user's profile, or null if they have none.</summary>
    internal async Task<CandidateProfile?> FindMineAsync(CancellationToken ct)
    {
        var id = MyId;
        return (await profiles.ListAsync(p => p.UserId == id, ct)).FirstOrDefault();
    }

    /// <summary>What is still needed before applying: a phone number, the highest education level and a CV.</summary>
    internal static List<string> MissingFor(CandidateProfile? p, IReadOnlyList<AttachmentDto> docs)
    {
        var missing = new List<string>();
        if (p?.Phone is null) missing.Add("your phone number");
        if (p is null || p.HighestEducation == EducationLevel.None) missing.Add("your highest education");
        if (!docs.Any(d => d.Category == AttachmentCategory.Cv)) missing.Add("your CV");
        return missing;
    }

    private async Task FillReadOnlyAsync(ProfileFormDto form, CandidateProfile? p, CancellationToken ct)
    {
        var contact = await users.GetContactAsync(MyId, ct);
        form.FullName = contact?.FullName ?? me.FullName ?? "";
        form.Email = contact?.Email ?? "";
        form.Documents = p is null ? [] : await files.ListAsync(AttachmentOwners.CandidateProfile, p.Id, ct);
        form.Missing = MissingFor(p, form.Documents);
    }

    private CandidateProfile Create()
    {
        var p = new CandidateProfile { UserId = MyId };
        profiles.Add(p);
        return p;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>One item per line: trims, drops bullets, blanks and duplicates (case-insensitive).</summary>
    internal static string? CleanList(string? text)
    {
        var items = (text ?? "").Split('\n')
            .Select(l => l.Trim().TrimStart('-', '*', '•').Trim())
            .Where(l => l.Length > 0)
            .DistinctBy(l => l.ToLowerInvariant())
            .ToList();
        return items.Count == 0 ? null : string.Join("\n", items);
    }
}
