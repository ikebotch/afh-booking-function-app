using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.System;

public sealed class HealthFunctions
{
    [Function("v1-health")]
    public async Task<HttpResponseData> Health([HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("ok");
        return response;
    }
}
