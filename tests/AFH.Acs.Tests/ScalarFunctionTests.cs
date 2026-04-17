using System.Net;
using System.Text;
using AFH.Acs.Function.Functions.V1.System;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Acs.Tests;

public sealed class ScalarFunctionTests
{
    [Fact]
    public async Task GetScalarUi_WritesHtmlViaAsyncResponseBody()
    {
        var request = new AcsTestHttpRequestData(new AcsTestFunctionContext(), new Uri("https://localhost/api/v1/scalar"), "GET");
        var sut = new ScalarFunction();

        var response = await sut.GetScalarUi(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Headers.GetValues("Content-Type").Single());

        var html = await ReadBodyAsync(response);
        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AFH ACS API Docs", html, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();
        response.Body.Position = 0;
        return payload;
    }
}

