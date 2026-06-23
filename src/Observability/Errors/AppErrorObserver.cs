using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Errors;

/// <summary>Records an <see cref="AppError"/> to logs, metrics, and the active trace span — the single error-observability seam.</summary>
public sealed partial class AppErrorObserver(ILogger<AppErrorObserver> logger, IErrorNatureClassifier natures)
{
    private static readonly Meter Meter = new("WoW.Two.Sdk.Errors");
    private static readonly Counter<long> ErrorsTotal = Meter.CreateCounter<long>("errors_total");

    /// <summary>Records <paramref name="error"/> (and its optional <paramref name="exception"/>) across logs, metrics, and the span.</summary>
    /// <param name="error">The error to record.</param>
    /// <param name="exception">The originating exception, when present.</param>
    public void Record(AppError error, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var type = error.Type.ToString();

        ErrorsTotal.Add(1, new KeyValuePair<string, object?>("type", type));

        var activity = Activity.Current;
        activity?.SetStatus(ActivityStatusCode.Error, error.Message);
        if (exception is not null)
        {
            activity?.AddException(exception);
        }

        switch (natures.Classify(error.Type))
        {
            case ErrorNature.Defect:
                LogDefect(type, error.Message, exception);
                break;
            case ErrorNature.Transient:
                LogTransient(type, error.Message, exception);
                break;
            default:
                LogPermanent(type, error.Message, exception);
                break;
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Defect {Type}: {Message}")]
    private partial void LogDefect(string type, string message, Exception? exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Transient failure {Type}: {Message}")]
    private partial void LogTransient(string type, string message, Exception? exception);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Failure {Type}: {Message}")]
    private partial void LogPermanent(string type, string message, Exception? exception);
}

/// <summary>Provides registration for the error-observability seam.</summary>
public static class AppErrorObserverServiceCollectionExtensions
{
    /// <summary>Registers <see cref="AppErrorObserver"/> and the default <see cref="IErrorNatureClassifier"/> it depends on.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddAppErrorObserver(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IErrorNatureClassifier, DefaultErrorNatureClassifier>();
        services.TryAddSingleton<AppErrorObserver>();

        return services;
    }
}
