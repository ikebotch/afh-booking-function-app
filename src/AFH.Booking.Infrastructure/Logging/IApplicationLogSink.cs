namespace AFH.Booking.Infrastructure.Logging;

public interface IApplicationLogSink
{
    Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken = default);
}
