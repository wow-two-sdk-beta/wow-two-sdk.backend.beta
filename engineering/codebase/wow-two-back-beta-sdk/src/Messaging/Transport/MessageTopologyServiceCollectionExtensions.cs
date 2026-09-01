using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>DI registration for message topology — endpoint naming, the consumed-type set, and the topology provider.</summary>
public static class MessageTopologyServiceCollectionExtensions
{
    /// <summary>
    /// Register the default topology — <see cref="ITopologyService"/>, <see cref="IEndpointNameMapper"/>,
    /// <see cref="TopologyOptions"/> — and record every message type this process consumes.
    /// </summary>
    /// <remarks>
    ///   - call after <c>AddEventHandlersFromAssemblies</c> — a handler registered later gets no binding and never receives
    ///   - repeat calls are additive
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional topology options (style, endpoint prefix, legacy bindings).</param>
    public static IServiceCollection AddMessageTopology(this IServiceCollection services, Action<TopologyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<TopologyOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        // Consumers take the record; the builder above stays for validation and post-configuration.
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<TopologyOptions>>().Value);

        // Registered unconditionally so both entry points share one registry instance, whichever runs first.
        GetOrAddDestinationBindings(services);

        var consumedTypes = GetOrAddConsumedTypes(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType is not { IsGenericType: true, ContainsGenericParameters: false } serviceType)
                continue;

            if (serviceType.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                consumedTypes.Add(serviceType.GetGenericArguments()[0]);
        }

        services.TryAddSingleton<IEndpointNameMapper>(static provider =>
            new DefaultEndpointNameMapper(provider.GetRequiredService<IOptions<TopologyOptions>>().Value.EndpointPrefix));
        services.TryAddSingleton<ITopologyService, TopologyService>();
        return services;
    }

    /// <summary>Replace the endpoint name formatter with a house naming scheme.</summary>
    /// <typeparam name="TFormatter">The formatter implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEndpointNameFormatter<TFormatter>(this IServiceCollection services)
        where TFormatter : class, IEndpointNameMapper
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IEndpointNameMapper, TFormatter>());
        return services;
    }

    /// <summary>
    /// Declare that this process consumes <paramref name="messageType"/> addressed to the logical destination
    /// <paramref name="destination"/>, binding that address alongside the type's own routing key.
    /// </summary>
    /// <remarks>
    ///   - declare an address before sending to it by logical name — an undeclared one is accepted and discarded, never refused
    ///   - the alias binds only where <paramref name="messageType"/> is consumed
    ///   - order against <see cref="AddMessageTopology"/> does not matter
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="destination">The logical destination address, exactly as passed to <see cref="IEventBus.SendAsync{TEvent}"/>.</param>
    /// <param name="messageType">The message contract type sent to that address.</param>
    public static IServiceCollection AddDestinationBinding(this IServiceCollection services, string destination, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(messageType);

        GetOrAddDestinationBindings(services).Add(destination, messageType);
        return services;
    }

    /// <summary>Declare a logical destination this process consumes <typeparamref name="TEvent"/> on.</summary>
    /// <typeparam name="TEvent">The message contract type sent to that address.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="destination">The logical destination address.</param>
    public static IServiceCollection AddDestinationBinding<TEvent>(this IServiceCollection services, string destination)
        where TEvent : class, IEvent
        => services.AddDestinationBinding(destination, typeof(TEvent));

    /// <summary>Replace the topology provider — for content/header routing, a schema registry, or an existing broker layout.</summary>
    /// <typeparam name="TProvider">The provider implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddTopologyProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITopologyService
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<ITopologyService, TProvider>());
        return services;
    }

    private static ConsumedMessageTypeRegistry GetOrAddConsumedTypes(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(ConsumedMessageTypeRegistry) && descriptor.ImplementationInstance is ConsumedMessageTypeRegistry existing)
                return existing;

        var registry = new ConsumedMessageTypeRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static DestinationBindingRegistry GetOrAddDestinationBindings(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(DestinationBindingRegistry) && descriptor.ImplementationInstance is DestinationBindingRegistry existing)
                return existing;

        var registry = new DestinationBindingRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
