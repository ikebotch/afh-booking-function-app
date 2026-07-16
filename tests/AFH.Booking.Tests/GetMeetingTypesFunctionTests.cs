using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Function.Functions.V1.Config;
using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json.Nodes;

namespace AFH.Booking.Tests;

public sealed class GetMeetingTypesFunctionTests
{
    [Fact]
    public async Task Run_ReturnsEmptyMeetingTypesWhenNoConfiguredRowsExist()
    {
        var sut = new GetMeetingTypesFunction(new StubMeetingTypeRepository([]));

        var response = await sut.Run(TestHttpRequestData.Create(), CancellationToken.None);
        var json = await ReadJsonAsync(response);

        var data = GetRequiredObject(json, "data");
        var meetingTypes = GetRequiredArray(data, "meetingTypes");

        Assert.True(GetRequiredValue<bool>(json, "success"));
        Assert.Equal("MeetingTypes", GetRequiredValue<string>(data, "source"));
        Assert.Empty(meetingTypes);
    }

    [Fact]
    public async Task Run_ReturnsConfiguredMeetingTypesWhenPresent()
    {
        var sut = new GetMeetingTypesFunction(new StubMeetingTypeRepository(
        [
            new MeetingTypeRecord
            {
                Code = "Review",
                Label = "Client Review",
                IsDefault = true,
                DefaultDurationMinutes = 45
            }
        ]));

        var response = await sut.Run(TestHttpRequestData.Create(), CancellationToken.None);
        var json = await ReadJsonAsync(response);

        var data = GetRequiredObject(json, "data");
        var meetingTypes = GetRequiredArray(data, "meetingTypes");

        Assert.True(GetRequiredValue<bool>(json, "success"));
        Assert.Equal("MeetingTypes", GetRequiredValue<string>(data, "source"));
        var meetingType = Assert.Single(meetingTypes);
        Assert.Equal("Review", GetRequiredValue<string>(meetingType!, "code"));
        Assert.Equal("Client Review", GetRequiredValue<string>(meetingType!, "label"));
        Assert.Equal(45, GetRequiredValue<int>(meetingType!, "defaultDurationMinutes"));
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

    private static JsonArray GetRequiredArray(JsonNode node, string propertyName)
        => GetRequiredNode(node, propertyName).AsArray();

    private static T GetRequiredValue<T>(JsonNode node, string propertyName)
        => GetRequiredNode(node, propertyName).GetValue<T>();

    private static JsonNode GetRequiredNode(JsonNode node, string propertyName)
    {
        var obj = node.AsObject();
        if (obj.TryGetPropertyValue(propertyName, out var value) && value is not null)
        {
            return value;
        }

        var pascalName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (obj.TryGetPropertyValue(pascalName, out value) && value is not null)
        {
            return value;
        }

        throw new InvalidOperationException($"Expected JSON property '{propertyName}'.");
    }

    private sealed class StubMeetingTypeRepository(IReadOnlyList<MeetingTypeRecord> rows) : IMeetingTypeRepository
    {
        public Task<IReadOnlyList<MeetingTypeRecord>> ListActiveAsync(CancellationToken ct)
            => Task.FromResult(rows);

        public Task<MeetingTypeRecord> UpsertAsync(MeetingTypeUpsert change, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> DeactivateAsync(string code, DateTime changedUtc, CancellationToken ct)
            => throw new NotSupportedException();
    }

}
