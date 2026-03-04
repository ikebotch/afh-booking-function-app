using System;
using System.Net;

namespace AFH.Booking.Application.Common
{
    /// <summary>
    /// Generic result wrapper for operations that return a value.
    /// </summary>
    public sealed class Result<T>
    {
        public bool IsSuccess { get; init; }
        public T? Value { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
        public HttpStatusCode StatusCode { get; init; }
        public Exception? Exception { get; init; }

        private Result(
            bool success,
            T? value,
            HttpStatusCode status,
            string? message,
            string? code,
            Exception? exception = null)
        {
            IsSuccess = success;
            Value = value;
            StatusCode = status;
            ErrorMessage = message;
            ErrorCode = code;
            Exception = exception;
        }

        /* ---------- SUCCESS ---------- */

        public static Result<T> Ok(T value)
            => new(true, value, HttpStatusCode.OK, null, null);

        /// <summary>
        /// Success with no payload (use T = object or Unit).
        /// </summary>
        public static Result<T> Ok()
            => new(true, default, HttpStatusCode.OK, null, null);

        public static Result<T> Created(T value)
            => new(true, value, HttpStatusCode.Created, null, null);

        public static Result<T> NoContent()
            => new(true, default, HttpStatusCode.NoContent, null, null);

        /* ---------- FAILURES ---------- */

        public static Result<T> NotFound(string msg)
            => new(false, default, HttpStatusCode.NotFound, msg, "NotFound");

        public static Result<T> Unauthorized(string msg = "Unauthorized")
            => new(false, default, HttpStatusCode.Unauthorized, msg, "Unauthorized");

        public static Result<T> BadRequest(string msg, string? code = null)
            => new(false, default, HttpStatusCode.BadRequest, msg, code ?? "BadRequest");

        public static Result<T> Fail(
            HttpStatusCode status,
            string msg,
            string? code = null,
            Exception? ex = null)
            => new(false, default, status, msg, code ?? status.ToString(), ex);

        /* ---------- CONVENIENCE ---------- */

        public static implicit operator Result<T>(T value) => Ok(value);
    }

    /// <summary>
    /// Non-generic result wrapper for operations that return no value.
    /// </summary>
    public sealed class Result
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
        public HttpStatusCode StatusCode { get; init; }
        public Exception? Exception { get; init; }

        private Result(bool success, HttpStatusCode status, string? message, string? code, Exception? exception = null)
        {
            IsSuccess = success;
            StatusCode = status;
            ErrorMessage = message;
            ErrorCode = code;
            Exception = exception;
        }

        /* ---------- SUCCESS ---------- */

        public static Result Ok()
            => new(true, HttpStatusCode.OK, null, null);

        public static Result Created()
            => new(true, HttpStatusCode.Created, null, null);

        public static Result NoContent()
            => new(true, HttpStatusCode.NoContent, null, null);

        /* ---------- FAILURES ---------- */

        public static Result Fail(HttpStatusCode status, string msg, string? code = null, Exception? ex = null)
            => new(false, status, msg, code ?? status.ToString(), ex);

        public static Result Unauthorized(string msg = "Unauthorized")
            => Fail(HttpStatusCode.Unauthorized, msg, "Unauthorized");

        public static Result BadRequest(string msg, string? code = null)
            => Fail(HttpStatusCode.BadRequest, msg, code ?? "BadRequest");

        public static Result NotFound(string msg = "Not Found")
            => Fail(HttpStatusCode.NotFound, msg, "NotFound");
    }
}
