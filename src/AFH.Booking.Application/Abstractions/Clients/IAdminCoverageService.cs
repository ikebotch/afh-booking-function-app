namespace AFH.Booking.Application.Abstractions.Clients;

public interface IAdminCoverageService
{
    Task<object?> GetCoverageAsync(CancellationToken ct);
}
