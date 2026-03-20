namespace AFH.Booking.Contracts.V1.Common;

public sealed class PageResultDto<T>
{

    public int? ReturnedCount { get; init; } = 10;
    public int? PageSize { get; init; } = 10;
    public string? NextCursor { get; init; }
    public bool HasMore => !string.IsNullOrWhiteSpace(NextCursor);
}