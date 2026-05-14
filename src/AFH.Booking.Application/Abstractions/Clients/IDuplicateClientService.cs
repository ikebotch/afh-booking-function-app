using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IDuplicateClientService
{
    Task<DuplicateClientCaseResponse> CreateCaseAsync(
        string primaryTransactionRef,
        string duplicateTransactionRef,
        string? notes,
        string? raisedBy,
        CancellationToken ct);

    Task<IReadOnlyList<DuplicateClientCaseResponse>> ListPendingAsync(CancellationToken ct);

    Task<DuplicateClientCaseResponse?> ResolveCaseAsync(
        string caseId,
        string resolution,
        string? resolvedBy,
        string? notes,
        CancellationToken ct);
}
