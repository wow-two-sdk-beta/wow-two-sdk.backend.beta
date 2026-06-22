using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Provides DI registration for the mediator and handler scanning.</summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IMediator"/> and scans the calling assembly for handlers.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediator(this IServiceCollection services)
        => services.AddMediator(Assembly.GetCallingAssembly());

    /// <summary>Registers <see cref="IMediator"/> and scans the supplied assemblies for closed <see cref="IRequestHandler{TRequest,TResponse}"/> and <see cref="INotificationHandler{TNotification}"/> implementations.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblies">Assemblies scanned for handlers.</param>
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

    /// <summary>Registers an open-generic pipeline behavior. Multiple registrations stack in registration order.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="openGenericBehavior">The open-generic behavior type to register (e.g. <c>typeof(LoggingBehavior&lt;,&gt;)</c>).</param>
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
