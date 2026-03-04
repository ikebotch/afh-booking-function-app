using AFH.Booking.Domain.Calendar;

namespace AFH.Booking.Application.Abstractions.Advisers;

public interface IAdviserDirectory
{
    Task<IReadOnlyList<AdviserDirectoryItem>> ListAsync(CancellationToken ct);
}

