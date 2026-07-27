namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>
/// Default <see cref="ISettableTenantContext"/> — holds the current tenant in an
/// <see cref="System.Threading.AsyncLocal{T}"/> so it flows with the request's async context and is
/// readable from singletons. Register as a singleton (a single instance whose value is per-request-ambient).
/// </summary>
public sealed class AmbientTenantContext : ISettableTenantContext
{
    private readonly AsyncLocal<Holder?> _current = new();

    /// <inheritdoc />
    public string? TenantId => _current.Value?.TenantId;

    /// <inheritdoc />
    public TenantInfo? Tenant => _current.Value?.Tenant;

    /// <inheritdoc />
    public bool HasTenant => _current.Value is not null;

    /// <inheritdoc />
    public void Set(string? tenantId, TenantInfo? tenant = null)
        => _current.Value = string.IsNullOrWhiteSpace(tenantId) ? null : new Holder(tenantId, tenant);

    /// <inheritdoc />
    public void Clear() => _current.Value = null;

    private sealed record Holder(string TenantId, TenantInfo? Tenant);
}
