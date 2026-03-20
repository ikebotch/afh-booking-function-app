using AFH.Booking.Domain.Client;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IClientEnricher
{
    Task EnrichAsync(ClientDirectoryItem client, CancellationToken ct);
}
