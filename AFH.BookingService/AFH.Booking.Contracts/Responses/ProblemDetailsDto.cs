namespace AFH.Booking.Contracts.Responses;

public sealed record ProblemDetailsDto(
    string Title,
    int Status,
    string Detail,
    string? Instance = null
);
