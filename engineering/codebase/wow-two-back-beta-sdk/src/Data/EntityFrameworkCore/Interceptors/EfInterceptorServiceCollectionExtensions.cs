using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

/// <summary>
/// Pluggable EF Core interceptor registration. An interceptor registered here is auto-wired into every DbContext
/// registered via <c>AddEntityFrameworkCore&lt;TContext&gt;</c> — so a concern (outbox, auditing, …) can plug its
/// interceptor from the persistence registration <em>or</em> from its own feature registration (e.g. messaging),
/// without the DbContext having to know about it.
/// </summary>
public static class EfInterceptorServiceCollectionExtensions
{
    /// <summary>Register an EF Core interceptor as a singleton so every SDK-registered DbContext auto-wires it.</summary>
    /// <typeparam name="TInterceptor">An EF Core <see cref="IInterceptor"/> implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEfInterceptor<TInterceptor>(this IServiceCollection services)
        where TInterceptor : class, IInterceptor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<TInterceptor>();
        services.AddSingleton<IInterceptor>(static sp => sp.GetRequiredService<TInterceptor>());
        return services;
    }

    /// <summary>Register an EF Core <see cref="ISaveChangesInterceptor"/> (convenience overload of <see cref="AddEfInterceptor{TInterceptor}"/>).</summary>
    /// <typeparam name="TInterceptor">A SaveChanges interceptor implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEfSaveChangesInterceptor<TInterceptor>(this IServiceCollection services)
        where TInterceptor : class, ISaveChangesInterceptor
        => services.AddEfInterceptor<TInterceptor>();
}
