namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Resolves <see cref="TenantInfo"/> for a tenant id — the registry of known tenants.</summary>
public interface ITenantStore
{
    /// <summary>Finds the tenant with the given id.</summary>
    /// <param name="tenantId">The tenant id to look up.</param>
    /// <param name="cancellationToken">Token to cancel the lookup.</param>
    /// <returns>The tenant metadata, or <see langword="null"/> when unknown.</returns>
    ValueTask<TenantInfo?> FindAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>In-memory <see cref="ITenantStore"/> over a fixed set of tenants (case-insensitive ids). Thread-safe.</summary>
public sealed class InMemoryTenantStore : ITenantStore
{
    private readonly IReadOnlyDictionary<string, TenantInfo> _tenants;

    /// <summary>Creates the store from a known set of tenants.</summary>
    /// <param name="tenants">The tenants to index by id.</param>
    public InMemoryTenantStore(IEnumerable<TenantInfo> tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        _tenants = tenants.ToDictionary(tenant => tenant.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ValueTask<TenantInfo?> FindAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return ValueTask.FromResult(_tenants.GetValueOrDefault(tenantId));
    }
}
