using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.Codes;
using AFH.Common.Errors.Exceptions;
using AFH.Common.Errors.Mapping;
using AFH.Common.Errors.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Function.Middleware;

public sealed class BookingExceptionMapper : IExceptionMapper
{
    private static readonly ErrorCode InvalidJson = new(
        "InvalidJson",
        ErrorCategory.Validation,
        ErrorSeverity.Warning,
        "Request body must be valid JSON with supported date/time values.");

    private static readonly ErrorCode DependencyAuthFailed = new(
        "DependencyAuthFailed",
        ErrorCategory.Dependency,
        ErrorSeverity.Warning,
        "A required downstream service could not complete the request.");

    private static readonly ErrorCode DependencyRejectedRequest = new(
        "DependencyRejectedRequest",
        ErrorCategory.Dependency,
        ErrorSeverity.Warning,
        "A required downstream service could not complete the request.");

    private static readonly ErrorCode DependencyTimeout = new(
        "DependencyTimeout",
        ErrorCategory.Dependency,
        ErrorSeverity.Warning,
        "A required downstream service timed out.");

    private static readonly ErrorCode DependencyUnavailable = new(
        "DependencyUnavailable",
        ErrorCategory.Dependency,
        ErrorSeverity.Warning,
        "A required downstream service could not complete the request.");

    private static readonly ErrorCode ConfigurationError = new(
        "ConfigurationError",
        ErrorCategory.Unknown,
        ErrorSeverity.Error,
        "A required service configuration is missing.");

    public ExceptionMappingResult Map(Exception exception, ErrorContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return TryMap(exception, context)?.MappingResult
            ?? new ExceptionMappingResult
            {
                Exception = exception,
                ErrorCode = CommonErrorCodes.Unexpected,
                Message = exception.Message,
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Context = context
            };
    }

    internal BookingHandledException? TryMap(Exception exception, ErrorContext? context = null)
    {
        if (LooksLikeDeserializationFailure(exception))
        {
            const string message = "Request body must be valid JSON with supported date/time values.";
            var validationErrors = new[]
            {
                new ValidationErrorDetail("body", message, ValidationErrorCodes.InvalidFormat.Value)
            };

            return new BookingHandledException(
                new ExceptionMappingResult
                {
                    Exception = new ValidationException(validationErrors, message, exception),
                    ErrorCode = InvalidJson,
                    Message = message,
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Context = context,
                    ValidationErrors = validationErrors
                },
                "RequestDeserialization",
                null,
                null,
                LogLevel.Warning);
        }

        if (TryGetHttpStatusCode(exception, out var downstreamStatusCode))
        {
            var resolvedStatusCode = downstreamStatusCode!.Value;
            var category = ClassifyDownstreamStatus(resolvedStatusCode);
            var errorCode = category switch
            {
                "AuthOrConfiguration" => DependencyAuthFailed,
                "InvalidRequest" => DependencyRejectedRequest,
                "Timeout" => DependencyTimeout,
                _ => DependencyUnavailable
            };

            return new BookingHandledException(
                new ExceptionMappingResult
                {
                    Exception = exception,
                    ErrorCode = errorCode,
                    Message = "A required downstream service could not complete the request.",
                    StatusCode = (int)(category == "InvalidRequest" ? HttpStatusCode.BadGateway : HttpStatusCode.ServiceUnavailable),
                    Context = context
                },
                "DownstreamDependency",
                category,
                (int)resolvedStatusCode,
                LogLevel.Warning);
        }

        if (exception is TaskCanceledException)
        {
            return new BookingHandledException(
                new ExceptionMappingResult
                {
                    Exception = exception,
                    ErrorCode = DependencyTimeout,
                    Message = "A required downstream service timed out.",
                    StatusCode = (int)HttpStatusCode.GatewayTimeout,
                    Context = context
                },
                "DownstreamDependency",
                "Timeout",
                null,
                LogLevel.Warning);
        }

        if (exception is InvalidOperationException invalidOperation &&
            invalidOperation.Message.Contains("is required", StringComparison.OrdinalIgnoreCase))
        {
            return new BookingHandledException(
                new ExceptionMappingResult
                {
                    Exception = exception,
                    ErrorCode = ConfigurationError,
                    Message = "A required service configuration is missing.",
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Context = context
                },
                "Configuration",
                null,
                null,
                LogLevel.Error);
        }

        return null;
    }

    private static bool LooksLikeDeserializationFailure(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                if (LooksLikeDeserializationFailure(inner))
                {
                    return true;
                }
            }
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is JsonException || current is FormatException)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetHttpStatusCode(Exception exception, out HttpStatusCode? statusCode)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException httpException && httpException.StatusCode.HasValue)
            {
                statusCode = httpException.StatusCode.Value;
                return true;
            }
        }

        statusCode = null;
        return false;
    }

    private static string ClassifyDownstreamStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AuthOrConfiguration",
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "InvalidRequest",
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => "Timeout",
            HttpStatusCode.NotFound => "NotFound",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => "Unavailable",
            _ when (int)statusCode >= 500 => "InternalFailure",
            _ => "Unavailable"
        };
    }

    internal sealed record BookingHandledException(
        ExceptionMappingResult MappingResult,
        string FailureSource,
        string? DownstreamCategory,
        int? DownstreamStatusCode,
        LogLevel Level);
}
