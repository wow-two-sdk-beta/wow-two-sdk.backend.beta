using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete;

/// <summary>Registration helpers for the soft-delete interceptor.</summary>
public static class SoftDeleteServiceCollectionExtensions
{
    /// <summary>Registers the <see cref="SoftDeleteInterceptor"/> as a singleton. Wire into a DbContext via <see cref="UseSoftDeleteInterceptor"/>.</summary>
    public static IServiceCollection AddEfCoreSoftDeleteFilter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SoftDeleteInterceptor>();
        return services;
    }

    /// <summary>Registers the soft-delete interceptor and a singleton current-user accessor for <c>DeletedBy</c> stamping.</summary>
    public static IServiceCollection AddEfCoreSoftDeleteFilter<TAccessor>(this IServiceCollection services)
        where TAccessor : class, IAuditCurrentUserAccessor
    {
        services.AddEfCoreSoftDeleteFilter();
        services.TryAddSingleton<IAuditCurrentUserAccessor, TAccessor>();
        return services;
    }

    /// <summary>Wires the soft-delete interceptor into a <see cref="DbContextOptionsBuilder"/>.</summary>
    public static DbContextOptionsBuilder UseSoftDeleteInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var interceptor = serviceProvider.GetRequiredService<SoftDeleteInterceptor>();
        return builder.AddInterceptors(interceptor);
    }
}
