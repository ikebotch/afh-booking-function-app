namespace AFH.Booking.Application.Abstractions.Clients;

public interface IPartnerWorkflowPolicyProvider
{
    Task<IReadOnlyList<PartnerWorkflowSendPolicy>> ListAsync(string changeType, CancellationToken ct);
    Task<PartnerWorkflowSendPolicy> GetAsync(string changeType, string? partnerKey, CancellationToken ct);
}

public sealed record PartnerWorkflowSendPolicy(
    bool WorkflowEnabled,
    string NormalizedChangeType,
    string? PartnerKey,
    PartnerWorkflowEndpoint? Endpoint);

public sealed record PartnerWorkflowEndpoint(
    string PartnerKey,
    string DisplayName,
    string? BookingUpdatesUrl,
    string? BaseUrl,
    string BookingUpdatesPath,
    string? ApiKey,
    string ApiKeyHeaderName,
    string IdempotencyKeyHeaderName,
    string PayloadFormat);
