namespace AFH.Booking.Application.Abstractions.Auth;

public interface IBookingIdentityAdminClient
{
    Task<T?> GetAsync<T>(string path, CancellationToken ct);
    Task<T?> PostAsync<TRequest, T>(string path, TRequest body, CancellationToken ct);
    Task<bool> DeleteAsync(string path, CancellationToken ct);
}
