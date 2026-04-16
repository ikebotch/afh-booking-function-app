using AFH.Acs.Recorder.Models.V1;
using Azure.Communication.Identity;

namespace AFH.Acs.Recorder.Services.Acs;

public class IdentityService : IIdentityService
{
    private readonly CommunicationIdentityClient _client;

    public IdentityService(CommunicationIdentityClient client)
    {
        _client = client;
    }

    public async Task<IssueTokenResult> IssueTokenAsync(CancellationToken ct = default)
    {
        var user = await _client.CreateUserAsync(ct);
        var token = await _client.GetTokenAsync(user.Value, new[] { CommunicationTokenScope.VoIP }, ct);

        return new IssueTokenResult(
            IdentityId: user.Value.Id,
            Token: token.Value.Token,
            ExpiresOn: token.Value.ExpiresOn);
    }
}