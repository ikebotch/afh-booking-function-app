namespace AFH.Booking.Application.Models.BusinessContacts;

public sealed record BookingBusinessContactSearch(
    IReadOnlyList<string> ContactTypes,
    string? AdviserId = null,
    string? Region = null,
    string? OrganisationId = null,
    string? ClientId = null);
