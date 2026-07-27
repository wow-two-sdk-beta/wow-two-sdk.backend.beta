using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete;

/// <summary>Registration helpers for the soft-delete interceptor.</summary>
public static class SoftDeleteServiceCollectionExtensions
{
    /// <summary>Registers the <see cref="SoftDeleteInterceptor"/> on the pluggable-interceptor seam, so every SDK-registered DbContext attaches it automatically.</summary>
    /// <remarks>Soft-delete runs in DI registration order relative to the other registered interceptors.</remarks>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddEfCoreSoftDeleteFilter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        return services.AddEfInterceptor<SoftDeleteInterceptor>();
    }

    /// <summary>Registers the soft-delete interceptor and a singleton current-user accessor for <c>DeletedBy</c> stamping.</summary>
    /// <typeparam name="TAccessor">The current-user accessor implementation to register.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddEfCoreSoftDeleteFilter<TAccessor>(this IServiceCollection services)
        where TAccessor : class, IAuditCurrentUserAccessor
    {
        services.AddEfCoreSoftDeleteFilter();
        services.TryAddSingleton<IAuditCurrentUserAccessor, TAccessor>();
        return services;
    }

    /// <summary>Wires the soft-delete interceptor into a <see cref="DbContextOptionsBuilder"/> that is not registered through an SDK path.</summary>
    /// <remarks>Idempotent — a no-op when the interceptor is already attached, so it cannot re-enter alongside the auto-wire seam. SDK-registered contexts attach it automatically and need not call this.</remarks>
    /// <param name="builder">The DbContext options builder to configure.</param>
    /// <param name="serviceProvider">The application service provider the interceptor is resolved from.</param>
    public static DbContextOptionsBuilder UseSoftDeleteInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var interceptor = serviceProvider.GetRequiredService<SoftDeleteInterceptor>();
        return builder.HasInterceptor(interceptor) ? builder : builder.AddInterceptors(interceptor);
    }
}
