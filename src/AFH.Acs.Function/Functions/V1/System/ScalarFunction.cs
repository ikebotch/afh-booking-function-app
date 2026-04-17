using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.System;

public sealed class ScalarFunction
{
    [Function("v1-scalar-ui")]
    public async Task<HttpResponseData> GetScalarUi([HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/scalar")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await response.WriteStringAsync("""
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>AFH ACS API Docs</title>
    <style>
      html, body { height: 100%; margin: 0; font-family: Inter, system-ui, sans-serif; background: #0b1020; color: #e7eefc; }
      main { min-height: 100%; display: grid; place-items: center; padding: 2rem; text-align: center; }
      a { color: #9ad7ff; }
      .card { max-width: 42rem; padding: 2rem; border: 1px solid rgba(255,255,255,0.12); border-radius: 1rem; background: rgba(10,18,35,0.78); box-shadow: 0 24px 80px rgba(0,0,0,0.35); }
      code { background: rgba(255,255,255,0.08); padding: 0.2rem 0.35rem; border-radius: 0.35rem; }
    </style>
  </head>
  <body>
    <main>
      <section class="card">
        <h1>AFH ACS Function API</h1>
        <p>Meeting orchestration, media, and transcription. Open the OpenAPI document at <code>/api/v1/openapi.json</code>.</p>
        <p><a href="/api/v1/openapi.json">View OpenAPI JSON</a></p>
      </section>
    </main>
  </body>
</html>
""");
        return response;
    }
}
