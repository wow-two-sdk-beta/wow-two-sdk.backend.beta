using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>DI registration for message observers — the watch-only counterpart to <c>AddConsumeInterceptor&lt;T&gt;()</c>.</summary>
public static class MessageObserverServiceCollectionExtensions
{
    /// <summary>
    /// Register an observer of the message pipeline. <typeparamref name="TObserver"/> may implement any combination of
    /// <see cref="IPublishObservingInterceptor"/>, <see cref="IReceiveObservingInterceptor"/> and <see cref="IConsumeObservingInterceptor"/>: it is added as
    /// a single singleton and surfaced under each interface it implements, so one instance sees every hook it declares.
    /// Calling this twice for the same type notifies that type twice.
    /// </summary>
    /// <typeparam name="TObserver">The observer implementation; must implement at least one observer interface.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="TObserver"/> is abstract, or implements no observer interface — either way the registration would fail or silently do nothing later, so it fails here instead.</exception>
    public static IServiceCollection AddMessageObservingInterceptor<TObserver>(this IServiceCollection services)
        where TObserver : class
    {
        ArgumentNullException.ThrowIfNull(services);

        var observerType = typeof(TObserver);
        if (observerType.IsAbstract)
            throw new InvalidOperationException($"'{observerType}' is abstract or an interface — register a concrete observer type.");

        var observesPublish = typeof(IPublishObservingInterceptor).IsAssignableFrom(observerType);
        var observesReceive = typeof(IReceiveObservingInterceptor).IsAssignableFrom(observerType);
        var observesConsume = typeof(IConsumeObservingInterceptor).IsAssignableFrom(observerType);

        if (!observesPublish && !observesReceive && !observesConsume)
            throw new InvalidOperationException($"'{observerType}' implements none of {nameof(IPublishObservingInterceptor)}, {nameof(IReceiveObservingInterceptor)}, {nameof(IConsumeObservingInterceptor)} — there is nothing to observe.");

        // One concrete singleton, resolved through each facet, so an observer implementing several interfaces keeps one identity (and one piece of state).
        services.TryAddSingleton<TObserver>();

        if (observesPublish)
            services.AddSingleton(static provider => (IPublishObservingInterceptor)provider.GetRequiredService<TObserver>());
        if (observesReceive)
            services.AddSingleton(static provider => (IReceiveObservingInterceptor)provider.GetRequiredService<TObserver>());
        if (observesConsume)
            services.AddSingleton(static provider => (IConsumeObservingInterceptor)provider.GetRequiredService<TObserver>());

        return services;
    }
}
