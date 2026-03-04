namespace AFH.Booking.Domain.Common.Paging;

public sealed class PageResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public string? NextCursor { get; set; }
}
