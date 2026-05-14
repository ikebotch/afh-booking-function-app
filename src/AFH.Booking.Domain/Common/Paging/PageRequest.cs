namespace AFH.Booking.Domain.Common.Paging;

public sealed class PageRequest
{
    public int Limit { get; set; } = 10;
    public Cursor? Cursor { get; set; }
}
