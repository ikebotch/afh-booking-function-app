using AFH.Booking.Application.Common;
using System.Threading;
using System.Threading.Tasks;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IBookingTokenService
{
    Task<Result<string>> GenerateClientAccessTokenAsync(string bookingId, CancellationToken ct);
}
