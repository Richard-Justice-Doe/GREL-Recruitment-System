namespace Grsms.Domain.Settings;

/// <summary>How a setting is entered and checked.</summary>
public enum SettingKind { Text, MultiLine, Number, Switch, Email, Secret }

/// <summary>One setting on the System settings screen.</summary>
/// <param name="Key">Stored key, and the same name as the server-configuration key it overrides (e.g. "Email:SmtpHost").</param>
/// <param name="Default">Used when neither the screen nor the server configuration sets it.</param>
public record SettingDefinition(string Key, string Group, string Label, string Help, SettingKind Kind, string? Default = null,
    int Min = 0, int Max = int.MaxValue, int MaxLength = 300);

/// <summary>
/// Phase 16: every setting a Super Admin can change on the System settings screen (an Admin sees them read-only).
///
/// A setting saved on the screen wins; an empty one falls back to the server configuration (appsettings.json,
/// environment variables, e.g. <c>Email__SmtpHost</c>), then to <see cref="SettingDefinition.Default"/>. So GREL IT can
/// keep configuring the server as before (docs/06-configuration.md), and the Super Admin can change things later
/// without IT. Settings that need a restart or a file on the server (database, keys, folders) are deliberately NOT here.
/// </summary>
public static class SettingCatalogue
{
    public const string Organisation = "Organisation and email wording";
    public const string Smtp = "Email server (SMTP)";
    public const string Microsoft365 = "Microsoft 365 sign-in";
    public const string Workflow = "Recruitment workflow";

    // Keys used in code
    public const string OrganisationName = "Organisation:Name";
    public const string EmailGreeting = "Email:Greeting";
    public const string EmailSignature = "Email:Signature";
    public const string FromAddress = "Email:FromAddress";
    public const string FromName = "Email:FromName";
    public const string SmtpHost = "Email:SmtpHost";
    public const string SmtpPort = "Email:SmtpPort";
    public const string SmtpSsl = "Email:EnableSsl";
    public const string SmtpUser = "Email:UserName";
    public const string SmtpPassword = "Email:Password";
    public const string AzureTenant = "AzureAd:TenantId";
    public const string AzureClient = "AzureAd:ClientId";
    public const string AzureSecret = "AzureAd:ClientSecret";
    public const string VacancyClosingDays = "Workflow:VacancyClosingDays";
    public const string OfferReplyMaxDays = "Workflow:OfferReplyMaxDays";
    public const string ApprovalWaitingDays = "Reminders:ApprovalWaitingDays";
    public const string DefaultKnockoutQuestions = "Workflow:DefaultKnockoutQuestions";
    public const string AllowSelfRegistration = "Accounts:AllowSelfRegistration";

    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new(OrganisationName, Organisation, "Organisation name", "Used in email signatures and pages, e.g. \"Ghana Rubber Estates Limited\".",
            SettingKind.Text, "GREL", MaxLength: 150),
        new(EmailGreeting, Organisation, "Greeting at the top of every email", "{{Name}} is replaced by the person's name.",
            SettingKind.Text, "Dear {{Name}},", MaxLength: 150),
        new(EmailSignature, Organisation, "Signature at the bottom of every email", "Plain text; each line on its own line.",
            SettingKind.MultiLine, "GREL Recruitment & Selection Management System", MaxLength: 500),

        new(FromAddress, Smtp, "Sender address", "The address emails come from, e.g. recruitment@grel.example.", SettingKind.Email, "noreply@grsms.local", MaxLength: 256),
        new(FromName, Smtp, "Sender name", "Shown as the sender, e.g. \"GREL Recruitment\".", SettingKind.Text, "GREL Recruitment", MaxLength: 100),
        new(SmtpHost, Smtp, "SMTP server", "e.g. smtp.office365.com. Empty everywhere = emails are saved as files on the server instead of being sent.",
            SettingKind.Text, MaxLength: 200),
        new(SmtpPort, Smtp, "SMTP port", "Usually 587.", SettingKind.Number, "587", Min: 1, Max: 65535),
        new(SmtpSsl, Smtp, "Use an encrypted connection (STARTTLS / SSL)", "Leave on unless IT says otherwise.", SettingKind.Switch, "true"),
        new(SmtpUser, Smtp, "SMTP user name", "If the server needs a login.", SettingKind.Text, MaxLength: 256),
        new(SmtpPassword, Smtp, "SMTP password", "Stored encrypted. Leave empty to keep the current one.", SettingKind.Secret, MaxLength: 300),

        new(AzureTenant, Microsoft365, "Directory (tenant) ID", "From the GRSMS app registration in Microsoft Entra.", SettingKind.Text, MaxLength: 100),
        new(AzureClient, Microsoft365, "Application (client) ID", "From the same app registration.", SettingKind.Text, MaxLength: 100),
        new(AzureSecret, Microsoft365, "Client secret", "Stored encrypted. Leave empty to keep the current one. Renew it in Entra before it expires.",
            SettingKind.Secret, MaxLength: 300),

        new(VacancyClosingDays, Workflow, "Advertising window for a new vacancy (days)",
            "The closing date suggested when a Recruitment Officer creates a vacancy.", SettingKind.Number, "14", Min: 1, Max: 120),
        new(OfferReplyMaxDays, Workflow, "Longest time a candidate may take to answer an offer (days)",
            "Offers can't be sent with a reply-by date later than this.", SettingKind.Number, "30", Min: 1, Max: 90),
        new(ApprovalWaitingDays, Workflow, "Remind approvers after (days)",
            "A request or vacancy waiting this long for a decision triggers a reminder, then one a week.", SettingKind.Number, "3", Min: 1, Max: 30),
        new(DefaultKnockoutQuestions, Workflow, "Knockout questions suggested for every new vacancy",
            "One per line, worded so that Yes is the answer needed. The Recruitment Officer can change them per vacancy.",
            SettingKind.MultiLine, MaxLength: 3000),
        new(AllowSelfRegistration, Workflow, "Job seekers can create their own candidate account",
            "Off = the \"Create a candidate account\" page is closed and only accounts created on Users & roles exist (e.g. while a trial link is shared).",
            SettingKind.Switch, "true"),
    ];

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(s => s.Key == key);
}
