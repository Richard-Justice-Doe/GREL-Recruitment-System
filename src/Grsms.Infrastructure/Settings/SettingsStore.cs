using System.Globalization;
using Grsms.Application.Settings;
using Grsms.Domain.Settings;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Settings;

/// <summary>
/// Phase 16: <see cref="ISettingsStore"/>. The SystemSettings table (values saved on the screen) is kept in memory and
/// re-read at most once a minute, and at once after a save on this server. Secrets are encrypted with ASP.NET Core data
/// protection (the same keys that protect sign-in cookies, in DataProtection:KeysFolder), so a copy of the database
/// alone does not reveal them. If the keys are lost, the secrets must be typed in again; GRSMS then logs a warning and
/// falls back to the server configuration.
/// Registered as a singleton; it opens its own short database scope to read and write.
/// </summary>
public sealed class SettingsStore(IServiceScopeFactory scopes, IConfiguration config, IDataProtectionProvider protection,
    ILogger<SettingsStore> logger) : ISettingsStore
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);
    private readonly IDataProtector _protector = protection.CreateProtector("GRSMS.SystemSettings.v1");
    private readonly Lock _lock = new();
    private Dictionary<string, string?>? _saved;
    private DateTime _loadedAt;

    public event Action? Changed;

    public string? Get(string key)
    {
        var saved = Saved().GetValueOrDefault(key);
        if (!string.IsNullOrEmpty(saved)) return saved;
        var configured = config[key];
        if (!string.IsNullOrEmpty(configured)) return configured;
        return SettingCatalogue.Find(key)?.Default;
    }

    public int GetInt(string key) =>
        int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n
        : int.Parse(SettingCatalogue.Find(key)?.Default ?? "0", CultureInfo.InvariantCulture);

    public bool GetBool(string key) => bool.TryParse(Get(key), out var b) ? b : bool.Parse(SettingCatalogue.Find(key)?.Default ?? "false");

    public string SourceOf(string key) =>
        !string.IsNullOrEmpty(Saved().GetValueOrDefault(key)) ? "screen"
        : !string.IsNullOrEmpty(config[key]) ? "server configuration"
        : SettingCatalogue.Find(key)?.Default is not null ? "default" : "not set";

    public async Task SaveAsync(IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
    {
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.SystemSettings.ToDictionaryAsync(s => s.Key, ct);
            foreach (var (key, raw) in values)
            {
                if (SettingCatalogue.Find(key) is not { } d) continue;
                var secret = d.Kind == SettingKind.Secret;
                if (secret && raw is null) continue; // not typed: keep
                var stored = string.IsNullOrEmpty(raw) ? null : secret ? _protector.Protect(raw) : raw;
                if (rows.TryGetValue(key, out var row))
                {
                    if (row.SettingValue != stored) row.SettingValue = stored;
                }
                else if (stored is not null)
                    db.SystemSettings.Add(new SystemSetting { Key = key, SettingValue = stored, IsSecret = secret });
            }
            await db.SaveChangesAsync(ct);
        }
        lock (_lock) _saved = null;
        Changed?.Invoke();
    }

    /// <summary>The values saved on the screen (secrets decrypted), from memory or the database.</summary>
    private Dictionary<string, string?> Saved()
    {
        lock (_lock)
        {
            if (_saved is not null && DateTime.UtcNow - _loadedAt < MaxAge) return _saved;
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                _saved = db.SystemSettings.AsNoTracking().ToList().ToDictionary(s => s.Key, s => s.IsSecret ? Unprotect(s) : s.SettingValue);
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or System.Data.Common.DbException)
            {
                // e.g. the very first start-up, before the table exists: the server configuration still applies.
                logger.LogWarning(ex, "System settings could not be read; using the server configuration");
                _saved ??= [];
            }
            _loadedAt = DateTime.UtcNow;
            return _saved;
        }
    }

    private string? Unprotect(SystemSetting s)
    {
        if (string.IsNullOrEmpty(s.SettingValue)) return null;
        try { return _protector.Unprotect(s.SettingValue); }
        catch (System.Security.Cryptography.CryptographicException)
        {
            logger.LogWarning("The saved secret {Key} cannot be read (were the data-protection keys replaced?). Type it in again on System settings.", s.Key);
            return null;
        }
    }
}
