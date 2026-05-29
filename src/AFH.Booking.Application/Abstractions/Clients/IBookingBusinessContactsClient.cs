using AFH.Booking.Application.Models.BusinessContacts;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IBookingBusinessContactsClient
{
    Task<IReadOnlyList<BookingBusinessContact>> GetContactsAsync(
        BookingBusinessContactSearch search,
        CancellationToken ct);
}
