using AFH.Acs.Application.Abstractions.Identity;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Acs.Function.Functions.V1.Meetings;

public sealed class MeetingIdentityFunction(IIdentityTokenService identityTokens)
{
    [Function("v1-meetings-identity-token")]
    public async Task<HttpResponseData> IssueIdentityTokenAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/meet/identity-token")] HttpRequestData req,
        CancellationToken ct)
    {
        var token = await identityTokens.IssueAsync(ct);
        var result = new IdentityTokenResponse
        {
            IdentityId = token.IdentityId,
            Token = token.Token,
            ExpiresOn = token.ExpiresOn
        };
        var response = req.CreateResponse(global::System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }
}
