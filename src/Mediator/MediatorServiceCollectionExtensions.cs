using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>
/// DI registration for the mediator + handler scanning.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IMediator"/> and scan the calling assembly for handlers.
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services)
        => services.AddMediator(Assembly.GetCallingAssembly());

    /// <summary>
    /// Register <see cref="IMediator"/> and scan the supplied assemblies for handlers.
    /// Discovered: closed implementations of <see cref="IRequestHandler{TRequest,TResponse}"/>
    /// and <see cref="INotificationHandler{TNotification}"/>.
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        services.TryAddTransient<IMediator, Mediator>();
        services.TryAddTransient<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddTransient<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        foreach (var assembly in assemblies)
            ScanHandlers(services, assembly);

        return services;
    }

    private static void ScanHandlers(IServiceCollection services, Assembly assembly)
    {
        var openHandlerTypes = new[]
        {
            typeof(IRequestHandler<,>),
            typeof(INotificationHandler<>),
        };

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) continue;

            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = iface.GetGenericTypeDefinition();
                if (Array.IndexOf(openHandlerTypes, def) >= 0)
                {
                    services.AddTransient(iface, type);
                }
            }
        }
    }

    /// <summary>
    /// Register an open-generic pipeline behavior. Multiple registrations stack — execution order is registration order.
    /// </summary>
    public static IServiceCollection AddMediatorBehavior(this IServiceCollection services, Type openGenericBehavior)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(openGenericBehavior);

        if (!openGenericBehavior.IsGenericTypeDefinition)
            throw new ArgumentException("Behavior must be an open generic type (e.g. typeof(LoggingBehavior<,>))", nameof(openGenericBehavior));

        services.AddTransient(typeof(IPipelineBehavior<,>), openGenericBehavior);
        return services;
    }
}
