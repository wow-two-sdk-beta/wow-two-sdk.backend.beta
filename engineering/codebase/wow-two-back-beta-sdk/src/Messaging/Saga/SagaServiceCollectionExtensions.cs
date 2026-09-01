using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>DI registration for state-machine sagas — the machine, its repository, and one event handler per observed event type.</summary>
public static class SagaServiceCollectionExtensions
{
    /// <summary>
    /// Register a state-machine saga: the machine itself, the in-memory <see cref="ISagaRepository{TState}"/> default,
    /// the timeout scheduler, and an <see cref="IEventHandler{TEvent}"/> for every event the machine observes.
    /// </summary>
    /// <typeparam name="TStateMachine">The state machine.</typeparam>
    /// <typeparam name="TState">The persisted instance state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional saga runtime options (concurrency retries, finalization).</param>
    /// <remarks>
    ///   - Call before <c>AddMessageTopology</c> — a saga registered after it gets no broker binding and never arrives
    ///   - repeat calls are idempotent
    ///   - one state machine serves one <typeparamref name="TState"/>
    /// </remarks>
    public static IServiceCollection AddSaga<TStateMachine, TState>(this IServiceCollection services, Action<SagaOptions>? configure = null)
        where TStateMachine : SagaStateMachine<TState>, new()
        where TState : class, ISagaState, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        var machine = new TStateMachine();
        machine.Validate();

        services.AddOptions<SagaOptions>().Configure(options => configure?.Invoke(options));
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<SagaOptions>>().Value);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISagaTimeoutService, SagaTimeoutService>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ISagaRepository<>), typeof(InMemorySagaRepository<>)));
        services.TryAddSingleton(machine);
        services.TryAddSingleton<SagaStateMachine<TState>>(machine);
        services.TryAddSingleton<SagaCoordinator<TState>>();

        var dispatchers = GetOrAddDispatcherRegistry(services);
        var typeRegistry = GetOrAddMessageTypeRegistry(services);

        foreach (var eventType in machine.ObservedEventTypes)
        {
            // Deduplicated, so registering the same saga twice still runs each transition once per message.
            services.TryAddEnumerable(ServiceDescriptor.Transient(
                typeof(IEventHandler<>).MakeGenericType(eventType),
                typeof(SagaEventHandler<,>).MakeGenericType(typeof(TState), eventType)));

            // The wire token, so a saga's events survive the round trip through a broker like any other contract.
            typeRegistry.Register(eventType);

            if (dispatchers.Contains(eventType))
                continue;

            var dispatcher = (EventDispatcher)Activator.CreateInstance(typeof(EventDispatcher<>).MakeGenericType(eventType))!;
            dispatchers.Register(eventType, dispatcher);
        }

        return services;
    }

    /// <summary>Replace the in-memory saga repository for one state type with a durable one.</summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <typeparam name="TRepository">The repository implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">Repository lifetime. Scoped by default — a DbContext-backed repository belongs to the message's scope.</param>
    /// <remarks>
    ///   - Guard the correlation id with a unique key, so a second insert loses
    ///   - Compare the stored <see cref="ISagaState.Version"/> on update and delete
    ///   - Throw <see cref="SagaConcurrencyException"/> on either violation
    /// </remarks>
    public static IServiceCollection AddSagaRepository<TState, TRepository>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TState : class, ISagaState
        where TRepository : class, ISagaRepository<TState>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Describe(typeof(ISagaRepository<TState>), typeof(TRepository), lifetime));
        return services;
    }

    private static EventDispatcherRegistry GetOrAddDispatcherRegistry(IServiceCollection services)
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
}
