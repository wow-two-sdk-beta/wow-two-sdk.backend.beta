using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

/// <summary>Creates ProblemDetails that vary by application error and request context.</summary>
public sealed class AppErrorProblemDetailsFactory(
    IErrorHttpStatusCodeMapper statusMapper,
    IErrorMessageMapper messageMapper,
    IFieldErrorMessageMapper fieldMessageMapper) : IAppErrorProblemDetailsFactory
{
    private const string RetryAfterMetadataKey = "retryAfter";
    private const string WwwAuthenticateMetadataKey = "wwwAuthenticate";

    /// <inheritdoc />
    public Microsoft.AspNetCore.Mvc.ProblemDetails Create(AppError error, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(httpContext);

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
            problem.Extensions["errors"] = ResolveMessages(failures, httpContext);
        }

        PromoteReservedHeaders(error, httpContext);

        return problem;
    }

    private FieldError[] ResolveMessages(
        IReadOnlyList<FieldError> failures,
        HttpContext httpContext)
    {
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
