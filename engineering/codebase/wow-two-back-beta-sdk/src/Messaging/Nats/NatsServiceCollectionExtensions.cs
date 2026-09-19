using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Nats.Transports;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>DI registration for the NATS JetStream event-bus adapter.</summary>
public static class NatsServiceCollectionExtensions
{
    /// <summary>
    /// Register the NATS JetStream transport behind the SDK event bus: send/receive transports (stream subject +
    /// emulated dead-letter subject), the shared <see cref="IEventBus"/>, resilience defaults, topology, and the
    /// consumer hosted service. Scans the supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// </summary>
    /// <remarks>
    ///   - set <see cref="NatsOptions.RouteByDestination"/> to route each message to its own subject
    ///   - call <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> before this to change the endpoint shape
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">NATS options (url, stream, subject, durable consumer, dead-letter subject).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddNatsEventBus(
        this IServiceCollection services,
        Action<NatsOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<NatsOptions>(
            configure,
            builder => builder
                .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out _), "NatsOptions.Url must be an absolute URI.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Stream), "NatsOptions.Stream must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Subject), "NatsOptions.Subject must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.DurableConsumer), "NatsOptions.DurableConsumer must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterSubject), "NatsOptions.DeadLetterSubject must not be empty.")
                .Validate(options => options.MaxDeliver > 0, "NatsOptions.MaxDeliver must be positive."));

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan: the routed subjects come from the registered IEventHandler<> descriptors.
        services.AddMessageTopology();

        // The shared endpoint's name defaults to this adapter's root subject, so a reply addressed here resolves to it.
        services.AddOptions<TopologyOptions>().PostConfigure<NatsOptions>((topology, nats) =>
        {
            topology.SharedEndpointName ??= nats.Subject;
            topology.SharedDeadLetterQueueName ??= nats.DeadLetterSubject;
        });

        services.TryAddSingleton<ITransportCapabilities, NatsCapabilities>();
        services.TryAddSingleton<ISendTransport, NatsSendTransport>();
        services.TryAddSingleton<IReceiveTransport, NatsReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerBackgroundService>();
        return services;
    }
}
