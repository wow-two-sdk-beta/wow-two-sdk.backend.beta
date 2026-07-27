using Microsoft.Extensions.DependencyInjection;
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
    /// <para>
    /// <b>Call it before <c>AddMessageTopology</c>.</b> A saga consumes through ordinary
    /// <see cref="IEventHandler{TEvent}"/> registrations, and the topology reads the consumed-type set from those
    /// descriptors — registered afterwards, a saga's events get no broker binding and never arrive. Same rule the
    /// handler scan already carries.
    /// </para>
    /// <para>
    /// The machine is constructed here, not by the container: its constructor is the definition, so it must run before
    /// anything can be registered from it. It is then validated — an observed event type with no declared correlation
    /// throws at startup instead of silently dropping messages.
    /// </para>
    /// <para>
    /// Repeat calls for the same machine are idempotent. One state machine per <typeparamref name="TState"/>: the state
    /// type is the key the coordinator, the repository and the instance store are all resolved by.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddInMemoryEventBus(typeof(Program).Assembly);
    /// services.AddSaga&lt;OrderStateMachine, OrderSagaState&gt;();
    /// services.AddSagaRepository&lt;OrderSagaState, EfOrderSagaRepository&gt;();  // optional: durable instead of in-memory
    /// services.AddMessageTopology();                                            // after the sagas
    /// </code>
    /// </example>
    public static IServiceCollection AddSaga<TStateMachine, TState>(this IServiceCollection services, Action<SagaOptions>? configure = null)
        where TStateMachine : SagaStateMachine<TState>, new()
        where TState : class, ISagaState, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        var machine = new TStateMachine();
        machine.Validate();

        var optionsBuilder = services.AddOptions<SagaOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISagaTimeoutScheduler, SagaTimeoutScheduler>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ISagaRepository<>), typeof(InMemorySagaRepository<>)));
        services.TryAddSingleton(machine);
        services.TryAddSingleton<SagaStateMachine<TState>>(machine);
        services.TryAddSingleton<SagaCoordinator<TState>>();

        var dispatchers = GetOrAddDispatcherRegistry(services);
        var typeRegistry = GetOrAddMessageTypeRegistry(services);

        foreach (var eventType in machine.ObservedEventTypes)
        {
            // TryAddEnumerable, not AddTransient: registering the same saga twice would otherwise put two handlers
            // behind one event type and run every transition twice per message.
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
    /// The implementation must honour the optimistic-concurrency contract on <see cref="ISagaRepository{TState}"/>: a
    /// unique key on the correlation id, a version compare on update and delete, and
    /// <see cref="SagaConcurrencyException"/> when either is violated. A repository that silently overwrites turns
    /// concurrent transitions into lost updates, and nothing above it can detect that.
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
