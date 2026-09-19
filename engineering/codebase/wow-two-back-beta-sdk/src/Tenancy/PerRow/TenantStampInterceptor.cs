using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.PerRow;

/// <summary>
/// Stamps the current tenant id onto newly-inserted <see cref="IHasTenant{TTenantId}">IHasTenant&lt;string&gt;</see>
/// entities and validates the stored tenant before updates or deletes. No-op when no tenant is in scope
/// (explicit system operations). Mirrors the audit/soft-delete interceptor pattern.
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
        Enforce(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await EnforceAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private void Enforce(DbContext? context)
    {
        if (context is null) return;

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IHasTenant<string> tenanted)
                continue;

            if (entry.State == EntityState.Added)
            {
                tenanted.TenantId = tenantId;
                continue;
            }

            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            var stored = entry.GetDatabaseValues();
            ValidateStoredTenant(entry.Metadata.ClrType, stored?[nameof(IHasTenant<string>.TenantId)] as string, tenantId);
            if (entry.State == EntityState.Modified)
                PreserveTenant(entry, tenanted, tenantId);
        }
    }

    private async ValueTask EnforceAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null) return;

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IHasTenant<string> tenanted)
                continue;

            if (entry.State == EntityState.Added)
            {
                tenanted.TenantId = tenantId;
                continue;
            }

            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            var stored = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
            ValidateStoredTenant(entry.Metadata.ClrType, stored?[nameof(IHasTenant<string>.TenantId)] as string, tenantId);
            if (entry.State == EntityState.Modified)
                PreserveTenant(entry, tenanted, tenantId);
        }
    }

    private static void PreserveTenant(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        IHasTenant<string> entity,
        string tenantId)
    {
        entity.TenantId = tenantId;
        entry.Property(nameof(IHasTenant<string>.TenantId)).IsModified = false;
    }

    private static void ValidateStoredTenant(Type entityType, string? storedTenantId, string currentTenantId)
    {
        if (storedTenantId is not null && !string.Equals(storedTenantId, currentTenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot write '{entityType.Name}' from tenant '{currentTenantId}' because the stored row belongs to another tenant.");
        }
    }
}
