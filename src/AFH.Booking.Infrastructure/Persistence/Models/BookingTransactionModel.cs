namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class BookingTransactionModel
{
    // Internal DB identity (PK)
    public string Id { get; set; } = default!;

  
    public string TransactionRef { get; set; } = default!;
    public string? BookingReference { get; set; }
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientAddressLine1 { get; set; }
    public string? ClientAddressLine2 { get; set; }
    public string? ClientTown { get; set; }
    public string? ClientCounty { get; set; }
    public string? ClientPostcode { get; set; }

    public DateTime ProposedStartUtc { get; set; }
    public int DurationMinutes { get; set; }             
    public string Timezone { get; set; } = "Europe/London";

    public bool IsRemote { get; set; }
    public string? MeetingType { get; set; }          


    public string? LocationRef { get; set; }


    public int Status { get; set; }          
    public DateTime CreatedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }


    public byte[] RowVersion { get; set; } = default!;


    public List<BookingSlotModel> Slots { get; set; } = new();
}
