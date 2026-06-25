using AFH.Booking.Domain.Options;

namespace AFH.Booking.Application.Abstractions.Availability;

public interface IAvailabilityRulesRepository
{
    Task<AvailabilityRulesOptions?> GetActiveRulesAsync(CancellationToken ct, string projectContext = "Booking");
}
