using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.Codes;
using AFH.Common.Errors.Exceptions;
using AFH.Common.Errors.Mapping;
using AFH.Common.Errors.Models;
using System.Net;
using System.Text.Json;

namespace AFH.Acs.Function.Middleware;

public sealed class AcsExceptionMapper : IExceptionMapper
{
    private static readonly ErrorCode ValidationError = new(
        "VALIDATION_ERROR",
        ErrorCategory.Validation,
        ErrorSeverity.Warning,
        "Request body must be valid JSON.");

    private static readonly ErrorCode InternalError = new(
        "INTERNAL_ERROR",
        ErrorCategory.Unknown,
        ErrorSeverity.Error,
        "An unexpected error occurred.");

    public ExceptionMappingResult Map(Exception exception, ErrorContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (LooksLikeDeserializationFailure(exception))
        {
            const string message = "Request body must be valid JSON.";
            var validationErrors = new[]
            {
                new ValidationErrorDetail("body", message, ValidationErrorCodes.InvalidFormat.Value)
            };

            return new ExceptionMappingResult
            {
                Exception = new ValidationException(validationErrors, message, exception),
                ErrorCode = ValidationError,
                Message = message,
                StatusCode = (int)HttpStatusCode.BadRequest,
                Context = context,
                ValidationErrors = validationErrors
            };
        }

        return new ExceptionMappingResult
        {
            Exception = exception,
            ErrorCode = InternalError,
            Message = "An unexpected error occurred.",
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Context = context
        };
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
}
