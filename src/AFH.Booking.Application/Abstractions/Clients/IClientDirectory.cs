using AFH.Booking.Domain.Client;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IClientDirectory
{
    Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct);
}

