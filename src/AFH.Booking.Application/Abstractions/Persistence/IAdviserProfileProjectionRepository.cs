using AFH.Booking.Application.Models.AdviserProjection;

namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IAdviserProfileProjectionRepository
{
    Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct);
    Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct);
    Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct);
    Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct);
}
