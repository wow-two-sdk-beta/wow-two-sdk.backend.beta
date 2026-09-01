using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.PerRow;

/// <summary>Registration helper for per-row tenant insert-stamping.</summary>
public static class TenantRowStampingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TenantStampInterceptor"/> so every SDK-registered DbContext auto-stamps the
    /// current tenant onto inserted <c>IHasTenant&lt;string&gt;</c> rows. Requires <c>AddTenancy(...)</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddTenantRowStamping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddEfSaveChangesInterceptor<TenantStampInterceptor>();
    }
}
