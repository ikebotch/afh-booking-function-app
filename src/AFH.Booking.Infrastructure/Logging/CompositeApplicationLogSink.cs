namespace AFH.Booking.Infrastructure.Logging;

internal sealed class CompositeApplicationLogSink : IApplicationLogSink
{
    private readonly IReadOnlyCollection<IApplicationLogSink> _sinks;

    public CompositeApplicationLogSink(params IApplicationLogSink[] sinks)
    {
        _sinks = sinks;
    }

    public async Task WriteAsync(ApplicationLogEntry entry, CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
            await sink.WriteAsync(entry, cancellationToken);
    }
}
