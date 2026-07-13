namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>
/// Ambient access to the current request's tenant. Readable from anywhere — including singleton EF
/// interceptors and query filters — because the default implementation is a singleton backed by an
/// <see cref="System.Threading.AsyncLocal{T}"/> that flows with the request's async context.
/// </summary>
public interface ITenantContext
{
    /// <summary>Gets the current tenant id, or <see langword="null"/> when no tenant is in scope (system/unscoped).</summary>
    string? TenantId { get; }

    /// <summary>Gets the resolved tenant metadata when available, or <see langword="null"/>.</summary>
    TenantInfo? Tenant { get; }

    /// <summary>Gets whether a tenant is currently in scope.</summary>
    bool HasTenant { get; }
}

/// <summary>Write side of <see cref="ITenantContext"/> — set by the resolution middleware (or tests).</summary>
public interface ISettableTenantContext : ITenantContext
{
    /// <summary>Sets the current tenant for the ambient scope.</summary>
    /// <param name="tenantId">The tenant id, or <see langword="null"/> to clear.</param>
    /// <param name="tenant">Optional resolved tenant metadata.</param>
    void Set(string? tenantId, TenantInfo? tenant = null);

    /// <summary>Clears the current tenant from the ambient scope.</summary>
    void Clear();
}
