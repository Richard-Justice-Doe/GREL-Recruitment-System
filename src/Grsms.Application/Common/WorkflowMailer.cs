using System.Net;
using System.Text.RegularExpressions;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Notifications;
using Grsms.Application.Users;
using Microsoft.Extensions.Logging;

namespace Grsms.Application.Common;

/// <summary>
/// Sends the short "something needs your attention" emails that workflows use (requests, vacancies, ...).
///
///   * Nobody is emailed about their own action.
///   * Every email has the same layout: greeting, message, a button-style link, footer.
///   * A mail failure is logged but never undoes the workflow step, which is already saved; the item
///     still appears in the right people's lists.
///
/// The message text passed in is HTML. Encode any user-typed values with <see cref="Enc"/>.
///
/// Phase 10: every email also becomes an in-app notification (the bell) for the same person, with the same title and
/// link, so the two never disagree. <see cref="TextAsync"/> sends the short text messages (SMS) used for applicants.
/// </summary>
public class WorkflowMailer(IUserDirectory users, IEmailService email, ICurrentUserService me, ILogger<WorkflowMailer> logger,
    INotificationCenter notifications, ISmsSender sms)
{
    /// <summary>Sends a text message (SMS) to a phone number as typed by the applicant; does nothing if it is not usable.</summary>
    public async Task TextAsync(string? phone, string text, CancellationToken ct = default)
    {
        if (PhoneNumbers.ToInternational(phone) is not { } number) return;
        try
        {
            await sms.SendAsync(number, text.Length > 300 ? text[..297] + "..." : text, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send a text message to {Phone}", number);
        }
    }

    /// <summary>Emails every active holder of <paramref name="role"/>, except the current user and <paramref name="skipUserIds"/>.</summary>
    public async Task ToRoleAsync(string role, IEnumerable<string?> skipUserIds, string subject, string html,
        string link, string linkText, CancellationToken ct = default)
    {
        var skip = skipUserIds.OfType<string>().Append(me.UserId).ToHashSet();
        foreach (var person in await users.GetContactsInRoleAsync(role, ct))
            if (!skip.Contains(person.Id))
                await SendAsync(person, subject, html, link, linkText, ct);
    }

    /// <summary>Emails one user (nothing happens if it is the current user or the id is null).</summary>
    public async Task ToUserAsync(string? userId, string subject, string html, string link, string linkText, CancellationToken ct = default)
    {
        if (userId is null || userId == me.UserId) return;
        if (await users.GetContactAsync(userId, ct) is { } person)
            await SendAsync(person, subject, html, link, linkText, ct);
    }

    /// <summary>Emails one user even if it is the current user (e.g. "we received your application").</summary>
    public async Task ToUserIncludingMeAsync(string userId, string subject, string html, string link, string linkText, CancellationToken ct = default)
    {
        if (await users.GetContactAsync(userId, ct) is { } person)
            await SendAsync(person, subject, html, link, linkText, ct);
    }

    /// <summary>HTML-encodes user-typed text before it goes into an email.</summary>
    public static string Enc(string? text) => WebUtility.HtmlEncode(text ?? "");

    private async Task SendAsync(UserContact to, string subject, string html, string link, string linkText, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(to.Email, subject,
                $"""
                <p>Dear {Enc(to.FullName)},</p>
                <p>{html}</p>
                <p><a href="{Enc(link)}">{Enc(linkText)}</a></p>
                <p style="color:#888">GREL Recruitment &amp; Selection Management System</p>
                """, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send '{Subject}' to {Email}", subject, to.Email);
        }

        try
        {
            await notifications.AddAsync(to.Id, subject, PlainText(html), link, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not save the notification '{Subject}' for {UserId}", subject, to.Id);
        }
    }

    /// <summary>The email text without HTML, on one line, at most 500 characters.</summary>
    public static string PlainText(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\s+([.,:;!?)])", "$1");
        return text.Length > 500 ? text[..497] + "..." : text;
    }
}
