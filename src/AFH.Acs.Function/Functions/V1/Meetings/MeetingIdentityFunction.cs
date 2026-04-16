using AFH.Acs.Function.Http;
using AFH.Acs.Function.Services.Meetings;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Acs.Function.Functions.V1.Meetings;

public sealed class MeetingIdentityFunction(IMeetingWorkflowStore meetings)
{
    [Function("v1-meetings-identity-token")]
    public async Task<HttpResponseData> IssueIdentityTokenAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/meet/identity-token")] HttpRequestData req,
        CancellationToken ct)
    {
        var result = await meetings.IssueIdentityTokenAsync(ct);
        var response = req.CreateResponse(global::System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }
}
