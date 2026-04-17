using AFH.Acs.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.System;

public sealed class OpenApiFunction
{
    [Function("v1-openapi-json")]
    public async Task<HttpResponseData> GetJson([HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/openapi.json")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(OpenApiDocumentFactory.CreateJson());
        return response;
    }
}
