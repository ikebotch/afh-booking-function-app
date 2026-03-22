using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class AdviserDirectoryProjectionSyncWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly AdviserDirectoryOptions _options;
    private readonly ILogger<AdviserDirectoryProjectionSyncWorker> _logger;

    public AdviserDirectoryProjectionSyncWorker(
        IServiceProvider services,
        IOptions<AdviserDirectoryOptions> options,
        ILogger<AdviserDirectoryProjectionSyncWorker> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogInformation("Adviser directory sync worker disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(5, _options.SyncIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _services.CreateAsyncScope();
                var sync = scope.ServiceProvider.GetRequiredService<IAdviserDirectorySyncService>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var result = await sync.SyncAsync(stoppingToken);
                await uow.SaveChangesAsync(stoppingToken);

                _logger.LogInformation(
                    "Adviser directory projection sync completed. SyncedCount={SyncedCount} Mailboxes={Mailboxes} CreatedOrRenewed={CreatedOrRenewed} Skipped={Skipped} Failures={Failures} SyncedAtUtc={SyncedAtUtc}",
                    result.SyncedCount,
                    result.MailboxesDetected,
                    result.SubscriptionsCreatedOrRenewed,
                    result.SubscriptionsSkipped,
                    result.SubscriptionFailures,
                    result.SyncedAtUtc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Adviser directory projection sync failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
