using AFH.Booking.Contracts.V1.Responses;
using System.Text.Json;

namespace AFH.Booking.Tests;

public sealed class ContractSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ReleaseHoldResponse_SerializesErrorUsingExistingPayloadShape()
    {
        var response = new ReleaseHoldResponse
        {
            Error = new ReleaseHoldError
            {
                Code = "conflict",
                Message = "Confirmed holds cannot be released."
            }
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("\"error\":", json, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"conflict\"", json, StringComparison.Ordinal);
        Assert.Contains("\"message\":\"Confirmed holds cannot be released.\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseHoldResponse_OmitsNullProperties()
    {
        var response = new ReleaseHoldResponse
        {
            BookingId = "hold-123"
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("\"bookingId\":\"hold-123\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"error\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"success\":", json, StringComparison.Ordinal);
    }
}
