namespace AFH.Booking.Domain;

public sealed record EndpointAccessRequirement(
    EndpointAccessPolicy Policy,
    string? RequiredPermission = null);
