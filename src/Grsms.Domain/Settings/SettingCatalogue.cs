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

    /// <summary>13 November 2026 (docs/26 Phase 24, docs/35 Wave 1): the offer drafted automatically when a hire is approved.</summary>
    public const string OfferDefaultReplyDays = "Offers:DefaultReplyDays";
    public const string OfferDefaultTerms = "Offers:DefaultTerms";
    public const string ApprovalWaitingDays = "Reminders:ApprovalWaitingDays";
    public const string DefaultKnockoutQuestions = "Workflow:DefaultKnockoutQuestions";
    public const string AllowSelfRegistration = "Accounts:AllowSelfRegistration";
    public const string OnboardingDocumentDays = "Onboarding:DocumentDeadlineWorkingDays";

    /// <summary>12 November 2026 (docs/35, approved 9 Nov in docs/26): interview quorum for newly scheduled sessions.</summary>
    public const string InterviewQuorum = "Interviews:EnforceQuorum";

    /// <summary>12 November 2026 (docs/35 R1): targets for each workflow step, in working days (Monday to Friday), for the Workflow targets report.</summary>
    public const string Targets = "Workflow targets (working days)";
    public const string TargetHodReview = "Targets:HodReviewDays";
    public const string TargetHrApproval = "Targets:HrApprovalDays";
    public const string TargetMdApproval = "Targets:MdApprovalDays";
    public const string TargetVacancyApproval = "Targets:VacancyApprovalDays";
    public const string TargetAdvertReview = "Targets:AdvertReviewDays";
    public const string TargetOfferAnswer = "Targets:OfferAnswerDays";
    public const string TargetDocumentVerification = "Targets:DocumentVerificationDays";

    /// <summary>
    /// 12 November 2026: the letterhead and signatory of the offer and appointment letters, edited on Administration →
    /// Letterhead &amp; signature (not on the System settings screen). The signature image is in <see cref="LetterSignatureGroup"/>,
    /// a group of its own so saving the text fields never clears it.
    /// </summary>
    public const string Letters = "Letterhead and signature";
    public const string LetterSignatureGroup = "Letter signature image";
    public const string LetterCompany = "Letters:CompanyName";
    public const string LetterDepartment = "Letters:Department";
    public const string LetterAddress = "Letters:Address";
    public const string LetterPhone = "Letters:Phone";
    public const string LetterEmail = "Letters:Email";
    public const string LetterWebsite = "Letters:Website";
    public const string LetterSignatoryName = "Letters:SignatoryName";
    public const string LetterSignatoryTitle = "Letters:SignatoryTitle";
    public const string LetterSignatureFile = "Letters:SignatureFile";

    /// <summary>
    /// 13 November 2026 (docs/36): the AI service, edited on Administration → AI settings &amp; usage. Everything is off until the
    /// Super Admin switches AI on, chooses the provider and enters a key; each feature then has its own switch
    /// (<see cref="AiFeatureKey"/>, group <see cref="AiFeaturesGroup"/>).
    /// </summary>
    public const string AiService = "AI service";
    public const string AiFeaturesGroup = "AI features";
    public const string AiEnabled = "Ai:Enabled";
    public const string AiProviderKey = "Ai:Provider";
    public const string AiApiKey = "Ai:ApiKey";
    public const string AiModel = "Ai:Model";
    public const string AiAzureEndpoint = "Ai:AzureEndpoint";
    public const string AiAzureApiVersion = "Ai:AzureApiVersion";
    public const string AiTimeoutSeconds = "Ai:TimeoutSeconds";
    public const string AiMonthlyBudgetUsd = "Ai:MonthlyBudgetUsd";
    public const string AiInputPrice = "Ai:InputPricePerMillionTokens";
    public const string AiOutputPrice = "Ai:OutputPricePerMillionTokens";
    public const string AiCallsPerMinute = "Ai:CallsPerUserPerMinute";
    public const string AiKeepPrompts = "Ai:KeepPrompts";

    /// <summary>The on/off switch of one AI feature, e.g. "Ai:Feature:JobDescription".</summary>
    public static string AiFeatureKey(Ai.AiFeature feature) => $"Ai:Feature:{feature}";

    /// <summary>The features that are built (and so have a switch); more are added as docs/36 §8 progresses.</summary>
    public static readonly IReadOnlyList<Ai.AiFeature> AiFeaturesBuilt = [Ai.AiFeature.JobDescription, Ai.AiFeature.Advert, Ai.AiFeature.InterviewQuestions, Ai.AiFeature.ExecutiveSummary,
        Ai.AiFeature.InterviewSummary, Ai.AiFeature.CvSummary, Ai.AiFeature.CvReading];

    /// <summary>Groups shown on their own page rather than on the System settings screen.</summary>
    public static readonly IReadOnlySet<string> OwnPageGroups = new HashSet<string> { Letters, LetterSignatureGroup, AiService, AiFeaturesGroup };

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
        new(OfferDefaultReplyDays, Workflow, "Reply-by date suggested on an offer draft (days)",
            "Days from today; never later than the start date.", SettingKind.Number, "7", Min: 1, Max: 90),
        new(OfferDefaultTerms, Workflow, "Terms pre-filled on every offer draft",
            "Probation, benefits and other conditions printed in the offer letter. The Recruitment Officer can change them per offer.",
            SettingKind.MultiLine, "Probation: 6 months.\nBenefits: in line with GREL's conditions of service for this grade.", MaxLength: 8000),
        new(ApprovalWaitingDays, Workflow, "Remind approvers after (days)",
            "A request or vacancy waiting this long for a decision triggers a reminder, then one a week.", SettingKind.Number, "3", Min: 1, Max: 30),
        new(InterviewQuorum, Workflow, "Enforce interview quorum",
            "New sessions need at least 3 panel members including the Head of Department (or a delegate) and an HR Manager; scores can only be validated when that many took part (docs/35).",
            SettingKind.Switch, "true"),
        new(OnboardingDocumentDays, Workflow, "Time a new employee has to provide the onboarding documents (working days)",
            "Counted from the day the Recruitment Officer starts the onboarding, Monday to Friday.", SettingKind.Number, "10", Min: 1, Max: 60),
        new(DefaultKnockoutQuestions, Workflow, "Knockout questions suggested for every new vacancy",
            "One per line, worded so that Yes is the answer needed. The Recruitment Officer can change them per vacancy.",
            SettingKind.MultiLine, MaxLength: 3000),
        new(AllowSelfRegistration, Workflow, "Job seekers can create their own candidate account",
            "Off = the \"Create a candidate account\" page is closed and only accounts created on Users & roles exist (e.g. while a trial link is shared).",
            SettingKind.Switch, "true"),

        new(TargetHodReview, Targets, "Head of Department review of a request", "Working days from submission to the HOD's decision.", SettingKind.Number, "2", Min: 1, Max: 60),
        new(TargetHrApproval, Targets, "HR Manager review of a request", "Working days the request waits for the HR Manager.", SettingKind.Number, "2", Min: 1, Max: 60),
        new(TargetMdApproval, Targets, "MD approval of a request", "Working days the request waits for the Managing Director.", SettingKind.Number, "3", Min: 1, Max: 60),
        new(TargetVacancyApproval, Targets, "Vacancy approval", "Working days a vacancy waits for the HR Manager's approval.", SettingKind.Number, "2", Min: 1, Max: 60),
        new(TargetAdvertReview, Targets, "Advert review", "Working days an advert waits at each review (HR review, then IT approval).", SettingKind.Number, "2", Min: 1, Max: 60),
        new(TargetOfferAnswer, Targets, "Candidate's answer to an offer", "Working days from sending the offer to the candidate's answer.", SettingKind.Number, "5", Min: 1, Max: 60),
        new(TargetDocumentVerification, Targets, "Verification of a new-hire document", "Working days from upload to the Recruitment Officer's decision.", SettingKind.Number, "3", Min: 1, Max: 60),

        new(LetterCompany, Letters, "Company name", "Printed next to the logo.", SettingKind.Text, "Ghana Rubber Estates Limited", MaxLength: 150),
        new(LetterDepartment, Letters, "Line under the name", "e.g. Human Resources Department.", SettingKind.Text, "Human Resources", MaxLength: 150),
        new(LetterAddress, Letters, "Postal address", "One line per line, e.g. P.O. Box 228, Takoradi, Ghana.", SettingKind.MultiLine, MaxLength: 400),
        new(LetterPhone, Letters, "Telephone", "e.g. +233 31 202 2700.", SettingKind.Text, MaxLength: 100),
        new(LetterEmail, Letters, "Email", "e.g. hr@grel.example.", SettingKind.Email, MaxLength: 256),
        new(LetterWebsite, Letters, "Website", "e.g. www.grel.example.", SettingKind.Text, MaxLength: 150),
        new(LetterSignatoryName, Letters, "Signatory's name", "Who signs the letters, e.g. Ama Mensah. Empty = the letters are signed \"Human Resources\".", SettingKind.Text, MaxLength: 150),
        new(LetterSignatoryTitle, Letters, "Signatory's title", "e.g. Human Resources Manager.", SettingKind.Text, MaxLength: 150),
        new(LetterSignatureFile, LetterSignatureGroup, "Signature image", "Stored file name of the uploaded signature.", SettingKind.Text, MaxLength: 300),

        new(AiEnabled, AiService, "Use AI", "The main switch. Off = no AI anywhere, whatever the feature switches say.", SettingKind.Switch, "false"),
        new(AiProviderKey, AiService, "Provider", "Anthropic (Claude), OpenAI or Azure OpenAI.", SettingKind.Text, "Anthropic", MaxLength: 30),
        new(AiApiKey, AiService, "API key", "The provider's key. Stored encrypted; never shown again.", SettingKind.Secret, MaxLength: 500),
        new(AiModel, AiService, "Model (or Azure deployment name)",
            "Anthropic: e.g. claude-sonnet-5-5. OpenAI: the model name from your account. Azure OpenAI: the deployment name.", SettingKind.Text, MaxLength: 100),
        new(AiAzureEndpoint, AiService, "Azure OpenAI endpoint", "Only for Azure OpenAI, e.g. https://grel-ai.openai.azure.com.", SettingKind.Text, MaxLength: 300),
        new(AiAzureApiVersion, AiService, "Azure OpenAI API version", "Only for Azure OpenAI.", SettingKind.Text, "2024-10-21", MaxLength: 30),
        new(AiTimeoutSeconds, AiService, "Wait at most (seconds)", "After this, the request is abandoned and the page says AI is unavailable.", SettingKind.Number, "60", Min: 5, Max: 300),
        new(AiMonthlyBudgetUsd, AiService, "Monthly budget (USD)", "AI stops for the rest of the month when the month's cost reaches this (needs the prices below). 0 = no limit.",
            SettingKind.Number, "50", Min: 0, Max: 100000),
        new(AiInputPrice, AiService, "Price per million input tokens (USD)", "From the provider's price list for the chosen model, e.g. 3. Empty = cost not worked out.", SettingKind.Text, MaxLength: 20),
        new(AiOutputPrice, AiService, "Price per million output tokens (USD)", "From the provider's price list, e.g. 15.", SettingKind.Text, MaxLength: 20),
        new(AiCallsPerMinute, AiService, "AI requests per person per minute", "Protects the budget from a stuck button or misuse.", SettingKind.Number, "20", Min: 1, Max: 600),
        new(AiKeepPrompts, AiService, "Keep prompts for troubleshooting",
            "Off (recommended) = the log records only counts and cost. On = what was sent is kept for 30 days (may include personal data).", SettingKind.Switch, "false"),
        .. Enum.GetValues<Ai.AiFeature>().Where(f => f != Ai.AiFeature.ConnectionTest).Select(f =>
            new SettingDefinition(AiFeatureKey(f), AiFeaturesGroup, Ai.AiFeatures.Label(f), Ai.AiFeatures.WhatIsSent(f), SettingKind.Switch, "false")),
    ];

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(s => s.Key == key);
}
