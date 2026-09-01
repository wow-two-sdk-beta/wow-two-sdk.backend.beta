using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>DI registration for the messaging test harness's observer.</summary>
public static class MessagingTestingServiceCollectionExtensions
{
    /// <summary>
    /// Register a <see cref="MessagingRecorder"/> on the bus. Use this to watch a host the test did not build — a
    /// <c>WebApplicationFactory</c>, a broker-backed host — then hand its <c>IServiceProvider</c> to
    /// <see cref="MessagingTestHarness.Attach"/>. <see cref="MessagingTestHarness.StartAsync"/> already does this.
    /// </summary>
    /// <remarks>Idempotent — a second call with a recorder already registered is a no-op.</remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddMessagingRecorder(this IServiceCollection services)
        => services.AddMessagingRecorder(new MessagingRecorder());

    /// <summary>Register a specific <see cref="MessagingRecorder"/> instance — for holding a reference to it before the host exists.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="recorder">The recorder to register.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>Idempotent — a second call with a recorder already registered is a no-op.</remarks>
    public static IServiceCollection AddMessagingRecorder(this IServiceCollection services, MessagingRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(recorder);

        // Registering the observer twice would notify it twice and double every count.
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(MessagingRecorder)))
            return services;

        // Registered before AddMessageObservingInterceptor so every observer facet resolves to this instance.
        services.TryAddSingleton(recorder);
        services.AddMessageObservingInterceptor<MessagingRecorder>();
        return services;
    }
}
