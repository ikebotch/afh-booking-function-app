using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class ConfigurationApprovalRoutingService : IApprovalRoutingService
{
    private readonly ApprovalRoutingOptions _options;

    public ConfigurationApprovalRoutingService(IOptions<ApprovalRoutingOptions> options)
    {
        _options = options.Value;
    }

    public Task<ApprovalRouteTarget> ResolveAsync(CancellationToken ct)
    {
        return Task.FromResult(new ApprovalRouteTarget(
            string.IsNullOrWhiteSpace(_options.TargetType) ? "Role" : _options.TargetType.Trim(),
            string.IsNullOrWhiteSpace(_options.TargetValue) ? "booking-approvers" : _options.TargetValue.Trim(),
            string.IsNullOrWhiteSpace(_options.DisplayName) ? "Booking Approvers" : _options.DisplayName.Trim()));
    }
}
