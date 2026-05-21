namespace AFH.Booking.Application.Models.Common;

public sealed class PageResult<T>
{
    public int? ReturnedCount { get; init; } = 10;
    public int? PageSize { get; init; } = 10;
    public string? NextCursor { get; init; }
    public bool HasMore => !string.IsNullOrWhiteSpace(NextCursor);
}
