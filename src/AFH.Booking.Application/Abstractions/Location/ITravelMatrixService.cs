using AFH.Booking.Domain.Location.Travel;

namespace AFH.Booking.Application.Abstractions.Location;

public interface ITravelMatrixService
{
    Task<TravelMatrixResult> GetAsync(TravelMatrixRequest request, CancellationToken ct);
}

