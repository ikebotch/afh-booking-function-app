using AFH.Booking.Contracts.V1.Common;
using AFH.Booking.Contracts.V1.Dtos.Availability;
using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts.V2.Responses;

public sealed class GetAvailabilityResponse
{
    public string TransactionId { get; init; } = default!;
    public IReadOnlyList<AdviserSlotsDto> Items { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PageResultDto<object>? Paging { get; init; }
}
