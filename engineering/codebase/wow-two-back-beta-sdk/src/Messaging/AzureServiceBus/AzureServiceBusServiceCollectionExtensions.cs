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
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

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

        services.AddOptions<AzureServiceBusOptions>().Configure(configure);

        // One shape at the injection site: consumers take the record, the builder keeps validation.
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan: rules come from the registered IEventHandler<> descriptors, so a later type gets none.
        services.AddMessageTopology();

        // Back-fill the shared endpoint name from the adapter's options; an explicit AddMessageTopology(...) wins.
        services.AddOptions<TopologyOptions>().PostConfigure<IOptions<AzureServiceBusOptions>>((topology, serviceBus) =>
        {
            topology.SharedEndpointName ??= serviceBus.Value.Subscription;
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
