using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Grsms.Domain.Settings;

/// <summary>One email a Super Admin can reword, with its built-in wording and the placeholders it can use.</summary>
public record EmailTemplateDefinition(string Key, string Name, string WhenSent, string DefaultSubject, string DefaultBody,
    IReadOnlyList<string> Placeholders, string LinkText);

/// <summary>
/// Phase 16: the emails sent to CANDIDATES (and internal applicants), which GREL may want to word its own way. Staff
/// notification emails ("a request needs your approval", ...) keep their built-in wording.
///
/// A template is PLAIN TEXT with {{Placeholders}}: a blank line starts a new paragraph, a single line break stays a line
/// break, and nothing typed is treated as HTML (so a template can never inject links or scripts). The values put into
/// placeholders are encoded too. A paragraph that ends up empty (e.g. {{Instructions}} when there are none) is dropped.
/// {{Organisation}} works in every template. Every email also gets the greeting and signature from System settings.
/// </summary>
public static partial class EmailTemplateCatalogue
{
    public const string ApplicationReceived = "application.received";
    public const string NotSuccessful = "application.not-successful";
    public const string Shortlisted = "application.shortlisted";
    public const string InterviewInvitation = "interview.invitation";
    public const string InterviewRescheduled = "interview.rescheduled";
    public const string InterviewCancelled = "interview.cancelled";
    public const string OfferSent = "offer.sent";
    public const string OfferWithdrawn = "offer.withdrawn";
    public const string OfferAccepted = "offer.accepted";
    public const string OfferReminder = "reminder.offer";
    public const string InterviewReminder = "reminder.interview";
    public const string AccountCreated = "account.created";
    public const string AccountCreatedMicrosoft = "account.created-microsoft";
    public const string AccountCreatedCandidate = "account.created-candidate";

    // Phase 18: onboarding emails to the new employee
    public const string OnboardingDocumentsRequested = "onboarding.documents-requested";
    public const string OnboardingDocumentsReturned = "onboarding.documents-returned";
    public const string OnboardingActivated = "onboarding.activated";

    private const string MyApplication = "View my application";

    public static readonly IReadOnlyList<EmailTemplateDefinition> All =
    [
        new(ApplicationReceived, "Application received", "When someone applies",
            "We received your application: {{JobTitle}}",
            "Thank you for applying for {{JobTitle}}. Your reference is {{ApplicationNumber}}.\n\nYou can follow its progress at any time under My applications.",
            ["JobTitle", "ApplicationNumber"], MyApplication),
        new(NotSuccessful, "Not successful", "When HR marks an application not successful (including confirming an ATS rejection). The reason is never sent.",
            "Your application for {{JobTitle}}",
            "Thank you for your interest in the {{JobTitle}} position at {{Organisation}}. After careful consideration, we will not be taking your application further on this occasion.\n\nWe encourage you to apply for future vacancies that match your profile.",
            ["JobTitle"], MyApplication),
        new(Shortlisted, "Shortlisted", "When an application is shortlisted",
            "You have been shortlisted: {{JobTitle}}",
            "Good news: your application for {{JobTitle}} has been shortlisted.\n\nWe will contact you about the next step (assessment or interview).",
            ["JobTitle"], MyApplication),
        new(InterviewInvitation, "Interview or assessment invitation", "When an interview or assessment is scheduled",
            "Invitation: {{Kind}} for {{JobTitle}}",
            "You are invited to a {{Kind}} for {{JobTitle}}.\n\nWhen: {{When}} GMT ({{Duration}} minutes)\nHow: {{Mode}}\nWhere: {{Location}}\n\n{{Instructions}}\n\nIf you cannot attend, please reply to this email as soon as possible.",
            ["Kind", "JobTitle", "When", "Duration", "Mode", "Location", "Instructions"], MyApplication),
        new(InterviewRescheduled, "Interview rescheduled", "When an interview or assessment moves",
            "Your interview has been rescheduled",
            "The {{Kind}} has moved to {{When}} GMT at {{Location}}.",
            ["Kind", "When", "Location"], MyApplication),
        new(InterviewCancelled, "Interview cancelled", "When an interview or assessment is cancelled",
            "Your interview has been cancelled",
            "The {{Kind}} on {{When}} has been cancelled. We will contact you if it is rearranged.",
            ["Kind", "When"], MyApplication),
        new(OfferSent, "Offer of employment", "When an offer is sent",
            "Offer of employment: {{JobTitle}}",
            "Congratulations! {{Organisation}} is pleased to offer you the position of {{JobTitle}}, starting {{StartDate}}.\n\nPlease read your offer letter and accept or decline it by {{ReplyBy}}.",
            ["JobTitle", "StartDate", "ReplyBy"], "Read my offer"),
        new(OfferWithdrawn, "Offer withdrawn", "When an offer is withdrawn (the reason is never sent)",
            "Your offer for {{JobTitle}}",
            "We are sorry to tell you that the offer of employment for {{JobTitle}} has been withdrawn. The recruitment team will contact you.",
            ["JobTitle"], MyApplication),
        new(OfferAccepted, "Welcome (offer accepted)", "When the candidate accepts the offer",
            "Welcome to {{Organisation}}: {{JobTitle}}",
            "Thank you for accepting our offer. We look forward to welcoming you on {{StartDate}}.\n\nHR will contact you about your appointment letter and first day.",
            ["JobTitle", "StartDate"], MyApplication),
        new(OfferReminder, "Reminder: reply to the offer", "The day before the offer's reply-by date",
            "Reminder: please reply to your offer by {{ReplyBy}}",
            "Your offer for the position of {{JobTitle}} is waiting for your reply. Please accept or decline it by {{ReplyBy}}.",
            ["JobTitle", "ReplyBy"], "Read my offer"),
        new(InterviewReminder, "Reminder: interview tomorrow", "The day before an interview or assessment",
            "Reminder: your {{Kind}} is tomorrow",
            "This is a reminder of your {{Kind}} on {{When}}, {{Location}}.",
            ["Kind", "When", "Location"], "See the details"),

        // 5 November 2026: every new staff account gets a welcome email. The temporary password is never emailed.
        new(AccountCreated, "New staff account", "When a Super Admin, Admin or HR Administrator creates a staff account",
            "Your account for the {{Organisation}} recruitment system",
            "An account has been created for you in GRSMS, the {{Organisation}} recruitment and selection system, by {{CreatedBy}}.\n\nSign in with: {{SignInEmail}}\nYour roles: {{Roles}}\n\nUse the link below to choose your password. It works for 24 hours; after that, use \"Forgot password\" on the sign-in page. You can also sign in with the temporary password HR gave you; you will then be asked to choose your own.",
            ["SignInEmail", "Roles", "CreatedBy"], "Choose my password"),
        new(AccountCreatedCandidate, "New candidate account", "When HR creates a candidate account for someone (e.g. while self-registration is closed)",
            "Your {{Organisation}} careers account",
            "An account has been created for you on the {{Organisation}} careers portal, so you can see our vacancies and apply.\n\nSign in with: {{SignInEmail}}\n\nUse the link below to choose your password. It works for 24 hours; after that, use \"Forgot password\" on the sign-in page.",
            ["SignInEmail"], "Choose my password"),
        new(AccountCreatedMicrosoft, "New account (Microsoft 365)", "When someone signs in with Microsoft 365 for the first time and GRSMS creates their account",
            "Welcome to the {{Organisation}} recruitment system",
            "Your GRSMS account was created when you first signed in with Microsoft 365 ({{SignInEmail}}).\n\nYour roles: {{Roles}}. If you need more access, for example to raise recruitment requests, ask HR.",
            ["SignInEmail", "Roles"], "Open GRSMS"),

        // Phase 18 (6 November 2026): onboarding emails to the new employee.
        new(OnboardingDocumentsRequested, "Onboarding: letter and documents", "When the HR Manager first issues the appointment letter",
            "Your appointment letter and new-hire documents: {{JobTitle}}",
            "Your appointment letter for the position of {{JobTitle}} is ready. Please read and sign it in your portal.\n\nPlease also upload your new-hire documents (Ghana Card, SSNIT card, certificates, medical report, police clearance, bank confirmation letter and the others listed), then click \"Submit to HR\". Your first day is {{StartDate}}.",
            ["JobTitle", "StartDate"], "Open my onboarding"),
        new(OnboardingDocumentsReturned, "Onboarding: documents need attention", "When HR sends rejected new-hire documents back to the new employee",
            "Some of your documents need attention",
            "HR has checked your new-hire documents. These need to be uploaded again:\n\n{{Documents}}\n\nPlease upload them and click \"Submit to HR\" again.",
            ["Documents"], "Open my onboarding"),
        new(OnboardingActivated, "Onboarding: staff account active", "When HR activates the new employee at the end of onboarding",
            "Welcome to {{Organisation}}: your staff account is active",
            "Your onboarding as {{JobTitle}} is complete and your GRSMS account is now a staff account. Your staff number is {{StaffNumber}}.\n\nWelcome to the team!",
            ["JobTitle", "StaffNumber"], "Open GRSMS"),
    ];

    public static EmailTemplateDefinition? Find(string key) => All.FirstOrDefault(t => t.Key == key);

    /// <summary>The placeholders a template may use: its own plus {{Organisation}}.</summary>
    public static IEnumerable<string> AllowedPlaceholders(EmailTemplateDefinition t) => t.Placeholders.Append("Organisation");

    /// <summary>Placeholders used in <paramref name="text"/> that the template does not know (shown as an error when saving).</summary>
    public static IReadOnlyList<string> UnknownPlaceholders(EmailTemplateDefinition t, string text) =>
        Placeholder().Matches(text).Select(m => m.Groups[1].Value).Distinct()
            .Where(p => !AllowedPlaceholders(t).Contains(p)).ToList();

    /// <summary>The subject with its placeholders filled in (plain text; line breaks removed).</summary>
    public static string RenderSubject(string subject, IReadOnlyDictionary<string, string?> values) =>
        Regex.Replace(Fill(subject, values, encode: false), @"\s+", " ").Trim();

    /// <summary>The body as safe HTML: everything typed and every value is encoded; blank lines make paragraphs.</summary>
    public static string RenderBody(string body, IReadOnlyDictionary<string, string?> values)
    {
        var html = new StringBuilder();
        foreach (var paragraph in Regex.Split(body.Replace("\r", ""), @"\n\s*\n"))
        {
            var lines = paragraph.Split('\n').Select(l => Fill(WebUtility.HtmlEncode(l), values, encode: true).Trim())
                .Where(l => l.Length > 0).ToList();
            if (lines.Count > 0) html.Append("<p>").Append(string.Join("<br/>", lines)).Append("</p>");
        }
        return html.ToString();
    }

    private static string Fill(string text, IReadOnlyDictionary<string, string?> values, bool encode) =>
        Placeholder().Replace(text, m =>
        {
            var v = values.GetValueOrDefault(m.Groups[1].Value) ?? "";
            return encode ? WebUtility.HtmlEncode(v).Replace("\n", "<br/>") : v;
        });

    [GeneratedRegex(@"\{\{\s*([A-Za-z]+)\s*\}\}")]
    private static partial Regex Placeholder();
}
