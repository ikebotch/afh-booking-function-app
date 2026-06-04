using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;
using System.Security.Cryptography;
using System.Text;

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

        var lookup = ResolveLeadLookup(query);
        var leadKey = lookup.Reference;
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
            _logger.LogInformation(
                "Booking availability prospect lookup. IsRemote={IsRemote} LookupAttempted={LookupAttempted} LookupSource={LookupSource} LookupRefHash={LookupRefHash}",
                false,
                true,
                lookup.Source,
                HashForLog(leadKey));

            prospect = await _clients.GetAsync(leadKey.Trim(), ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Leads directory call failed. LookupSource={LookupSource} LookupRefHash={LookupRefHash}.",
                lookup.Source,
                HashForLog(leadKey));
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
            "Booking availability prospect resolved. IsRemote={IsRemote} LookupSource={LookupSource} LookupRefHash={LookupRefHash} ProspectLocationResolved={ProspectLocationResolved}",
            false,
            lookup.Source,
            HashForLog(leadKey),
            !string.IsNullOrWhiteSpace(prospect.StreetName1) &&
            !string.IsNullOrWhiteSpace(prospect.Town) &&
            !string.IsNullOrWhiteSpace(prospect.PostalCode));

        return (prospect, null);
    }

    private static (string? Reference, string Source) ResolveLeadLookup(GetAvailabilityQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.ClientLookupRef))
        {
            return (
                query.ClientLookupRef,
                string.IsNullOrWhiteSpace(query.ClientLookupSource)
                    ? "ClientLookupRef"
                    : query.ClientLookupSource);
        }

        if (!string.IsNullOrWhiteSpace(query.TransactionId))
            return (query.TransactionId, "TransactionId");

        return (query.ClientId, "ClientId");
    }

    private static string? HashForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes)[..12];
    }
}
