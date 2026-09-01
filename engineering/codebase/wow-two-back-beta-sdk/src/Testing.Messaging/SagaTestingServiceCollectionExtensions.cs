using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>DI registration for the saga test recorder.</summary>
public static class SagaTestingServiceCollectionExtensions
{
    /// <summary>
    /// Wrap one saga's repository in the recorder, so every load / write the coordinator makes is observable. Use this
    /// to watch a host the test built itself; <see cref="SagaTestHarness.StartAsync{TStateMachine,TState}"/> already
    /// does it.
    /// </summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="recorder">The recorder to register — hold the reference before the host exists.</param>
    /// <param name="repository">
    /// Builds the repository under the recorder. Defaults to a fresh <see cref="InMemorySagaRepository{TState}"/>,
    /// which is what <c>AddSaga</c> would have registered; pass a factory to put a durable or fault-injecting
    /// repository underneath instead.
    /// </param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    ///   - call after <c>AddSaga</c> — the closed registration wins over its open-generic default
    ///   - a later <c>AddSagaRepository&lt;TState, …&gt;</c> wins over this one and silences the recorder
    /// </remarks>
    public static IServiceCollection AddSagaRecorder<TState>(
        this IServiceCollection services,
        SagaRecorder<TState> recorder,
        Func<IServiceProvider, ISagaRepository<TState>>? repository = null)
        where TState : class, ISagaState
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(recorder);

        services.TryAddSingleton(recorder);

        // Registers the bracket once — a second registration would nest it and shadow the outer envelope.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConsumeInterceptor, SagaScopingConsumeInterceptor>());

        repository ??= static provider => new InMemorySagaRepository<TState>(provider.GetRequiredService<TimeProvider>());
        services.AddSingleton<ISagaRepository<TState>>(provider => new RecordingSagaRepository<TState>(repository(provider), recorder));
        return services;
    }
}
