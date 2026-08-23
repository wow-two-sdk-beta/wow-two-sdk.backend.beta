using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Creates RFC 9457 ProblemDetails from an <see cref="AppError"/> — the single factory shared by controller failure-arms and the global exception handlers.</summary>
public static class AppErrorProblemDetailsFactory
{
    private const string RetryAfterMetadataKey = "retryAfter";
    private const string WwwAuthenticateMetadataKey = "wwwAuthenticate";

    /// <summary>Builds a ProblemDetails for <paramref name="error"/>, sets the response status, and promotes reserved metadata to response headers; never emits <see cref="AppError.Origin"/>.</summary>
    /// <param name="error">The error to render.</param>
    /// <param name="httpContext">The current request context.</param>
    /// <param name="statusMapper">The error-to-status mapper.</param>
    /// <param name="messageMapper">The mapper for the top-level display message.</param>
    /// <param name="fieldMessageMapper">The mapper for each field message.</param>
    public static Microsoft.AspNetCore.Mvc.ProblemDetails Create(
        AppError error,
        HttpContext httpContext,
        IErrorHttpStatusCodeMapper statusMapper,
        IErrorMessageMapper messageMapper,
        IFieldErrorMessageMapper? fieldMessageMapper = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(statusMapper);
        ArgumentNullException.ThrowIfNull(messageMapper);

        var status = statusMapper.ToStatusCode(error);
        httpContext.Response.StatusCode = status;

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = $"urn:wow-two:error:{error.Type}",
            Status = status,
            Detail = messageMapper.Map(error, httpContext),
        };

        problem.Extensions["code"] = error.Type.ToString();

        var failures = ExtractFailures(error);
        if (failures is not null)
        {
            problem.Extensions["errors"] = ResolveMessages(failures, httpContext, fieldMessageMapper);
        }

        PromoteReservedHeaders(error, httpContext);

        return problem;
    }

    /// <summary>Rewrites each failure's message through <paramref name="fieldMessageMapper"/>, returning the list unchanged when there is none.</summary>
    /// <param name="failures">The field failures to render.</param>
    /// <param name="httpContext">The current request context.</param>
    /// <param name="fieldMessageMapper">The mapper for each field message.</param>
    private static IReadOnlyList<FieldError> ResolveMessages(
        IReadOnlyList<FieldError> failures,
        HttpContext httpContext,
        IFieldErrorMessageMapper? fieldMessageMapper)
    {
        if (fieldMessageMapper is null)
        {
            return failures;
        }

        var resolved = new FieldError[failures.Count];
        for (var index = 0; index < failures.Count; index++)
        {
            var failure = failures[index];
            resolved[index] = failure with { Message = fieldMessageMapper.Map(failure, httpContext) };
        }

        return resolved;
    }

    private static IReadOnlyList<FieldError>? ExtractFailures(AppError error)
    {
        if (error is ValidationError validationError)
        {
            return validationError.Failures;
        }

        if (error is AppAggregateError aggregate)
        {
            return aggregate.Errors
                .OfType<ValidationError>()
                .SelectMany(validation => validation.Failures)
                .ToArray();
        }

        return null;
    }

    private static void PromoteReservedHeaders(AppError error, HttpContext httpContext)
    {
        if (error.Metadata is null)
        {
            return;
        }

        if (TryGetString(error.Metadata, RetryAfterMetadataKey, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = retryAfter;
        }

        if (TryGetString(error.Metadata, WwwAuthenticateMetadataKey, out var wwwAuthenticate))
        {
            httpContext.Response.Headers.WWWAuthenticate = wwwAuthenticate;
        }
    }

    private static bool TryGetString(IReadOnlyDictionary<string, object?> metadata, string key, out string value)
    {
        if (metadata.TryGetValue(key, out var raw) && raw is not null)
        {
            value = raw.ToString() ?? string.Empty;

            return value.Length > 0;
        }

        value = string.Empty;

        return false;
    }
}
