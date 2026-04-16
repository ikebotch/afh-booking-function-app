using AFH.Acs.Application.Abstractions;
using AFH.Acs.Domain.Entities;
using Azure.Communication.Identity;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Infrastructure.Acs;

public sealed class AcsJoinTokenIssuer(
    CommunicationIdentityClient identityClient,
    ILogger<AcsJoinTokenIssuer> logger) : IJoinTokenIssuer
{
    public async Task<IssuedJoinToken> IssueForMeetingAsync(MeetingSession session, string displayName, string role, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        logger.LogInformation("Issuing ACS join token for MeetingId={MeetingId} GroupId={GroupId} Role={Role}", session.MeetingId, session.GroupId, role);

        var userResult = await identityClient.CreateUserAsync(cancellationToken: ct);
        var tokenResult = await identityClient.GetTokenAsync(userResult.Value, [CommunicationTokenScope.VoIP], ct);

        return new IssuedJoinToken
        {
            MeetingId = session.MeetingId,
            GroupId = session.GroupId,
            UserId = userResult.Value.Id,
            Token = tokenResult.Value.Token,
            ExpiresOn = tokenResult.Value.ExpiresOn,
            DisplayName = displayName
        };
    }

    public async Task<IssuedIdentityToken> IssueIdentityTokenAsync(CancellationToken ct = default)
    {
        var userResult = await identityClient.CreateUserAsync(cancellationToken: ct);
        var tokenResult = await identityClient.GetTokenAsync(userResult.Value, [CommunicationTokenScope.VoIP], ct);

        return new IssuedIdentityToken
        {
            IdentityId = userResult.Value.Id,
            Token = tokenResult.Value.Token,
            ExpiresOn = tokenResult.Value.ExpiresOn
        };
    }
}
