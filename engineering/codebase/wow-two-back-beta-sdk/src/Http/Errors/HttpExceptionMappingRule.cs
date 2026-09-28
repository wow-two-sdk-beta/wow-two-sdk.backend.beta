using System.Net;
using Polly.CircuitBreaker;
using Polly.Timeout;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Http.Safety;

namespace WoW.Two.Sdk.Backend.Beta.Http.Errors;

/// <summary>
/// Maps outbound HTTP failures to <see cref="AppError"/>: an unreachable, failing, throttling or credential-rejecting
/// dependency is <c>ExternalUnavailable</c> (503) — never a 401 that would sign the caller out — a timeout
/// <c>OperationTimeout</c> and a destination blocked by outbound safety <c>Forbidden</c>. Other client errors stay unmapped.
/// </summary>
public sealed class HttpExceptionMappingRule : IExceptionMappingRule
{
    /// <inheritdoc />
    public AppError? TryMap(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Classify(exception) is { } type
            ? AppError.FromException(type, ErrorMessageConstants.For(type), exception)
            : null;
    }

    /// <summary>The error type for an outbound failure, or null when it is not one.</summary>
    /// <param name="exception">The exception.</param>
    public static AppErrorType? Classify(Exception exception) => exception switch
    {
        OutboundAddressBlockedException => AppErrorType.Forbidden,
        HttpRequestException { StatusCode: { } status } => ForStatus(status),
        HttpRequestException => AppErrorType.ExternalUnavailable,
        TaskCanceledException { InnerException: TimeoutException } => AppErrorType.OperationTimeout,
        TimeoutRejectedException => AppErrorType.OperationTimeout,
        BrokenCircuitException => AppErrorType.ExternalUnavailable,
        _ => null,
    };

    /// <summary>The error type for a dependency's failing status code, or null for statuses the caller must judge.</summary>
    /// <param name="status">The dependency's status code.</param>
    public static AppErrorType? ForStatus(HttpStatusCode status) => (int)status switch
    {
        408 or 504 => AppErrorType.OperationTimeout,
        401 or 403 or 429 or >= 500 => AppErrorType.ExternalUnavailable,
        _ => null,
    };
}
