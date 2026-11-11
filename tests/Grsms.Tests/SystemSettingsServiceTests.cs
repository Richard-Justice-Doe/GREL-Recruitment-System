using Grsms.Application.Auditing;
using Grsms.Application.Common;
using Grsms.Application.Settings;
using Grsms.Application.Vacancies;
using Grsms.Domain.Auditing;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 16: system settings (fallback order, encrypted secrets, who may change them), email templates (placeholders,
/// nothing typed becomes HTML, reset), the audit monitor's alerts, and the backup folder's safety rules.
/// </summary>
public class SystemSettingsServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    public void Dispose() => _db.Dispose();

    public SystemSettingsServiceTests() => _db.CurrentUser.SignInAs("super-id", "super@grel.test", Roles.SuperAdmin);

    private async Task<T> Scoped<T>(Func<IServiceProvider, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider);
    }

    private ISettingsStore Store => _db.Services.GetRequiredService<ISettingsStore>();

    [Fact]
    public async Task Screen_values_win_then_server_configuration_then_defaults_and_secrets_are_encrypted_and_masked()
    {
        // Nothing saved: server configuration, then the built-in default.
        Assert.Equal(("GREL Recruitment (tests)", "server configuration"), (Store.Get(SettingCatalogue.FromName), Store.SourceOf(SettingCatalogue.FromName)));
        Assert.Equal((587, "default"), (Store.GetInt(SettingCatalogue.SmtpPort), Store.SourceOf(SettingCatalogue.SmtpPort)));

        var save = await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveGroupAsync(SettingCatalogue.Smtp, new Dictionary<string, string?>
        {
            [SettingCatalogue.FromName] = "GREL Jobs", [SettingCatalogue.SmtpHost] = "smtp.example.test", [SettingCatalogue.SmtpPort] = "2525",
            [SettingCatalogue.SmtpPassword] = "S3cret-value"
        }));
        Assert.True(save.Succeeded, string.Join(" ", save.Errors));
        Assert.Equal(("GREL Jobs", "screen"), (Store.Get(SettingCatalogue.FromName), Store.SourceOf(SettingCatalogue.FromName)));
        Assert.Equal(2525, Store.GetInt(SettingCatalogue.SmtpPort));
        Assert.False(Store.GetBool(SettingCatalogue.SmtpSsl)); // an unticked switch means off
        Assert.Equal("S3cret-value", Store.Get(SettingCatalogue.SmtpPassword));

        // The secret is encrypted in the database, never shown back, and masked in the audit trail.
        using (var scope = _db.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.SystemSettings.SingleAsync(s => s.Key == SettingCatalogue.SmtpPassword);
            Assert.True(row.IsSecret);
            Assert.DoesNotContain("S3cret", row.SettingValue);
            Assert.DoesNotContain(await db.AuditLogs.ToListAsync(), a => (a.NewValues ?? "").Contains("S3cret") || (a.NewValues ?? "").Contains(row.SettingValue!));
            Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.EntityName == "SystemSetting" && (a.NewValues ?? "").Contains("(secret, changed)"));
        }
        var rows = await Scoped(sp => Task.FromResult(sp.GetRequiredService<SystemSettingsService>().GetAll()));
        Assert.Null(rows.Single(r => r.Definition.Key == SettingCatalogue.SmtpPassword).Value);
        Assert.True(rows.Single(r => r.Definition.Key == SettingCatalogue.SmtpPassword).HasValue);

        // Leaving the secret empty keeps it; emptying a normal field goes back to the server configuration.
        await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveGroupAsync(SettingCatalogue.Smtp, new Dictionary<string, string?>
            { [SettingCatalogue.FromName] = "", [SettingCatalogue.SmtpPassword] = "" }));
        Assert.Equal("S3cret-value", Store.Get(SettingCatalogue.SmtpPassword));
        Assert.Equal("GREL Recruitment (tests)", Store.Get(SettingCatalogue.FromName));

        // Checks, and only the Super Admin.
        var bad = await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveGroupAsync(SettingCatalogue.Smtp, new Dictionary<string, string?>
            { [SettingCatalogue.SmtpPort] = "70000", [SettingCatalogue.FromAddress] = "not-an-address" }));
        Assert.Equal(2, bad.Errors.Count);
        _db.CurrentUser.SignInAs("admin-id", "admin@grel.test", Roles.Admin);
        Assert.False((await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveGroupAsync(SettingCatalogue.Workflow,
            new Dictionary<string, string?> { [SettingCatalogue.VacancyClosingDays] = "21" }))).Succeeded);
        Assert.True(await Scoped(sp => Task.FromResult(sp.GetRequiredService<SystemSettingsService>().CanView)));
    }

    [Fact]
    public async Task Templates_use_placeholders_never_turn_typed_text_into_html_and_can_be_reset()
    {
        var form = (await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().GetTemplateAsync(EmailTemplateCatalogue.Shortlisted)))!;
        Assert.False(form.IsCustomised);

        form.Subject = "Shortlisted for {{JobTitle}} at {{Organisation}}";
        form.Body = "Well done!\nYou made the shortlist for {{JobTitle}}.\n\n<script>alert(1)</script> {{Unknown}}";
        var refused = await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveTemplateAsync(form));
        Assert.Contains(refused.Errors, e => e.Contains("{{Unknown}}"));

        form.Body = "Well done!\nYou made the shortlist for {{JobTitle}}.\n\n<script>alert(1)</script>";
        Assert.True((await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveTemplateAsync(form))).Succeeded);

        // Sent through the mailer: the new wording, values filled in, the "script" shown as text, greeting and signature from settings.
        var user = await _db.CreateUserAsync("kwame@mail.test", null, Roles.Candidate);
        await Scoped(async sp =>
        {
            await sp.GetRequiredService<WorkflowMailer>().TemplateToUserAsync(user, EmailTemplateCatalogue.Shortlisted,
                new Dictionary<string, string?> { ["JobTitle"] = "Safety <Officer>" }, "https://grsms.test/x");
            return 0;
        });
        var mail = _db.SentEmails().Single(m => m.Contains("kwame@mail.test"));
        var decoded = DecodeBody(mail);
        Assert.Contains("Subject: Shortlisted for Safety <Officer> at GREL", mail);
        Assert.Contains("Well done!<br/>You made the shortlist for Safety &lt;Officer&gt;.", decoded);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", decoded);
        Assert.DoesNotContain("<script>", decoded);
        Assert.Contains("Dear kwame,", decoded);

        Assert.True((await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().ResetTemplateAsync(EmailTemplateCatalogue.Shortlisted))).Succeeded);
        Assert.False((await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().GetTemplateAsync(EmailTemplateCatalogue.Shortlisted)))!.IsCustomised);
        // ...and it can be customised again after a reset.
        Assert.True((await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveTemplateAsync(form))).Succeeded);
    }

    [Fact]
    public async Task Workflow_settings_reach_the_vacancy_form()
    {
        await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveGroupAsync(SettingCatalogue.Workflow, new Dictionary<string, string?>
        {
            [SettingCatalogue.VacancyClosingDays] = "21", [SettingCatalogue.OfferReplyMaxDays] = "10", [SettingCatalogue.ApprovalWaitingDays] = "5",
            [SettingCatalogue.DefaultKnockoutQuestions] = "Are you willing to work at any GREL site?"
        }));
        Assert.Equal((21, 10, 5), (Store.GetInt(SettingCatalogue.VacancyClosingDays), Store.GetInt(SettingCatalogue.OfferReplyMaxDays),
            Store.GetInt(SettingCatalogue.ApprovalWaitingDays)));
        Assert.Equal("Are you willing to work at any GREL site?", Store.Get(SettingCatalogue.DefaultKnockoutQuestions));
    }

    [Fact]
    public async Task Audit_monitor_raises_alerts_for_repeated_failed_sign_ins_and_settings_changes()
    {
        using (var scope = _db.NewScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            for (var i = 0; i < 5; i++) await audit.LogAsync(AuditActions.SignInFailed, null, "target@grel.test");
            await audit.LogAsync(AuditActions.LockedOut, null, "target@grel.test");
        }
        await Scoped(sp => sp.GetRequiredService<SystemSettingsService>().SaveGroupAsync(SettingCatalogue.Workflow,
            new Dictionary<string, string?> { [SettingCatalogue.VacancyClosingDays] = "21" }));

        var m = await Scoped(sp => sp.GetRequiredService<IAuditLogService>().MonitorAsync(DateTime.UtcNow));
        Assert.Equal((5, 1), (m.Last24Hours.FailedSignIns, m.Last24Hours.Lockouts));
        Assert.Contains(m.Alerts, a => a.Contains("5 failed sign-ins for target@grel.test"));
        Assert.Contains(m.Alerts, a => a.Contains("lockout"));
        Assert.Contains(m.Alerts, a => a.Contains("permissions, roles or settings"));
        Assert.Equal("target@grel.test", m.FailedSignInsByAccount.First().Name);
        Assert.Contains(m.RecentSettingsChanges, a => a.EntityName == "SystemSetting");
    }

    [Fact]
    public async Task Backups_list_only_backup_files_download_only_database_dumps_and_requests_leave_a_file()
    {
        var store = _db.Services.GetRequiredService<IBackupStore>();
        var folder = Path.Combine(_db.TempFolder, "backups");
        Assert.False(store.Overview().Configured); // the folder does not exist yet

        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "grsms-db-20261105-0100.sql.gz"), "dump");
        await File.WriteAllTextAsync(Path.Combine(folder, "grsms-files-20261105-0100.tar.gz"), "files");
        await File.WriteAllTextAsync(Path.Combine(folder, "notes.txt"), "not a backup");
        await File.WriteAllTextAsync(Path.Combine(_db.TempFolder, "secret.txt"), "outside");

        var o = store.Overview();
        Assert.True(o.Configured);
        Assert.Equal(["Database", "Files and keys"], o.Files.Select(f => f.Kind).OrderBy(k => k));

        using (var dump = store.Open("grsms-db-20261105-0100.sql.gz")) Assert.NotNull(dump);
        Assert.Null(store.Open("grsms-files-20261105-0100.tar.gz")); // holds the keys: server only
        Assert.Null(store.Open("../secret.txt"));
        Assert.Null(store.Open("notes.txt"));

        Assert.True(await store.RequestBackupAsync());
        Assert.NotNull(store.Overview().RequestedAt);
        Assert.True(File.Exists(Path.Combine(folder, "requests", "backup-request")));
    }

    /// <summary>The body of a saved .eml file, decoded (base64 or quoted-printable).</summary>
    private static string DecodeBody(string m)
    {
        if (m.Contains("Content-Transfer-Encoding: quoted-printable"))
            return System.Text.RegularExpressions.Regex.Replace(m.Replace("=\r\n", "").Replace("=\n", ""), "=([0-9A-F]{2})",
                x => ((char)Convert.ToInt32(x.Groups[1].Value, 16)).ToString());
        var split = m.IndexOf("\r\n\r\n", StringComparison.Ordinal) is var i and >= 0 ? i : m.IndexOf("\n\n", StringComparison.Ordinal);
        if (split < 0 || !m.Contains("Content-Transfer-Encoding: base64")) return m;
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(m[split..].Where(c => !char.IsWhiteSpace(c)))));
    }
}
