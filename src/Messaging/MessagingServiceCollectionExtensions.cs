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
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
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
        services.TryAddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();
        GetOrAddMessageTypeRegistry(services); // ensure the type registry singleton exists (populated by the handler/contract scan)
        services.TryAddSingleton<IMessageTypeResolver, DefaultMessageTypeResolver>();
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

    /// <summary>Replace the default System.Text.Json message serializer with another (MessagePack, Protobuf, …).</summary>
    /// <typeparam name="TSerializer">The serializer implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddMessageSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, IMessageSerializer
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IMessageSerializer, TSerializer>());
        return services;
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
