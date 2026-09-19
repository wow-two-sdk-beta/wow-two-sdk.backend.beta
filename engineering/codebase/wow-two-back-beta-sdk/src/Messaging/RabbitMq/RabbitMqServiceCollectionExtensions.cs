using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq.Transports;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;

/// <summary>DI registration for the RabbitMQ event-bus adapter.</summary>
public static class RabbitMqServiceCollectionExtensions
{
    /// <summary>
    /// Register the RabbitMQ transport behind the SDK event bus: send/receive transports (topic exchange + native
    /// DLX/DLQ), the shared <see cref="IEventBus"/>, resilience defaults, topology, and the consumer hosted service.
    /// Scans the supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>, and binds one routing
    /// key per handled type — a service receives only what it handles.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">RabbitMQ options (connection, exchange, queue, DLQ).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    /// <remarks>
    ///   - to reshape the endpoints, call <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> before this
    ///   - the two calls share one consumed-type set
    ///   - only the names back-filled from <see cref="RabbitMqOptions"/> are left to it
    /// </remarks>
    public static IServiceCollection AddRabbitMqEventBus(
        this IServiceCollection services,
        Action<RabbitMqOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<RabbitMqOptions>(
            configure,
            builder => builder
                .Validate(options => Uri.TryCreate(options.ConnectionString, UriKind.Absolute, out _), "RabbitMqOptions.ConnectionString must be an absolute URI.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Exchange), "RabbitMqOptions.Exchange must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Queue), "RabbitMqOptions.Queue must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterExchange), "RabbitMqOptions.DeadLetterExchange must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterQueue), "RabbitMqOptions.DeadLetterQueue must not be empty.")
                .Validate(options => options.PrefetchCount > 0, "RabbitMqOptions.PrefetchCount must be positive.")
                .Validate(options => options.NetworkRecoveryInterval > TimeSpan.Zero, "RabbitMqOptions.NetworkRecoveryInterval must be positive."));

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // Read after the handler scan — a type registered later gets no binding.
        services.AddMessageTopology();

        // Null-coalesce, so an explicit AddMessageTopology(...) still wins.
        services.AddOptions<TopologyOptions>().PostConfigure<RabbitMqOptions>((topology, rabbit) =>
        {
            topology.SharedEndpointName ??= rabbit.Queue;
            topology.SharedDeadLetterQueueName ??= rabbit.DeadLetterQueue;
        });

        services.TryAddSingleton<RabbitMqConnection>();
        services.TryAddSingleton<ITransportCapabilities, RabbitMqCapabilities>();
        services.TryAddSingleton<ISendTransport, RabbitMqSendTransport>();
        services.TryAddSingleton<IReceiveTransport, RabbitMqReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerBackgroundService>();
        return services;
    }
}
