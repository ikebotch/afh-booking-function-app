using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationOperationalStoreInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationOperationalStoreInitializer> _logger;

    public NotificationOperationalStoreInitializer(
        IServiceProvider services,
        ILogger<NotificationOperationalStoreInitializer> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification DB migration failed. Service startup will continue, but notification reads or writes may fail until migrations are applied.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
