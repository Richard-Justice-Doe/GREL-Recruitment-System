using System.Net;
using Grsms.Application.Common.Interfaces;
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
/// </summary>
public class WorkflowMailer(IUserDirectory users, IEmailService email, ICurrentUserService me, ILogger<WorkflowMailer> logger)
{
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
    }
}
