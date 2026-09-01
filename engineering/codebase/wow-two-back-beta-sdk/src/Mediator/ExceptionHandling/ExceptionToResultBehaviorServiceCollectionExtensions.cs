using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Result;
using WoW.Two.Sdk.Backend.Beta.Observability.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.ExceptionHandling;

/// <summary>Provides registration for the terminal exception-to-result pipeline behavior.</summary>
public static class ExceptionToResultBehaviorServiceCollectionExtensions
{
    /// <summary>Registers the terminal behavior that converts handler exceptions into <c>AppResult.Failure</c> (and the <see cref="IExceptionMapper"/> and <c>IErrorRecordingService</c> it depends on). <c>AddMediator</c> registers it first already; call this only on a container that opted out.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorExceptionMappingInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionMapping();

        // The observer logs, so a bare container gets the logging it would otherwise resolve for nothing.
        services.AddLogging();
        services.AddErrorRecordingService();

        return services.AddMediatorInterceptor(typeof(ExceptionMappingInterceptor<,>));
    }
}
