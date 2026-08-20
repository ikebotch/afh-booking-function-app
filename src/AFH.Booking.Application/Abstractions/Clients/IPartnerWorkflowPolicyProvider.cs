namespace AFH.Booking.Application.Abstractions.Clients;

public interface IPartnerWorkflowPolicyProvider
{
    Task<bool> IsEnabledAsync(string changeType, CancellationToken ct);
}
