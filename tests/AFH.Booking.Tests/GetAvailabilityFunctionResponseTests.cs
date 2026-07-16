using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Models.Common;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Function.Functions.V1.Availability;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json.Nodes;
using AppAvailability = AFH.Booking.Application.Models.Availability;

namespace AFH.Booking.Tests;

public sealed class GetAvailabilityFunctionResponseTests
{
    [Fact]
    public async Task Run_Success_DoesNotDuplicatePagingAtEnvelopeLevel()
    {
        var sut = new GetAvailabilityFunction(
            new StubAvailabilityService(),
            NullLogger<GetAvailabilityFunction>.Instance);

        var request = TestHttpRequestData.Create(method: "POST");
        await WriteBodyAsync(request,
            """
            {
              "clientId": "client-1",
              "preferredStartUtc": "2026-06-20T10:00:00Z",
              "duration": 60,
              "isRemote": true,
              "meetingType": "Review",
              "limit": 10
            }
            """);

        var response = await sut.Run(request, "tx-1", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        Assert.True(GetRequiredValue<bool>(json, "success"));
        Assert.Null(GetOptionalNode(json, "paging"));

        var data = GetRequiredObject(json, "data");
        var paging = GetRequiredObject(data, "paging");
        Assert.Equal(2, GetRequiredValue<int>(paging, "returnedCount"));
        Assert.Equal(10, GetRequiredValue<int>(paging, "pageSize"));
        Assert.False(GetRequiredValue<bool>(paging, "hasMore"));
    }

    private static async Task WriteBodyAsync(TestHttpRequestData request, string json)
    {
        await using var writer = new StreamWriter(request.Body, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync(json);
        await writer.FlushAsync();
        request.Body.Position = 0;
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var text = await reader.ReadToEndAsync();
        return JsonNode.Parse(text)!;
    }

    private static JsonObject GetRequiredObject(JsonNode node, string propertyName)
        => GetRequiredNode(node, propertyName).AsObject();

    private static T GetRequiredValue<T>(JsonNode node, string propertyName)
        => GetRequiredNode(node, propertyName).GetValue<T>();

    private static JsonNode? GetOptionalNode(JsonNode node, string propertyName)
    {
        var obj = node.AsObject();
        if (obj.TryGetPropertyValue(propertyName, out var value))
        {
            return value;
        }

        var pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        return obj.TryGetPropertyValue(pascalName, out value) ? value : null;
    }

    private static JsonNode GetRequiredNode(JsonNode node, string propertyName)
        => GetOptionalNode(node, propertyName)
           ?? throw new InvalidOperationException($"Expected JSON property '{propertyName}'.");

    private sealed class StubAvailabilityService : IAvailabilityService
    {
        public Task<Result<AppAvailability.GetAvailabilityResponse>> HandleAsync(GetAvailabilityQuery query, CancellationToken ct)
            => Task.FromResult(Result<AppAvailability.GetAvailabilityResponse>.Ok(new AppAvailability.GetAvailabilityResponse
            {
                TransactionId = query.TransactionId ?? "tx-1",
                Advisers = [],
                Paging = new PageResult<object>
                {
                    ReturnedCount = 2,
                    PageSize = 10,
                    NextCursor = null
                }
            }));
    }
}
