namespace AFH.Booking.Contracts.V1.Requests;

public sealed class CreateDuplicateClientCaseRequest
{
    public string PrimaryTransactionRef { get; init; } = default!;
    public string DuplicateTransactionRef { get; init; } = default!;
    public string? Notes { get; init; }
    public string? RaisedBy { get; init; }
}
