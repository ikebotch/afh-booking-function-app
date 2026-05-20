namespace AFH.Booking.Domain.Calendar;


    public sealed class AdviserDirectoryItem
{
    public string AdviserId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Region { get; set; }
    public string? HomePostcode { get; set; }
}
