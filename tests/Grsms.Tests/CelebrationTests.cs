using Grsms.Application.Celebrations;
using Grsms.Application.Settings;
using Grsms.Domain.Celebrations;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// 17 November 2026: birthdays (from the onboarding personal record's date of birth) and work anniversaries (from the date joined):
/// today, the next days and the month; 29 February; opting out; department only; the once-a-day morning announcement and greeting;
/// colleagues' messages and HR's moderation.
/// </summary>
public class CelebrationTests : OnboardingTestBase
{
    private async Task<T> Run<T>(Func<CelebrationService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<CelebrationService>());
    }

    private async Task SetAsync(params (string Key, string Value)[] values)
    {
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(values.ToDictionary(v => v.Key, v => (string?)v.Value));
    }

    /// <summary>Ama (the new employee) has her birthday today; a long-serving colleague (Kofi, IT) his 5th work anniversary.</summary>
    private async Task<(int Ama, int Kofi)> PeopleAsync()
    {
        await ActivatedAsync();
        var ama = await Db(db => db.Employees.Where(e => e.UserId == _candidate).Select(e => e.Id).SingleAsync());
        var kofi = await Db(async db =>
        {
            var e = new Domain.Employees.Employee { EmployeeNumber = "GREL-0100", UserId = _itHod, PositionTitle = "Head of IT", DepartmentId = It, SiteId = 1,
                HireDate = Today.AddYears(-5), Status = Domain.Employees.EmployeeStatus.Active, StatusChangedAt = DateTime.UtcNow };
            db.Employees.Add(e);
            await db.SaveChangesAsync();
            return e.Id;
        });
        return (ama, kofi);
    }

    [Fact]
    public void Dates_fall_on_the_right_day_and_29_february_moves_to_the_28th()
    {
        Assert.Equal(new DateOnly(2027, 2, 28), CelebrationService.InYear(new DateOnly(1996, 2, 29), 2027));
        Assert.Equal(new DateOnly(2028, 2, 29), CelebrationService.InYear(new DateOnly(1996, 2, 29), 2028));
        Assert.Equal(new DateOnly(2027, 1, 5), CelebrationService.NextOccurrence(new DateOnly(1990, 1, 5), new DateOnly(2026, 11, 17)));
        Assert.Equal(("1st", "2nd", "3rd", "11th", "22nd"), (CelebrationService.Ordinal(1), CelebrationService.Ordinal(2), CelebrationService.Ordinal(3),
            CelebrationService.Ordinal(11), CelebrationService.Ordinal(22)));
    }

    [Fact]
    public async Task Todays_birthdays_and_anniversaries_are_shown_to_staff_without_the_year_and_opted_out_people_are_left_out()
    {
        var (ama, kofi) = await PeopleAsync();
        As(_staff, Roles.Employee);
        var o = await Run(s => s.OverviewAsync(Today));
        var birthday = Assert.Single(o.Today, c => c.Kind == CelebrationKind.Birthday);
        Assert.Equal((ama, (int?)null), (birthday.EmployeeId, birthday.Years)); // never the age
        var anniversary = Assert.Single(o.Today, c => c.Kind == CelebrationKind.Anniversary);
        Assert.Equal((kofi, (int?)5, true), (anniversary.EmployeeId, anniversary.Years, anniversary.Milestone));
        Assert.Equal((1, 1), (o.BirthdaysToday, o.AnniversariesToday));
        Assert.NotEmpty(await Run(s => s.MonthAsync(Today.Year, Today.Month, null, null)));

        // Candidates see nothing; an opted-out employee disappears.
        As(_candidate2, Roles.Candidate);
        Assert.Empty((await Run(s => s.OverviewAsync(Today))).Today);
        AsHrManager();
        Assert.True((await Run(s => s.SetOptOutAsync(ama, true))).Succeeded);
        As(_staff, Roles.Employee);
        Assert.DoesNotContain((await Run(s => s.OverviewAsync(Today))).Today, c => c.EmployeeId == ama);

        // Department only: staff see their own department's celebrations; HR sees all.
        await SetAsync((SettingCatalogue.CelebrationDepartmentOnly, "true"));
        Assert.Empty((await Run(s => s.OverviewAsync(Today))).Today); // the clerk is in neither department
        AsHrManager();
        Assert.Single((await Run(s => s.OverviewAsync(Today))).Today);
    }

    [Fact]
    public async Task The_morning_announcement_goes_once_to_staff_and_greets_the_people_celebrated()
    {
        var (ama, _) = await PeopleAsync();
        await SetAsync((SettingCatalogue.CelebrationFromHourGmt, "0"));
        var told = await Run(s => s.RunMorningAsync(Today));
        Assert.True(told > 0);
        Assert.Equal(0, await Run(s => s.RunMorningAsync(Today))); // once a day

        var bell = await Db(db => db.Notifications.ToListAsync());
        Assert.Contains(bell, n => n.UserId == _staff && n.Title.Contains("Birthday") && (n.Body ?? "").Contains("Today we celebrate the birthday of ama")); // test accounts are named after their email
        Assert.Contains(bell, n => n.UserId == _candidate && n.Title == "🎂 Happy birthday from all of us at GREL!");
        Assert.Contains(bell, n => n.UserId == _itHod && n.Title == "🎉 Happy 5th work anniversary!");
        Assert.DoesNotContain(bell, n => n.UserId == _candidate && n.Title.StartsWith("🎉 Happy Birthday")); // not told about their own

        // Emails on: the announcement is emailed too (on a new day).
        await SetAsync((SettingCatalogue.CelebrationEmails, "true"));
        await Db(async db => { db.SentReminders.RemoveRange(db.SentReminders); return await db.SaveChangesAsync(); });
        var before = _db.SentEmails().Count(m => m.Contains("clerk@grel.test"));
        await Run(s => s.RunMorningAsync(Today));
        Assert.Equal(before + 1, _db.SentEmails().Count(m => m.Contains("clerk@grel.test"))); // one announcement email (encoded subject with emoji)
        _ = ama;
    }

    [Fact]
    public async Task Colleagues_leave_messages_and_hr_can_hide_them()
    {
        var (ama, _) = await PeopleAsync();
        As(_staff, Roles.Employee);
        Assert.True((await Run(s => s.PostAsync(ama, CelebrationKind.Birthday, "Happy Birthday! 🎂", Today))).Succeeded);
        Assert.False((await Run(s => s.PostAsync(ama, CelebrationKind.Birthday, new string('x', 281), Today))).Succeeded);
        Assert.False((await Run(s => s.PostAsync(ama, CelebrationKind.Anniversary, "Congratulations!", Today))).Succeeded); // not her anniversary
        As(_candidate, Roles.Employee);
        Assert.Equal("This is your own celebration.", (await Run(s => s.PostAsync(ama, CelebrationKind.Birthday, "Me!", Today))).Errors[0]);

        var id = (await Run(s => s.MessagesAsync(ama, CelebrationKind.Birthday, Today.Year))).Single().Id;
        As(_staff, Roles.Employee);
        Assert.False((await Run(s => s.SetHiddenAsync(id, true))).Succeeded); // HR only
        AsHrManager();
        Assert.True((await Run(s => s.SetHiddenAsync(id, true))).Succeeded);
        As(_staff, Roles.Employee);
        Assert.Empty(await Run(s => s.MessagesAsync(ama, CelebrationKind.Birthday, Today.Year)));
        AsHrManager();
        Assert.True(Assert.Single(await Run(s => s.MessagesAsync(ama, CelebrationKind.Birthday, Today.Year))).IsHidden);

        // Messages switched off.
        await SetAsync((SettingCatalogue.CelebrationMessages, "false"));
        As(_staff, Roles.Employee);
        Assert.Equal("Messages are switched off.", (await Run(s => s.PostAsync(ama, CelebrationKind.Birthday, "Hi", Today))).Errors[0]);
    }
}
