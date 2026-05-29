using AFH.Booking.Application.Models.Notifications;

namespace AFH.Booking.Application.Models.OrganisationAssignments;

public sealed record BookingOrganisationAssignment(
    string AssignmentType,
    string DisplayName,
    string? Email,
    string? MobileNumber,
    IReadOnlyList<BookingNotificationChannel> Channels);
