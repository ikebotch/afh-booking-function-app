namespace AFH.Booking.Contracts.V1.Responses;

public sealed class DuplicateClientCaseResponse
{
    public string CaseId { get; init; } = default!;
    public string PrimaryTransactionRef { get; init; } = default!;
    public string DuplicateTransactionRef { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? Notes { get; init; }
    public string? RaisedBy { get; init; }
    public DateTime RaisedUtc { get; init; }
    public string? Resolution { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedUtc { get; init; }
}
