using System.Globalization;
using System.Net.Mail;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;

namespace Grsms.Application.Settings;

/// <summary>
/// Phase 16: the System settings and Email templates screens. Changing anything needs "settings.manage" (Super Admin);
/// "settings.view" (the Admin by default) sees everything read-only. Secrets are never sent back to the browser.
/// Every change is a database change, so it is in the audit trail (secret values masked).
/// </summary>
public class SystemSettingsService(
    ISettingsStore store,
    IRepository<EmailTemplate> templates,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    IEmailService email)
{
    private const string NotAllowed = "Only a Super Admin can change system settings.";
    public bool CanManage => me.HasPermission(Permissions.ManageSystemSettings);
    public bool CanView => CanManage || me.HasPermission(Permissions.ViewSystemSettings);

    // ===================================================================== Settings

    /// <summary>The settings of the System settings screen (groups with a page of their own, such as the letterhead, are left out).</summary>
    public IReadOnlyList<SettingRowDto> GetAll() => Rows(SettingCatalogue.All.Where(d => !SettingCatalogue.OwnPageGroups.Contains(d.Group)));

    /// <summary>The settings of one group, e.g. the letterhead page.</summary>
    public IReadOnlyList<SettingRowDto> GetGroup(string group) => Rows(SettingCatalogue.All.Where(d => d.Group == group));

    private List<SettingRowDto> Rows(IEnumerable<SettingDefinition> definitions) =>
        definitions.Select(d =>
        {
            var value = store.Get(d.Key);
            var has = !string.IsNullOrEmpty(value);
            return new SettingRowDto(d, d.Kind == SettingKind.Secret ? null : value, store.SourceOf(d.Key), has);
        }).ToList();

    /// <summary>Saves one group of the screen. <paramref name="values"/>: key → typed text (secrets: empty = keep).</summary>
    public async Task<Result> SaveGroupAsync(string group, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure(NotAllowed);
        var errors = new List<string>();
        var toSave = new Dictionary<string, string?>();
        foreach (var d in SettingCatalogue.All.Where(d => d.Group == group))
        {
            var raw = values.GetValueOrDefault(d.Key)?.Trim();
            if (d.Kind == SettingKind.Switch) raw = raw is "true" or "on" ? "true" : "false";
            if (d.Kind == SettingKind.Secret && string.IsNullOrEmpty(raw)) continue; // keep the current secret
            if (raw is { Length: > 0 } && raw.Length > d.MaxLength) errors.Add($"{d.Label}: at most {d.MaxLength} characters.");
            if (d.Kind == SettingKind.Number && !string.IsNullOrEmpty(raw) &&
                (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < d.Min || n > d.Max))
                errors.Add($"{d.Label}: enter a whole number from {d.Min} to {d.Max}.");
            if (d.Kind == SettingKind.Email && !string.IsNullOrEmpty(raw) && !MailAddress.TryCreate(raw, out _))
                errors.Add($"{d.Label}: enter a valid email address.");
            toSave[d.Key] = raw;
        }
        if (errors.Count > 0) return Result.Failure([.. errors]);
        await store.SaveAsync(toSave, ct);
        return Result.Success();
    }

    /// <summary>Removes a saved secret (back to the server configuration, if any).</summary>
    public async Task<Result> ClearSecretAsync(string key, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure(NotAllowed);
        if (SettingCatalogue.Find(key)?.Kind != SettingKind.Secret) return Result.Failure("Unknown setting.");
        await store.SaveAsync(new Dictionary<string, string?> { [key] = "" }, ct);
        return Result.Success();
    }

    /// <summary>Sends a test email to <paramref name="to"/> with the current email server settings.</summary>
    public async Task<Result> SendTestEmailAsync(string? to, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure(NotAllowed);
        if (string.IsNullOrWhiteSpace(to) || !MailAddress.TryCreate(to.Trim(), out _)) return Result.Failure("Enter the address to send the test to.");
        try
        {
            await email.SendNowAsync(to.Trim(), "GRSMS test email", // waits for the server, so the result is real
                "<p>This is a test email from GRSMS. If you can read it, the email settings work.</p>", ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"The email could not be sent: {ex.Message}");
        }
    }

    // ===================================================================== Email templates

    public async Task<IReadOnlyList<EmailTemplateRowDto>> GetTemplatesAsync(CancellationToken ct = default)
    {
        var saved = (await templates.ListAsync(null, ct)).ToDictionary(t => t.Key);
        return EmailTemplateCatalogue.All.Select(d => saved.TryGetValue(d.Key, out var t)
            ? new EmailTemplateRowDto(d, true, t.UpdatedAt ?? t.CreatedAt, t.UpdatedBy ?? t.CreatedBy)
            : new EmailTemplateRowDto(d, false, null, null)).ToList();
    }

    public async Task<EmailTemplateFormDto?> GetTemplateAsync(string key, CancellationToken ct = default)
    {
        if (EmailTemplateCatalogue.Find(key) is not { } d) return null;
        var saved = (await templates.ListAsync(t => t.Key == key, ct)).FirstOrDefault();
        var form = new EmailTemplateFormDto
        {
            Key = key, Definition = d, IsCustomised = saved is not null,
            Subject = saved?.Subject ?? d.DefaultSubject, Body = saved?.Body ?? d.DefaultBody
        };
        Preview(form);
        return form;
    }

    /// <summary>Fills the preview with example values.</summary>
    public void Preview(EmailTemplateFormDto form)
    {
        form.Definition ??= EmailTemplateCatalogue.Find(form.Key);
        var values = ExampleValues();
        form.PreviewSubject = EmailTemplateCatalogue.RenderSubject(form.Subject ?? "", values);
        form.PreviewHtml = EmailTemplateCatalogue.RenderBody(form.Body ?? "", values);
    }

    public async Task<Result> SaveTemplateAsync(EmailTemplateFormDto form, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure(NotAllowed);
        if (EmailTemplateCatalogue.Find(form.Key) is not { } d) return Result.Failure("Unknown email template.");
        var subject = form.Subject?.Trim() ?? "";
        var body = form.Body?.Trim() ?? "";
        var errors = new List<string>();
        if (subject.Length is < 3 or > 200) errors.Add("The subject must be 3 to 200 characters.");
        if (body.Length is < 10 or > 4000) errors.Add("The message must be 10 to 4,000 characters.");
        var unknown = EmailTemplateCatalogue.UnknownPlaceholders(d, subject + "\n" + body);
        if (unknown.Count > 0)
            errors.Add($"Unknown placeholder(s): {string.Join(", ", unknown.Select(u => "{{" + u + "}}"))}. Use only: {string.Join(", ", EmailTemplateCatalogue.AllowedPlaceholders(d).Select(p => "{{" + p + "}}"))}.");
        if (errors.Count > 0) return Result.Failure([.. errors]);

        var saved = (await templates.ListAsync(t => t.Key == form.Key, ct)).FirstOrDefault();
        if (saved is null) templates.Add(new EmailTemplate { Key = form.Key, Subject = subject, Body = body });
        else { saved.Subject = subject; saved.Body = body; }
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Goes back to the built-in wording.</summary>
    public async Task<Result> ResetTemplateAsync(string key, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure(NotAllowed);
        if ((await templates.ListAsync(t => t.Key == key, ct)).FirstOrDefault() is { } saved)
        {
            templates.Remove(saved);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    private Dictionary<string, string?> ExampleValues() => new()
    {
        ["Organisation"] = store.Get(SettingCatalogue.OrganisationName),
        ["JobTitle"] = "Safety Officer", ["ApplicationNumber"] = "APP-2026-00042", ["Kind"] = "interview",
        ["When"] = "Tuesday 17 November 2026, 10:00", ["Duration"] = "45", ["Mode"] = "In person", ["Location"] = "Head Office, Takoradi",
        ["Instructions"] = "Please bring your original certificates.", ["StartDate"] = "1 December 2026", ["ReplyBy"] = "Friday 20 November 2026"
    };
}
