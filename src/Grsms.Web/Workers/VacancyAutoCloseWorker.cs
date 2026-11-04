using Grsms.Application.Adverts;
using Grsms.Application.Vacancies;

namespace Grsms.Web.Workers;

/// <summary>
/// Background job for date-driven changes. Each run:
///   1. closes published vacancies whose closing date has passed (Phase 3);
///   2. closes job adverts whose end date has passed or whose vacancy is no longer open, and publishes scheduled
///      adverts whose start date has arrived (Phase 4);
///   3. marks offers not answered by their reply-by date as expired (Phase 8).
/// Runs shortly after the application starts, then every "Vacancies:AutoCloseIntervalMinutes" (default 60).
/// Switch it off with "Vacancies:AutoClose" = false (the automated tests do this).
///
/// It runs with no signed-in user, so its history entries and audit rows are recorded as "System".
/// </summary>
public class VacancyAutoCloseWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<VacancyAutoCloseWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Vacancies:AutoClose", true))
        {
            logger.LogInformation("Automatic vacancy closing and advert scheduling are switched off (Vacancies:AutoClose = false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Vacancies:AutoCloseIntervalMinutes", 60)));

        // Short pause so the first run does not compete with application start-up.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            try
            {
                // A fresh scope per run = a fresh database context, like a separate web request.
                using var scope = scopes.CreateScope();
                var closed = await scope.ServiceProvider.GetRequiredService<VacancyService>().CloseExpiredAsync(today, stoppingToken);
                if (closed > 0) logger.LogInformation("Closed {Count} vacancies whose closing date had passed", closed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never let one failure (e.g. database briefly unavailable) stop the job for good.
                logger.LogError(ex, "Automatic vacancy closing failed; will retry in {Interval}", interval);
            }

            try
            {
                using var scope = scopes.CreateScope();
                var (published, closedAdverts) = await scope.ServiceProvider.GetRequiredService<AdvertService>().RunScheduleAsync(today, stoppingToken);
                if (published + closedAdverts > 0)
                    logger.LogInformation("Adverts: {Published} published on their start date, {Closed} closed", published, closedAdverts);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Advert scheduling failed; will retry in {Interval}", interval);
            }

            try
            {
                // Phase 8: offers not answered by their reply-by date expire.
                using var scope = scopes.CreateScope();
                var expired = await scope.ServiceProvider.GetRequiredService<Application.Hiring.HiringService>().ExpireOffersAsync(today, stoppingToken);
                if (expired > 0) logger.LogInformation("Expired {Count} unanswered offers", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Offer expiry failed; will retry in {Interval}", interval);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
