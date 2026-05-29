namespace AFH.Booking.Application.Models.OrganisationAssignments;

public sealed record BookingOrganisationAssignmentSearch(
    IReadOnlyList<string> AssignmentTypes,
    string? AdviserId = null,
    string? Region = null,
    string? OrganisationId = null,
    string? ClientId = null);
