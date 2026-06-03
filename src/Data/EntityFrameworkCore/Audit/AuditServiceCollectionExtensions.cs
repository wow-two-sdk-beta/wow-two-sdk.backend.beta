using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;

/// <summary>
/// Registration helpers for the audit interceptor.
/// </summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="AuditInterceptor"/> as a singleton. To wire it into a DbContext,
    /// resolve the interceptor in the context's configuration and call
    /// <see cref="DbContextOptionsBuilder.AddInterceptors(IEnumerable{IInterceptor})"/>.
    /// </summary>
    /// <remarks>
    /// <c>TimeProvider</c> falls back to <see cref="TimeProvider.System"/> if not registered.
    /// Register <see cref="IAuditCurrentUserAccessor"/> if you want <c>CreatedBy</c>/<c>UpdatedBy</c> stamping.
    /// </remarks>
    public static IServiceCollection AddEfCoreAuditInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();
        return services;
    }

    /// <summary>
    /// Registers the audit interceptor and a custom <typeparamref name="TAccessor"/> implementation
    /// for the current-user resolution.
    /// </summary>
    public static IServiceCollection AddEfCoreAuditInterceptor<TAccessor>(this IServiceCollection services)
        where TAccessor : class, IAuditCurrentUserAccessor
    {
        services.AddEfCoreAuditInterceptor();
        services.TryAddSingleton<IAuditCurrentUserAccessor, TAccessor>();
        return services;
    }

    /// <summary>
    /// Convenience extension for wiring the audit interceptor into a <see cref="DbContextOptionsBuilder"/>.
    /// Resolves the interceptor from the application service provider.
    /// </summary>
    public static DbContextOptionsBuilder UseAuditInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var interceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
        return builder.AddInterceptors(interceptor);
    }
}
