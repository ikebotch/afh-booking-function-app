using System.Text.Json.Serialization;

namespace AFH.Booking.Function.Http;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiPaging? Paging { get; init; }

    public static ApiResponse<T> Ok(T data, ApiPaging? paging = null) =>
        new() { Success = true, Data = data, Paging = paging };

    public static ApiResponse<T> Fail(T data) =>
        new() { Success = false, Data = data };
}

public sealed class ApiPaging
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}
