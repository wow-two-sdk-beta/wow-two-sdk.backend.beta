using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.PerRow;

/// <summary>Model-builder extensions installing a per-row tenant isolation query filter.</summary>
public static class TenantModelBuilderExtensions
{
    /// <summary>
    /// Installs a named <c>"Tenant"</c> query filter on every <c>IHasTenant&lt;string&gt;</c> entity, restricting
    /// reads to the current tenant (<c>e.TenantId == currentTenant</c>). When no tenant is in scope the filter is
    /// inert (all rows visible) — for system/admin work. Because the filter closes over the AsyncLocal-backed
    /// singleton <see cref="ITenantContext"/>, it is re-evaluated per query and is safe with DbContext pooling.
    /// The named filter co-exists with the default soft-delete filter (EF Core 10 named filters).
    /// Call inside <c>OnModelCreating</c> after <c>base.OnModelCreating(modelBuilder)</c>.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="tenantContext">The ambient tenant context read by the filter.</param>
    /// <returns>The same <see cref="ModelBuilder"/> for chaining.</returns>
    public static ModelBuilder ApplyTenantFilter(this ModelBuilder modelBuilder, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(tenantContext);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IHasTenant<string>).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");

            var currentTenant = Expression.Property(
                Expression.Constant(tenantContext, typeof(ITenantContext)),
                nameof(ITenantContext.TenantId));

            var entityTenant = Expression.Property(
                Expression.Convert(parameter, typeof(IHasTenant<string>)),
                nameof(IHasTenant<string>.TenantId));

            var noTenantInScope = Expression.Equal(currentTenant, Expression.Constant(null, typeof(string)));
            var tenantMatches = Expression.Equal(entityTenant, currentTenant);
            var body = Expression.OrElse(noTenantInScope, tenantMatches);

            var filter = Expression.Lambda(body, parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter("Tenant", filter);
        }

        return modelBuilder;
    }
}
