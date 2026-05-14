using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Infrastructure.Logging;

internal sealed class DatabaseApplicationLogSink : IApplicationLogSink
{
    private readonly IDbContextFactory<BookingDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseApplicationLogSink> _logger;

    public DatabaseApplicationLogSink(
        IDbContextFactory<BookingDbContext> dbContextFactory,
        ILogger<DatabaseApplicationLogSink> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.ApplicationLogs.Add(new ApplicationLogModel
            {
                Id = Guid.NewGuid().ToString("N"),
                OccurredUtc = entry.OccurredUtc,
                Level = ApplicationLogPayloadHelper.Truncate(entry.Level, 32) ?? "Information",
                Category = ApplicationLogPayloadHelper.Truncate(entry.Category, 128) ?? "Application",
                Operation = ApplicationLogPayloadHelper.Truncate(entry.Operation, 256) ?? "Unknown",
                CorrelationId = ApplicationLogPayloadHelper.Truncate(entry.CorrelationId, 128),
                UserId = ApplicationLogPayloadHelper.Truncate(entry.UserId, 128),
                ContextId = ApplicationLogPayloadHelper.Truncate(entry.ContextId, 256),
                EventType = ApplicationLogPayloadHelper.Truncate(entry.EventType, 128) ?? "ApplicationEvent",
                Result = ApplicationLogPayloadHelper.Truncate(entry.Result, 64) ?? "Unknown",
                Message = ApplicationLogPayloadHelper.Truncate(entry.Message, 2048) ?? string.Empty,
                ExceptionType = ApplicationLogPayloadHelper.Truncate(entry.ExceptionType, 256),
                ExceptionMessage = ApplicationLogPayloadHelper.Truncate(entry.ExceptionMessage, 2048),
                PayloadJson = ApplicationLogPayloadHelper.Truncate(entry.PayloadJson, 4096),
                CreatedUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist booking application log entry.");
        }
    }
}
