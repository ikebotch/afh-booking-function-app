using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Domain.Client;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class NoopClientDirectory : IClientDirectory
{
    public Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct)
        => Task.FromResult<ClientDirectoryItem?>(null);
}
