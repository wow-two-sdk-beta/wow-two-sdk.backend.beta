using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.PerRow;

/// <summary>
/// Stamps the current tenant id onto newly-inserted <see cref="IHasTenant{TTenantId}">IHasTenant&lt;string&gt;</see>
/// entities that don't already carry one, so callers never set <c>TenantId</c> by hand. No-op when no tenant
/// is in scope (system operations). Mirrors the audit/soft-delete interceptor pattern.
/// </summary>
public sealed class TenantStampInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    /// <summary>Creates the interceptor.</summary>
    /// <param name="tenantContext">The ambient tenant context supplying the current tenant id.</param>
    public TenantStampInterceptor(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added
                && entry.Entity is IHasTenant<string> tenanted
                && string.IsNullOrEmpty(tenanted.TenantId))
            {
                tenanted.TenantId = tenantId;
            }
        }
    }
}

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
