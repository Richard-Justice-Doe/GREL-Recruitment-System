using Grsms.Application.Common;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Settings;

namespace Grsms.Application.Ai;

/// <summary>What the AI settings &amp; usage page shows.</summary>
public sealed record AiSettingsPageDto(
    IReadOnlyList<SettingRowDto> Service,
    IReadOnlyList<(SettingRowDto Row, AiFeature Feature, bool Built, bool PersonalData)> Features,
    bool Configured,
    bool PricesSet,
    int BudgetUsd,
    AiUsageDto Usage);

/// <summary>
/// 13 November 2026 (docs/36): Administration → AI settings &amp; usage. The Super Admin ("settings.manage") switches AI on,
/// chooses the provider, enters the key and model, sets the budget and prices, switches each feature on, and tests the
/// connection; the Admin sees it read-only. Values are saved through <see cref="SystemSettingsService"/> (validation, encrypted
/// key, audit trail).
/// </summary>
public class AiAdminService(SystemSettingsService settings, ISettingsStore store, AiGateway gateway, IAiLogStore logs)
{
    public bool CanManage => settings.CanManage;

    public async Task<AiSettingsPageDto> GetPageAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var features = settings.GetGroup(SettingCatalogue.AiFeaturesGroup)
            .Select(r => (Row: r, Feature: Enum.Parse<AiFeature>(r.Definition.Key["Ai:Feature:".Length..])))
            .Select(x => (x.Row, x.Feature, SettingCatalogue.AiFeaturesBuilt.Contains(x.Feature), AiFeatures.SendsPersonalData(x.Feature)))
            .OrderByDescending(x => x.Item3).ToList();
        return new AiSettingsPageDto(settings.GetGroup(SettingCatalogue.AiService), features, gateway.Connection() is not null && gateway.IsConfigured,
            gateway.Prices() is not null, store.GetInt(SettingCatalogue.AiMonthlyBudgetUsd),
            await logs.UsageAsync(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), 25, ct));
    }

    public async Task<Result> SaveServiceAsync(IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
    {
        var provider = values.GetValueOrDefault(SettingCatalogue.AiProviderKey)?.Trim();
        if (!Enum.TryParse<AiProvider>(provider, out _)) return Result.Failure("Choose the provider.");
        foreach (var priceKey in new[] { SettingCatalogue.AiInputPrice, SettingCatalogue.AiOutputPrice })
            if (values.GetValueOrDefault(priceKey) is { Length: > 0 } price &&
                !(decimal.TryParse(price, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var p) && p >= 0))
                return Result.Failure("Prices must be numbers such as 3 or 0.25 (US dollars per million tokens).");
        if (provider == nameof(AiProvider.AzureOpenAI) &&
            !Uri.TryCreate(values.GetValueOrDefault(SettingCatalogue.AiAzureEndpoint)?.Trim(), UriKind.Absolute, out var uri) | uri?.Scheme != "https")
            return Result.Failure("Azure OpenAI needs its endpoint, starting with https://.");
        if (provider is nameof(AiProvider.OpenAI) or nameof(AiProvider.AzureOpenAI) && string.IsNullOrWhiteSpace(values.GetValueOrDefault(SettingCatalogue.AiModel)))
            return Result.Failure(provider == nameof(AiProvider.OpenAI) ? "Enter the OpenAI model name." : "Enter the Azure OpenAI deployment name.");
        return await settings.SaveGroupAsync(SettingCatalogue.AiService, values, ct);
    }

    public Task<Result> SaveFeaturesAsync(IReadOnlyDictionary<string, string?> values, CancellationToken ct = default) =>
        settings.SaveGroupAsync(SettingCatalogue.AiFeaturesGroup, values, ct);

    public Task<Result> ClearKeyAsync(CancellationToken ct = default) => settings.ClearSecretAsync(SettingCatalogue.AiApiKey, ct);

    /// <summary>The answer shape of the connection test (also proves structured output works with this provider and model).</summary>
    public sealed record ConnectionCheck(bool Ok, string Reply);

    /// <summary>Sends a tiny request with the saved settings. Needs the main switch on and a key.</summary>
    public async Task<Result<string>> TestAsync(CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure<string>("Only a Super Admin can test the AI connection.");
        if (!gateway.IsConfigured) return Result.Failure<string>("Switch \"Use AI\" on and save an API key first.");
        var r = await gateway.RunAsync<ConnectionCheck>(AiFeature.ConnectionTest, "v1",
            "This is a connection test from an HR system. Answer with ok = true and reply = a greeting of at most eight words.", "Test.",
            maxOutputTokens: 200, ct: ct);
        return r.Succeeded && r.Value!.Ok
            ? Result.Success($"Connected. The model replied: \"{r.Value.Reply}\"")
            : Result.Failure<string>(r.Problem ?? "The answer was not as expected.");
    }
}
