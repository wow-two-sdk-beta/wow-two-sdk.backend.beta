using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Logging;

/// <summary>Registration helper.</summary>
public static class LoggingBehaviorServiceCollectionExtensions
{
    /// <summary>Register the logging pipeline behavior.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorLoggingInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddMediatorInterceptor(typeof(LoggingInterceptor<,>));
    }
}
