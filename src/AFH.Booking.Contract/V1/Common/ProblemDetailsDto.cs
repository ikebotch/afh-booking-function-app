namespace AFH.Booking.Contracts.V1.Common;

public sealed class ProblemDetailsDto
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
}
