namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IIntegrationSyncStateRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken ct);
    Task UpsertValueAsync(string key, string value, DateTime updatedUtc, CancellationToken ct);
}
