using System.Text.Json.Serialization;

namespace AFH.Booking.Functions.Http;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; init; }

    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(string message, string code) =>
        new() { Success = false, Error = new ApiError { Code = code, Message = message } };
}

public sealed class ApiError
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
}