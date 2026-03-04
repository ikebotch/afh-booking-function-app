namespace AFH.Booking.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }
    public HttpStatusCode StatusCode { get; }

    protected Result(bool success, HttpStatusCode status, string? message, string? code)
    {
        IsSuccess = success;
        StatusCode = status;
        ErrorMessage = message;
        ErrorCode = code;
    }

    public static Result Ok()
        => new(true, HttpStatusCode.OK, null, null);

    public static Result Fail(HttpStatusCode status, string msg, string? code = null)
        => new(false, status, msg, code ?? status.ToString());
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool success, T? value, HttpStatusCode status, string? message, string? code)
        : base(success, status, message, code)
    {
        Value = value;
    }

    public static Result<T> Ok(T value)
        => new(true, value, HttpStatusCode.OK, null, null);

    public static Result<T> NotFound(string msg)
        => new(false, default, HttpStatusCode.NotFound, msg, Errors.NotFound);

    public static Result<T> Fail(HttpStatusCode status, string msg, string? code = null)
        => new(false, default, status, msg, code ?? status.ToString());
}
