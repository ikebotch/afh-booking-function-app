using AFH.Booking.Contracts.Dtos;
using System.ComponentModel.DataAnnotations;

namespace AFH.Booking.Contracts.Requests;
public sealed class CreateHoldRequest
{
    [Required]
    public string AdviserId { get; init; }

    [Required]
    public string CustomerId { get; init; }

    [Required]
    public MeetingMode Mode { get; init; }

    [Required]
    public DateTime StartUtc { get; init; }

    [Required]
    public DateTime EndUtc { get; init; }

    [Required]
    public string Timezone { get; init; }

    public string? Notes { get; init; }
    public string Subject { get; init; }
    public TimeSpan HoldDuration { get; init; }
    public string TransactionId { get; init; }
    public LocationDto Location { get; set; }


    public bool IsRemote { get; set; }
    public IEnumerable<string>? Categories { get; set; }
    public CalendarImportance Importance { get; set; } = CalendarImportance.Normal;
}
