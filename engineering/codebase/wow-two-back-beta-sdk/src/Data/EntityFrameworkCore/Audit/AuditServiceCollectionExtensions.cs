using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;
using WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;

/// <summary>Registration helpers for the audit interceptor.</summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>Registers the <see cref="AuditInterceptor"/> on the pluggable-interceptor seam, so every SDK-registered DbContext attaches it automatically.</summary>
    /// <remarks><c>TimeProvider</c> falls back to <see cref="TimeProvider.System"/> if not registered; register <see cref="ICurrentUserService"/> if you want <c>CreatedBy</c>/<c>UpdatedBy</c> stamping. Audit runs in DI registration order relative to the other registered interceptors — register a guard interceptor before this call to have it run first.</remarks>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddEfCoreAuditInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        return services.AddEfInterceptor<AuditInterceptor>();
    }

    /// <summary>Registers the audit interceptor and a custom <typeparamref name="TCurrentUser"/> implementation for current-user resolution.</summary>
    /// <typeparam name="TCurrentUser">The current-user service implementation to register.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddEfCoreAuditInterceptor<TCurrentUser>(this IServiceCollection services)
        where TCurrentUser : class, ICurrentUserService
    {
        services.AddEfCoreAuditInterceptor();
        services.TryAddSingleton<ICurrentUserService, TCurrentUser>();
        return services;
    }

    /// <summary>Wires the audit interceptor into a <see cref="DbContextOptionsBuilder"/> that is not registered through an SDK path.</summary>
    /// <remarks>Idempotent — a no-op when the interceptor is already attached, so it cannot double-stamp alongside the auto-wire seam. SDK-registered contexts attach it automatically and need not call this.</remarks>
    /// <param name="builder">The DbContext options builder to configure.</param>
    /// <param name="serviceProvider">The application service provider the interceptor is resolved from.</param>
    public static DbContextOptionsBuilder UseAuditInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var interceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
        return builder.HasInterceptor(interceptor) ? builder : builder.AddInterceptors(interceptor);
    }
}
