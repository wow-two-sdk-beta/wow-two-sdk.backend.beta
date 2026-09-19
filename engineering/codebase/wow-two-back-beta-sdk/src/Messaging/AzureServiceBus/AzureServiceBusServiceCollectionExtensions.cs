using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus.Transports;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus;

/// <summary>DI registration for the Azure Service Bus event-bus adapter.</summary>
public static class AzureServiceBusServiceCollectionExtensions
{
    /// <summary>
    /// Register the Azure Service Bus transport behind the SDK event bus: send/receive transports (topic +
    /// subscription-per-endpoint with correlation filters, native DLQ), the shared <see cref="IEventBus"/>, resilience
    /// defaults, topology, and the consumer hosted service. Scans the supplied assemblies (or the caller's) for
    /// <see cref="IEventHandler{TEvent}"/>, and creates one subscription rule per handled type — a service receives only
    /// what it handles.
    /// </summary>
    /// <remarks>
    ///   - to change the endpoint shape, call <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> before this
    ///   - the two calls share one consumed-type set
    ///   - only the names this method back-fills from <see cref="AzureServiceBusOptions"/> are left to it
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Service Bus options (connection string, topic, subscription, sessions, provisioning).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddAzureServiceBusEventBus(
        this IServiceCollection services,
        Action<AzureServiceBusOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<AzureServiceBusOptions>(
            configure,
            builder => builder
                .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "AzureServiceBusOptions.ConnectionString must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Topic), "AzureServiceBusOptions.Topic must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Subscription), "AzureServiceBusOptions.Subscription must not be empty.")
                .Validate(options => options.MaxDeliveryCount > 0, "AzureServiceBusOptions.MaxDeliveryCount must be positive.")
                .Validate(options => options.LockDuration > TimeSpan.Zero, "AzureServiceBusOptions.LockDuration must be positive.")
                .Validate(options => options.PrefetchCount >= 0, "AzureServiceBusOptions.PrefetchCount must not be negative.")
                .Validate(options => options.MaxMessagesPerReceive > 0, "AzureServiceBusOptions.MaxMessagesPerReceive must be positive.")
                .Validate(options => options.ReceiveWaitTime > TimeSpan.Zero, "AzureServiceBusOptions.ReceiveWaitTime must be positive.")
                .Validate(options => options.MaxConcurrentSessions > 0, "AzureServiceBusOptions.MaxConcurrentSessions must be positive.")
                .Validate(options => options.SessionIdleTimeout > TimeSpan.Zero, "AzureServiceBusOptions.SessionIdleTimeout must be positive.")
                .Validate(options => options.DuplicateDetectionWindow > TimeSpan.Zero, "AzureServiceBusOptions.DuplicateDetectionWindow must be positive."));

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan: rules come from the registered IEventHandler<> descriptors, so a later type gets none.
        services.AddMessageTopology();

        // Back-fill the shared endpoint name from the adapter's options; an explicit AddMessageTopology(...) wins.
        services.AddOptions<TopologyOptions>().PostConfigure<AzureServiceBusOptions>((topology, serviceBus) =>
        {
            topology.SharedEndpointName ??= serviceBus.Subscription;
        });

        services.TryAddSingleton<AzureServiceBusConnection>();
        services.TryAddSingleton<AzureServiceBusTopology>();
        services.TryAddSingleton<ITransportCapabilities, AzureServiceBusCapabilities>();
        services.TryAddSingleton<ISendTransport, AzureServiceBusSendTransport>();
        services.TryAddSingleton<IReceiveTransport, AzureServiceBusReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerBackgroundService>();
        return services;
    }
}
