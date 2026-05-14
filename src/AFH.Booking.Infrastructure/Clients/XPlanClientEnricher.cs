using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Domain.Client;

namespace AFH.Booking.Infrastructure.Clients;


public sealed class XPlanClientEnricher : IClientEnricher
{
    public Task EnrichAsync(ClientDirectoryItem client, CancellationToken ct)
        => Task.CompletedTask;
}
