using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Kafka;

/// <summary>DI registration for the Kafka event-bus adapter.</summary>
public static class KafkaServiceCollectionExtensions
{
    /// <summary>
    /// Register the Kafka transport behind the SDK event bus: send/receive transports (topic + emulated DLQ topic),
    /// the shared <see cref="IEventBus"/>, resilience defaults, topology, and the consumer hosted service. Scans the
    /// supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// </summary>
    /// <remarks>
    ///   - set <see cref="KafkaOptions.RouteByDestination"/> to route each message to its own topic
    ///   - call <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> before this to change the endpoint shape
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Kafka options (bootstrap servers, topic, group, DLQ topic).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddKafkaEventBus(
        this IServiceCollection services,
        Action<KafkaOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<KafkaOptions>().Configure(configure);

        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<KafkaOptions>>().Value);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan: the routed topics come from the registered IEventHandler<> descriptors.
        services.AddMessageTopology();

        // The shared endpoint's name defaults to this adapter's topic, so a reply addressed here resolves to it.
        services.AddOptions<TopologyOptions>().PostConfigure<IOptions<KafkaOptions>>((topology, kafka) =>
        {
            topology.SharedEndpointName ??= kafka.Value.Topic;
            topology.SharedDeadLetterQueueName ??= kafka.Value.DeadLetterTopic;
        });

        services.TryAddSingleton<ITransportCapabilities, KafkaCapabilities>();
        services.TryAddSingleton<ISendTransport, KafkaSendTransport>();
        services.TryAddSingleton<IReceiveTransport, KafkaReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerBackgroundService>();
        return services;
    }
}
