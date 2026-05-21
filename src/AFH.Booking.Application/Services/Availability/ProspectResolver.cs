using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;

namespace AFH.Booking.Application.Availability;

public sealed class ProspectResolver : IProspectResolver
{
    private readonly IClientDirectory _clients;
    private readonly ILogger<ProspectResolver> _logger;

    public ProspectResolver(
        IClientDirectory clients,
        ILogger<ProspectResolver> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    public async Task<(Domain.Client.ClientDirectoryItem? Value, Result<GetAvailabilityResponse>? Error)> ResolveAsync(
        GetAvailabilityQuery query,
        CancellationToken ct)
    {
        if (query.IsRemote)
            return (null, null);

        var leadKey = string.IsNullOrWhiteSpace(query.TransactionId) ? query.ClientId : query.TransactionId;
        if (string.IsNullOrWhiteSpace(leadKey))
        {
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.BadRequest,
                    "transactionId or clientId is required for in-person meetings.",
                    Errors.Validation));
        }

        Domain.Client.ClientDirectoryItem? prospect;
        try
        {
            prospect = await _clients.GetAsync(leadKey.Trim(), ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Leads directory call failed for lookup key {LeadKey}.", leadKey);
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.BadGateway,
                    "Leads service is unavailable. Please try again shortly.",
                    "LeadsServiceUnavailable"));
        }

        if (prospect is null)
        {
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.NotFound,
                    "Client/prospect not found in leads directory.",
                    Errors.NotFound));
        }

        _logger.LogInformation(
            "Booking availability prospect resolved. IsRemote={IsRemote} TransactionId={TransactionId} ProspectLocationResolved={ProspectLocationResolved}",
            false,
            query.TransactionId ?? query.ClientId,
            !string.IsNullOrWhiteSpace(prospect.StreetName1) &&
            !string.IsNullOrWhiteSpace(prospect.Town) &&
            !string.IsNullOrWhiteSpace(prospect.PostalCode));

        return (prospect, null);
    }
}
