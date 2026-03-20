using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts.V1.Responses;

public sealed class ReleaseHoldResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Success { get; init; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    //public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BookingId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Error? Error { get; init; }
}


public sealed class Error
{

    public string? Code { get; init; }

    public string? Message { get; init; }

}