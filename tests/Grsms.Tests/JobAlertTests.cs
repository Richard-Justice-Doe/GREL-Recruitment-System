using Grsms.Application.Applications;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>12 November 2026 (docs/34 quick win): opt-in job alerts, sent when a matching advert goes live on the careers page.</summary>
public class JobAlertTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private string _ama = "", _kofi = "", _efua = "";

    public async Task InitializeAsync()
    {
        _ama = await _db.CreateUserAsync("ama@mail.test", null, Roles.Candidate);
        _kofi = await _db.CreateUserAsync("kofi@mail.test", null, Roles.Candidate);
        _efua = await _db.CreateUserAsync("efua@mail.test", null, Roles.Candidate);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<T> Run<T>(Func<JobAlertService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<JobAlertService>());
    }

    private async Task SubscribeAsync(string userId, bool active, string? keywords)
    {
        _db.CurrentUser.SignInAs(userId, userId, Roles.Candidate);
        Assert.True((await Run(s => s.SaveMineAsync(active, keywords))).Succeeded);
    }

    private static (JobAdvert Advert, Vacancy Vacancy) Advert(string title, VacancyType type = VacancyType.External, bool website = true)
    {
        var advert = new JobAdvert
        {
            Id = 41, AdvertNumber = "ADV-2026-0041", VacancyId = 7, Title = title, Body = "Join the TBU factory team at Tsibu.",
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14), Status = AdvertStatus.Live
        };
        advert.Channels.Add(new AdvertChannelPost { Channel = website ? AdvertChannel.CompanyWebsite : AdvertChannel.InternalPortal });
        return (advert, new Vacancy { Id = 7, JobTitle = title, VacancyType = type });
    }

    [Fact]
    public async Task Only_people_who_switched_alerts_on_and_match_are_emailed_once()
    {
        await SubscribeAsync(_ama, true, "Engineer, mechanic");
        await SubscribeAsync(_kofi, true, null);              // every vacancy
        await SubscribeAsync(_efua, true, "accounting");
        _db.CurrentUser.SignInAs("system", "System");

        var (advert, vacancy) = Advert("Process Engineer");
        Assert.Equal(2, await Run(s => s.SendForAdvertAsync(advert, vacancy)));
        var mails = _db.SentEmails();
        Assert.Contains(mails, m => m.Contains("To: ama@mail.test") && m.Contains("New vacancy at GREL: Process Engineer"));
        Assert.Contains(mails, m => m.Contains("To: kofi@mail.test"));
        Assert.DoesNotContain(mails, m => m.Contains("To: efua@mail.test"));
        Assert.Equal(0, await Run(s => s.SendForAdvertAsync(advert, vacancy))); // the same advert going live again: not twice
    }

    [Fact]
    public async Task Staff_only_and_internal_adverts_send_no_alerts_and_switching_off_stops_them()
    {
        await SubscribeAsync(_kofi, true, null);
        _db.CurrentUser.SignInAs("system", "System");
        var (internalOnly, v1) = Advert("Clerk", VacancyType.Internal);
        Assert.Equal(0, await Run(s => s.SendForAdvertAsync(internalOnly, v1)));
        var (portalOnly, v2) = Advert("Clerk", website: false);
        Assert.Equal(0, await Run(s => s.SendForAdvertAsync(portalOnly, v2)));

        await SubscribeAsync(_kofi, false, null);
        _db.CurrentUser.SignInAs(_kofi, _kofi, Roles.Candidate);
        Assert.False((await Run(s => s.GetMineAsync())).IsActive);
        _db.CurrentUser.SignInAs("system", "System");
        var (advert, vacancy) = Advert("Driver");
        Assert.Equal(0, await Run(s => s.SendForAdvertAsync(advert, vacancy)));
    }

    [Fact]
    public void Keywords_match_words_in_any_case()
    {
        var alert = new JobAlertSubscription { Keywords = " engineer ;Driver,, " };
        Assert.Equal(["engineer", "driver"], alert.KeywordList);
        Assert.True(alert.Matches("Senior ENGINEER, TBU"));
        Assert.False(alert.Matches("Accountant"));
        Assert.True(new JobAlertSubscription().Matches("anything"));
    }
}
