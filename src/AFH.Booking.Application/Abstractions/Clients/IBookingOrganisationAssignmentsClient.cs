using AFH.Booking.Application.Models.OrganisationAssignments;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IBookingOrganisationAssignmentsClient
{
    Task<IReadOnlyList<BookingOrganisationAssignment>> GetAssignmentsAsync(
        BookingOrganisationAssignmentSearch search,
        CancellationToken ct);
}
