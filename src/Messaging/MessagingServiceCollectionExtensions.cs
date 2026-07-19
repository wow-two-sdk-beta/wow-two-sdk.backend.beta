using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>
/// DI registration for the event bus, the in-memory transport, reliability, and declarative event sagas.
/// The pieces register independently — compose them, or use <see cref="AddInMemoryEventBus(IServiceCollection, Assembly[])"/>
/// for the batteries-included default.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>Batteries-included in-memory event bus: handlers + in-memory reliability + in-memory transport.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddInMemoryEventBus(this IServiceCollection services, params Assembly[] handlerAssemblies)
        => services.AddInMemoryEventBus(configure: null, handlerAssemblies);

    /// <summary>Batteries-included in-memory event bus with options.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options (channel capacity, retry schedule).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddInMemoryEventBus(
        this IServiceCollection services,
        Action<InMemoryEventBusOptions>? configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];

        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddInMemoryReliability();
        services.AddInMemoryEventTransport(configure);
        return services;
    }

    /// <summary>Register the in-memory transport only — the channel, the <see cref="IEventBus"/>, and the consumer hosted service. Pair with <see cref="AddInMemoryReliability"/> + <see cref="AddEventHandlersFromAssemblies"/> (or reuse a broker's reliability).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options.</param>
    public static IServiceCollection AddInMemoryEventTransport(this IServiceCollection services, Action<InMemoryEventBusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<InMemoryEventBusOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<InMemoryEventChannel>();
        services.TryAddSingleton<IEventScheduler, InMemoryEventScheduler>();
        services.TryAddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
        services.TryAddSingleton<ISendTransport, InMemorySendTransport>();
        services.TryAddSingleton<IReceiveTransport, InMemoryReceiveTransport>();
        services.TryAddSingleton<ITransportCapabilities, InMemoryCapabilities>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddMessagePump();
        GetOrAddRegistry(services); // ensure the dispatcher registry exists even with no handlers registered yet
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }

    /// <summary>Register the transport-neutral resilience defaults — <see cref="IRetryPolicy"/>, <see cref="IEventResiliencePipeline"/>, <see cref="IInboxProcessor"/>. Reused by every transport (in-memory + broker adapters).</summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEventResilienceDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<InMemoryEventBusOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRetryPolicy, DefaultRetryPolicy>();
        services.TryAddSingleton<IEventResiliencePipeline, DefaultEventResiliencePipeline>();
        services.TryAddSingleton<IInboxProcessor, InMemoryInboxProcessor>();
        services.TryAddSingleton<IEventFaultClassifier>(DefaultEventFaultClassifier.RetryAll); // every exception retries until rules are registered
        services.TryAddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();
        services.TryAddSingleton<MessageSerializerRegistry>(); // selects the deserializer by the received wt-content-type; one registered serializer = today's behaviour
        GetOrAddMessageTypeRegistry(services); // ensure the type registry singleton exists (populated by the handler/contract scan)
        services.TryAddSingleton<IMessageTypeResolver, DefaultMessageTypeResolver>();
        services.AddMessagePump();
        return services;
    }

    /// <summary>
    /// Configure consume-side concurrency — how many messages the pump processes in parallel, whether messages sharing a
    /// <see cref="EventEnvelope.PartitionKey"/> stay ordered, and the shutdown drain budget. Without this the pump runs
    /// with <see cref="ConcurrencyOptions.MaxConcurrentMessages"/> = 1, dispatching inline exactly as before it existed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Concurrency options.</param>
    public static IServiceCollection AddMessagingConcurrency(this IServiceCollection services, Action<ConcurrencyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<ConcurrencyOptions>().Configure(configure);
        services.AddMessagePump();
        return services;
    }

    // Every transport routes its receive loop through the pump; TryAdd keeps the 4 registration paths idempotent.
    private static IServiceCollection AddMessagePump(this IServiceCollection services)
    {
        services.AddOptions<ConcurrencyOptions>();
        services.TryAddSingleton<MessagePump>();
        services.AddMessagingMetrics(); // pipeline, bus and pump all take IMessagingMetrics — every transport path lands here
        services.TryAddSingleton<IMessageHeaderPropagationPolicy>(MessageHeaderPropagationPolicy.Default);
        services.TryAddSingleton<BusControl>();
        services.TryAddSingleton<IBusControl>(static sp => sp.GetRequiredService<BusControl>()); // hosted service needs the concrete type for lifecycle stamps; same instance behind the port
        return services;
    }

    /// <summary>Register the in-memory reliability defaults — resilience (<see cref="AddEventResilienceDefaults"/>) plus the in-memory <see cref="IDeadLetterStore"/> and <see cref="IEventScheduler"/>. A broker adapter uses its native DLQ/scheduling instead.</summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddInMemoryReliability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEventResilienceDefaults();
        services.TryAddSingleton<InMemoryEventChannel>();
        services.TryAddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
        services.TryAddSingleton<IEventScheduler, InMemoryEventScheduler>();
        return services;
    }

    /// <summary>Scan assemblies for <see cref="IEventHandler{TEvent}"/> implementations and register them + their dispatchers. Callable repeatedly to add more assemblies incrementally.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="handlerAssemblies">Assemblies to scan; defaults to the calling assembly.</param>
    public static IServiceCollection AddEventHandlersFromAssemblies(this IServiceCollection services, params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var registry = GetOrAddRegistry(services);
        var typeRegistry = GetOrAddMessageTypeRegistry(services);
        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        foreach (var assembly in assemblies)
        {
            ScanHandlers(services, registry, typeRegistry, assembly);
            RegisterEventContracts(typeRegistry, assembly); // stable wire tokens for every IEvent contract (published + handled)
        }

        return services;
    }

    /// <summary>
    /// Register a declarative event saga: the <see cref="IEventSagaRunner"/>, the in-process <see cref="IEventSagaTransport"/>,
    /// the definition itself, and each of its step types (transient).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="definition">A definition built via <see cref="EventSagaBuilder"/>.</param>
    public static IServiceCollection AddEventSaga(this IServiceCollection services, EventSagaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(definition);

        services.TryAddSingleton<IEventSagaRunner, EventSagaRunner>();
        services.TryAddSingleton<IEventSagaTransport, InProcessEventSagaTransport>();
        services.AddSingleton(definition);

        foreach (var stepType in definition.StepTypes)
            services.TryAddTransient(stepType);

        return services;
    }

    /// <summary>
    /// Make <typeparamref name="TSerializer"/> <b>the default</b> — the serializer every message is sent with, replacing
    /// System.Text.Json. Pair with <see cref="AddReceiveOnlyMessageSerializer{TSerializer}"/> to keep decoding a format
    /// this service no longer sends.
    /// </summary>
    /// <remarks>
    /// Swaps the <em>default</em> descriptor specifically — the last <see cref="IMessageSerializer"/> registered, which is
    /// the one MS DI resolves for the singular service. <c>Replace</c> removes the <em>first</em> match instead, which with
    /// a receive-only serializer registered would drop that format and leave the default untouched — the exact inverse of
    /// what the call says. With only the shipped default registered the two are the same descriptor.
    /// </remarks>
    /// <typeparam name="TSerializer">The serializer implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddMessageSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, IMessageSerializer
    {
        ArgumentNullException.ThrowIfNull(services);

        var defaultIndex = LastIndexOfMessageSerializer(services);
        if (defaultIndex >= 0)
            services.RemoveAt(defaultIndex);

        services.Add(ServiceDescriptor.Singleton<IMessageSerializer, TSerializer>()); // last = the default DI resolves
        return services;
    }

    /// <summary>
    /// Make <typeparamref name="TSerializer"/>'s format <b>understood on receive</b> without making it the default — the
    /// send path keeps whatever <see cref="AddMessageSerializer{TSerializer}"/> (or the shipped default) put there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what fills <see cref="MessageSerializerRegistry"/>. The registry selects a deserializer by the received
    /// <c>wt-content-type</c>, but it is built from <c>IEnumerable&lt;IMessageSerializer&gt;</c> — and with
    /// <see cref="AddMessageSerializer{TSerializer}"/> as the only registration path that enumerable could never hold more
    /// than one entry, so the selection had nothing to select between. Call this once per extra format a producer on this
    /// stream might send.
    /// </para>
    /// <para>
    /// The descriptor is inserted <em>ahead of</em> the default rather than appended, because MS DI resolves the singular
    /// <see cref="IMessageSerializer"/> to the last descriptor: appending would silently hand the send path to a serializer
    /// registered for receive. Registering the default first is for the same reason — <c>TryAddSingleton</c> in
    /// <see cref="AddEventResilienceDefaults"/> matches on service type alone, so a bare call placed ahead of it would
    /// suppress the System.Text.Json default entirely. Repeat calls for one implementation type are no-ops.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSerializer">The serializer implementation whose <see cref="IMessageSerializer.ContentType"/> becomes decodable.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddReceiveOnlyMessageSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, IMessageSerializer
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>(); // guarantee a default to sit behind
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(IMessageSerializer) && descriptor.ImplementationType == typeof(TSerializer))
                return services; // already decodable — as the default or from an earlier call

        services.Insert(LastIndexOfMessageSerializer(services), ServiceDescriptor.Singleton<IMessageSerializer, TSerializer>());
        return services;
    }

    // The default is positional: MS DI hands the singular IMessageSerializer to the LAST registered descriptor, and the
    // registry takes that same instance as its fallback. Both registration paths pivot on this index, never on the first
    // match, so a receive-only serializer can never be mistaken for the one that sends.
    private static int LastIndexOfMessageSerializer(IServiceCollection services)
    {
        for (var index = services.Count - 1; index >= 0; index--)
            if (services[index].ServiceType == typeof(IMessageSerializer))
                return index;

        return -1;
    }

    /// <summary>Replace the default message-type resolver (e.g. a URN- or schema-registry-backed one).</summary>
    /// <typeparam name="TResolver">The resolver implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddMessageTypeResolver<TResolver>(this IServiceCollection services)
        where TResolver : class, IMessageTypeResolver
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IMessageTypeResolver, TResolver>());
        return services;
    }

    /// <summary>Register an explicit stable wire token for an event type — for producer-only contracts, a custom URN, or a rename alias.</summary>
    /// <typeparam name="TEvent">The event contract type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="token">The stable wire token.</param>
    public static IServiceCollection MapMessageType<TEvent>(this IServiceCollection services, string token)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(token);
        GetOrAddMessageTypeRegistry(services).Register(typeof(TEvent), token);
        return services;
    }

    /// <summary>Replace the header-propagation policy — which of a consumed message's headers flow onto messages published while handling it. Defaults to W3C trace context only.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">The policy, e.g. <c>MessageHeaderPropagationPolicy.Default.Allow("tenant-id")</c>.</param>
    public static IServiceCollection AddMessageHeaderPropagation(this IServiceCollection services, IMessageHeaderPropagationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(policy);
        services.Replace(ServiceDescriptor.Singleton(policy));
        return services;
    }

    /// <summary>Register a consume-pipeline filter — ordered by registration, runs once per message around the resilience/dedupe/dispatch core (fault-publish, wire-tap, claim-check, rate-limit, …).</summary>
    /// <typeparam name="TFilter">The filter implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddConsumeFilter<TFilter>(this IServiceCollection services)
        where TFilter : class, IConsumeFilter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IConsumeFilter, TFilter>();
        return services;
    }

    private static EventDispatcherRegistry GetOrAddRegistry(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(EventDispatcherRegistry) && descriptor.ImplementationInstance is EventDispatcherRegistry existing)
                return existing;

        var registry = new EventDispatcherRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static MessageTypeRegistry GetOrAddMessageTypeRegistry(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(MessageTypeRegistry) && descriptor.ImplementationInstance is MessageTypeRegistry existing)
                return existing;

        var registry = new MessageTypeRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static void RegisterEventContracts(MessageTypeRegistry typeRegistry, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
            if (type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } && typeof(IEvent).IsAssignableFrom(type))
                typeRegistry.Register(type);
    }

    private static void ScanHandlers(IServiceCollection services, EventDispatcherRegistry registry, MessageTypeRegistry typeRegistry, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                continue;

            foreach (var handlerInterface in type.GetInterfaces().Where(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)))
            {
                services.AddTransient(handlerInterface, type);

                var eventType = handlerInterface.GetGenericArguments()[0];
                typeRegistry.Register(eventType);
                if (registry.Contains(eventType))
                    continue;

                var dispatcherType = typeof(EventDispatcher<>).MakeGenericType(eventType);
                var dispatcher = (EventDispatcher)Activator.CreateInstance(dispatcherType)!;
                registry.Register(eventType, dispatcher);
            }
        }
    }
}
