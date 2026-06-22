using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Logging;

/// <summary>Logs the request name and elapsed time at <see cref="LogLevel.Information"/>, failures at <see cref="LogLevel.Error"/>.</summary>
/// <param name="logger">The logger that records request start, completion, and failure.</param>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    /// <param name="request">The request flowing through the pipeline.</param>
    /// <param name="nextStep">The continuation that invokes the next behavior or the handler.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextStep);

        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        LogRequestStart(logger, name);

        try
        {
            var response = await nextStep().ConfigureAwait(false);
            LogRequestCompleted(logger, name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            LogRequestFailed(logger, name, sw.ElapsedMilliseconds, ex);
            throw;
        }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "→ {Request}")]
    private static partial void LogRequestStart(ILogger logger, string request);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "← {Request} in {ElapsedMs}ms")]
    private static partial void LogRequestCompleted(ILogger logger, string request, long elapsedMs);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "✕ {Request} failed after {ElapsedMs}ms")]
    private static partial void LogRequestFailed(ILogger logger, string request, long elapsedMs, Exception exception);
}

/// <summary>Registration helper.</summary>
public static class LoggingBehaviorServiceCollectionExtensions
{
    /// <summary>Register the logging pipeline behavior.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorLoggingBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddMediatorBehavior(typeof(LoggingBehavior<,>));
    }
}
