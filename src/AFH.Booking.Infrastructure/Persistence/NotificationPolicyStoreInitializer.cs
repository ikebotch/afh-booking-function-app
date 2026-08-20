using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class NotificationPolicyStoreInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationPolicyStoreInitializer> _logger;

    public NotificationPolicyStoreInitializer(
        IServiceProvider services,
        ILogger<NotificationPolicyStoreInitializer> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationPolicyDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification policy DB migration failed. Service startup will continue, but notification policy reads may use defaults until migrations are applied.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
