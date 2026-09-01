using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>DI registration for the Redis Streams event-bus adapter.</summary>
public static class RedisStreamsServiceCollectionExtensions
{
    /// <summary>
    /// Register the Redis Streams transport behind the SDK event bus: send/receive transports (stream + consumer group
    /// + emulated dead-letter stream), the shared <see cref="IEventBus"/>, resilience defaults, topology, and the
    /// consumer hosted service. Scans the supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Redis options (configuration string, stream, consumer group, dead-letter stream, trimming, claim tuning).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    /// <remarks>
    ///   - to reshape the endpoints, call <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> before this
    ///   - the two calls share one consumed-type set
    ///   - only the names back-filled from <see cref="RedisStreamsOptions"/> are left to it
    /// </remarks>
    public static IServiceCollection AddRedisStreamsEventBus(
        this IServiceCollection services,
        Action<RedisStreamsOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<RedisStreamsOptions>().Configure(configure);

        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<RedisStreamsOptions>>().Value);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // Read after the handler scan — a type registered later gets no stream.
        services.AddMessageTopology();

        // Back-fill the shared endpoint name from the adapter's stream; an explicit AddMessageTopology(...) still wins.
        services.AddOptions<TopologyOptions>().PostConfigure<IOptions<RedisStreamsOptions>>((topology, redis) =>
        {
            topology.SharedEndpointName ??= redis.Value.Stream;
            topology.SharedDeadLetterQueueName ??= redis.Value.DeadLetterStream;
        });

        services.TryAddSingleton<RedisStreamsConnection>();
        services.TryAddSingleton<ITransportCapabilities, RedisStreamsCapabilities>();
        services.TryAddSingleton<ISendTransport, RedisStreamsSendTransport>();
        services.TryAddSingleton<IReceiveTransport, RedisStreamsReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerBackgroundService>();
        return services;
    }
}
