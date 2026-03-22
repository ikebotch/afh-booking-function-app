using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class BookingOperationalStoreInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BookingOperationalStoreInitializer> _logger;

    public BookingOperationalStoreInitializer(
        IServiceProvider services,
        ILogger<BookingOperationalStoreInitializer> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            // EF-first schema management: all table changes should come from migrations.
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Booking DB migration failed. Service startup will continue, but writes may fail until migrations are applied.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
