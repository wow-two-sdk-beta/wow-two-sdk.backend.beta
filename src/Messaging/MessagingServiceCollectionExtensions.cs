using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>DI registration for the messaging abstraction, the in-memory transport, and declarative sagas.</summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory <see cref="IMessageBus"/>, reliability ports (retry / dead-letter / inbox / scheduler),
    /// the consumer hosted service, and scan the supplied assemblies (or the caller's) for <see cref="IMessageHandler{TMessage}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddInMemoryMessaging(this IServiceCollection services, params Assembly[] handlerAssemblies)
        => services.AddInMemoryMessaging(configure: null, handlerAssemblies);

    /// <summary>
    /// Register the in-memory messaging stack with options, and scan the supplied assemblies (or the caller's) for handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options configuration (channel capacity, retry schedule).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddInMemoryMessaging(
        this IServiceCollection services,
        Action<InMemoryMessagingOptions>? configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<InMemoryMessagingOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<InMemoryMessageChannel>();
        services.TryAddSingleton<IInboxStore, InMemoryInboxStore>();
        services.TryAddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
        services.TryAddSingleton<IMessageScheduler, InMemoryMessageScheduler>();
        services.TryAddSingleton<IRetryPolicy, DefaultRetryPolicy>();
        services.TryAddSingleton<IMessageBus, InMemoryMessageBus>();

        var registry = new MessageDispatcherRegistry();
        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        foreach (var assembly in assemblies)
            ScanHandlers(services, registry, assembly);

        services.TryAddSingleton(registry);
        services.AddHostedService<MessageConsumerHostedService>();
        return services;
    }

    /// <summary>
    /// Register a declarative saga: the <see cref="ISagaRunner"/>, the in-process <see cref="ISagaTransport"/>,
    /// the definition itself, and each of its step types (transient).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="definition">A definition built via <see cref="SagaBuilder"/>.</param>
    public static IServiceCollection AddSaga(this IServiceCollection services, SagaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(definition);

        services.TryAddSingleton<ISagaRunner, SagaRunner>();
        services.TryAddSingleton<ISagaTransport, InProcessSagaTransport>();
        services.AddSingleton(definition);

        foreach (var stepType in definition.StepTypes)
            services.TryAddTransient(stepType);

        return services;
    }

    private static void ScanHandlers(IServiceCollection services, MessageDispatcherRegistry registry, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                continue;

            foreach (var handlerInterface in type.GetInterfaces().Where(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)))
            {
                services.AddTransient(handlerInterface, type);

                var messageType = handlerInterface.GetGenericArguments()[0];
                if (registry.Contains(messageType))
                    continue;

                var dispatcherType = typeof(MessageDispatcher<>).MakeGenericType(messageType);
                var dispatcher = (MessageDispatcher)Activator.CreateInstance(dispatcherType)!;
                registry.Register(messageType, dispatcher);
            }
        }
    }
}
