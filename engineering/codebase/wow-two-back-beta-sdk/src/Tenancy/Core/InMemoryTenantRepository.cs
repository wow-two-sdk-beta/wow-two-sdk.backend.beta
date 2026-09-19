namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Accesses a fixed set of tenants in process memory.</summary>
public sealed class InMemoryTenantRepository : ITenantRepository
{
    private readonly IReadOnlyDictionary<string, TenantInfo> _tenants;

    /// <summary>Creates the store from a known set of tenants.</summary>
    /// <param name="tenants">The tenants to index by id.</param>
    public InMemoryTenantRepository(IEnumerable<TenantInfo> tenants)
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
