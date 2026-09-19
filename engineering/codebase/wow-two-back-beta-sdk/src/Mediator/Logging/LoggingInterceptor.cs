using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Logging;

/// <summary>Traces a mediator request and logs its start and successful completion.</summary>
/// <param name="logger">The logger that records request start, completion, and failure.</param>
public sealed partial class LoggingInterceptor<TRequest, TResponse>(ILogger<LoggingInterceptor<TRequest, TResponse>> logger)
    : IRequestInterceptor<TRequest, TResponse>
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
        using var activity = MediatorDiagnosticConstants.Source.StartActivity(name, ActivityKind.Internal);
        if (activity?.IsAllDataRequested is true)
            activity.SetTag("request.type", typeof(TRequest).FullName ?? name);

        var sw = Stopwatch.StartNew();
        LogRequestStart(logger, name);

        var response = await nextStep().ConfigureAwait(false);
        LogRequestCompleted(logger, name, sw.ElapsedMilliseconds);
        return response;
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "→ {Request}")]
    private static partial void LogRequestStart(ILogger logger, string request);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "← {Request} in {ElapsedMs}ms")]
    private static partial void LogRequestCompleted(ILogger logger, string request, long elapsedMs);

}
