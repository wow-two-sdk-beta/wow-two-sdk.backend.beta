namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>
/// Defines ambient access to the current request's tenant. Readable from anywhere — including singleton EF
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
