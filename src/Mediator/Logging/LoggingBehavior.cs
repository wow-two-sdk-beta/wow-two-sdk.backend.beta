using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Logging;

/// <summary>
/// Pipeline behavior that logs the request name + elapsed time at <see cref="LogLevel.Information"/>.
/// Logs failures at <see cref="LogLevel.Error"/>.
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        LogRequestStart(logger, name);

        try
        {
            var response = await next().ConfigureAwait(false);
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
    public static IServiceCollection AddMediatorLoggingBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddMediatorBehavior(typeof(LoggingBehavior<,>));
    }
}
