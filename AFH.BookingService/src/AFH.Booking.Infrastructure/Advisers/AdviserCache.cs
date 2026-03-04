using AFH.Booking.Application.Abstractions.Advisers;
using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Infrastructure.Advisers;

public sealed class AdviserCache : IAdviserCache
{
    private readonly IAdviserDirectory _inner;

    public AdviserCache(IAdviserDirectory inner)
    {
        _inner = inner;
    }

    public Task<IReadOnlyList<AdviserDirectoryItem>> ListAsync(CancellationToken ct)
        => _inner.ListAsync(ct);
}
