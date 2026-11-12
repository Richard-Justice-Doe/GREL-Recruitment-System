using Grsms.Application.Onboarding;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 27 stage C (12 November 2026): the onboarding document deadline: reminders every 3 working days, the last one
/// copied to the Recruitment Officer, an overdue notice once, the RO's daily digest, and one extension of at most 14 days.
/// </summary>
public class DocumentDeadlineTests : OnboardingTestBase
{
    private async Task<DocumentDeadlineService.RunResult> RunOn(DateOnly day)
    {
        As(_staff, Roles.Employee); // the daily job acts as nobody in particular (the mailer never emails the person acting)
        using var scope = _db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<DocumentDeadlineService>().RunAsync(day);
    }

    private int MailsTo(string address, string subjectPart) =>
        _db.SentEmails().Count(m => m.Contains($"To: {address}") && m.Contains(subjectPart));

    [Fact]
    public async Task Reminders_go_out_every_three_working_days_then_the_last_day_then_overdue_once()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);
        var o = await RecordAsync(id);
        var started = DateOnly.FromDateTime(o.CreatedAt);
        var due = o.DocumentsDueDate!.Value;

        Assert.Equal(0, (await RunOn(OnboardingWorkflow.AddWorkingDays(started, 2))).Reminders); // not a third working day
        var day3 = OnboardingWorkflow.AddWorkingDays(started, 3);
        Assert.Equal(1, (await RunOn(day3)).Reminders);
        Assert.Equal(0, (await RunOn(day3)).Reminders);                                     // not twice the same day
        Assert.Equal(1, MailsTo("ama@mail.test", "Reminder: your onboarding documents"));

        Assert.Equal(1, (await RunOn(due)).Reminders);                                      // the last day, copied to the RO
        Assert.Equal(1, MailsTo("ama@mail.test", "Last day for your onboarding documents"));
        Assert.Equal(1, MailsTo("ro@grel.test", "documents due today"));

        var after = OnboardingWorkflow.AddWorkingDays(due, 1);
        Assert.Equal(1, (await RunOn(after)).Overdue);
        Assert.Equal(0, (await RunOn(OnboardingWorkflow.AddWorkingDays(due, 2))).Overdue); // once
        Assert.Equal(1, MailsTo("ro@grel.test", "documents overdue"));

        // The list flags it.
        AsRecruiter();
        var row = (await Run(s => s.SearchAsync(new OnboardingFilter { Tab = "progress" }))).Single(r => r.Id == id);
        Assert.True(row.DocumentsOverdue(after));
        Assert.False(row.DocumentsOverdue(due));
    }

    [Fact]
    public async Task The_recruitment_officer_gets_one_digest_of_documents_to_verify()
    {
        await SubmittedAsync(Today);                                // everything uploaded, waiting for verification
        var weekday = OnboardingWorkflow.AddWorkingDays(Today, 1);
        Assert.Equal(1, (await RunOn(weekday)).Digests);
        Assert.Equal(0, (await RunOn(weekday)).Digests);            // one a day
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ro@grel.test") && m.Contains("to verify") && m.Contains("Ama Mensah"));
        Assert.Equal(0, (await RunOn(weekday)).Reminders);          // nothing left for the employee to do
    }

    [Fact]
    public async Task The_deadline_can_be_extended_once_by_at_most_fourteen_days()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);
        var due = (await RecordAsync(id)).DocumentsDueDate!.Value;

        As(_staff, Roles.Employee);
        Assert.False((await Run(s => s.ExtendDocumentDeadlineAsync(id, due.AddDays(5), "Travelling"))).Succeeded); // not HR
        AsRecruiter();
        Assert.Contains("at most 14 days", (await Run(s => s.ExtendDocumentDeadlineAsync(id, due.AddDays(15), "Travelling"))).Errors[0]);
        Assert.False((await Run(s => s.ExtendDocumentDeadlineAsync(id, due.AddDays(5), null))).Succeeded);         // reason required
        var ok = await Run(s => s.ExtendDocumentDeadlineAsync(id, due.AddDays(5), "Waiting for the police report"));
        Assert.True(ok.Succeeded, string.Join(" ", ok.Errors));
        Assert.Contains("already been extended", (await Run(s => s.ExtendDocumentDeadlineAsync(id, due.AddDays(7), "Again"))).Errors[0]);

        var o = await Db(db => db.Onboardings.Include(x => x.Actions).SingleAsync(x => x.Id == id));
        Assert.Equal((due.AddDays(5), true), (o.DocumentsDueDate!.Value, o.DocumentsDueDateExtended));
        Assert.Contains(o.Actions, a => a.Action == OnboardingAction.DeadlineExtended && a.Comments!.Contains("police report"));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("More time for your onboarding documents"));
    }
}
