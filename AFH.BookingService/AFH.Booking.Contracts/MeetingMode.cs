using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MeetingMode
{
    Remote,
    InPerson
}
