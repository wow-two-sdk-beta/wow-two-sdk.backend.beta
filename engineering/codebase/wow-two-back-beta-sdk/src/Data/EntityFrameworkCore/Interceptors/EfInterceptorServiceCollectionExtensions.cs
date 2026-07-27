using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

/// <summary>
/// Pluggable EF Core interceptor registration. An interceptor registered here is auto-wired into every DbContext
/// registered via <c>AddEntityFrameworkCore&lt;TContext&gt;</c> or <c>AddPostgresPersistence&lt;TContext&gt;</c> — so a
/// concern (outbox, auditing, tenant stamping, …) can plug its interceptor from the persistence registration
/// <em>or</em> from its own feature registration (e.g. messaging), without the DbContext having to know about it.
/// </summary>
/// <remarks>
/// Attachment order is DI registration order — register a guard interceptor before the concerns it must precede.
/// Attachment happens in exactly one place, <see cref="EfInterceptorWiring.AddRegisteredInterceptors"/>, and a boot
/// guard fails the host if a registered interceptor never reaches an SDK-registered context.
/// </remarks>
public static class EfInterceptorServiceCollectionExtensions
{
    /// <summary>Register an EF Core interceptor as a singleton so every SDK-registered DbContext auto-wires it. Repeat calls for the same type are a no-op.</summary>
    /// <typeparam name="TInterceptor">An EF Core <see cref="IInterceptor"/> implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEfInterceptor<TInterceptor>(this IServiceCollection services)
        where TInterceptor : class, IInterceptor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<TInterceptor>();

        // TryAddEnumerable de-duplicates on the factory's return type, so a concern registered from two places
        // (e.g. AddPostgresPersistence and the consumer) yields one IInterceptor entry, not two.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IInterceptor, TInterceptor>(static sp => sp.GetRequiredService<TInterceptor>()));

        return services;
    }

    /// <summary>Register an EF Core <see cref="ISaveChangesInterceptor"/> (convenience overload of <see cref="AddEfInterceptor{TInterceptor}"/>).</summary>
    /// <typeparam name="TInterceptor">A SaveChanges interceptor implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEfSaveChangesInterceptor<TInterceptor>(this IServiceCollection services)
        where TInterceptor : class, ISaveChangesInterceptor
        => services.AddEfInterceptor<TInterceptor>();
}
