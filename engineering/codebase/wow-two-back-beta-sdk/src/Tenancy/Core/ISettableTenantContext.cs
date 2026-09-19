namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Defines write side of <see cref="ITenantContext"/> — set by the resolution middleware (or tests).</summary>
public interface ISettableTenantContext : ITenantContext
{
    /// <summary>Sets the current tenant for the ambient scope.</summary>
    /// <param name="tenantId">The tenant id, or <see langword="null"/> to clear.</param>
    /// <param name="tenant">Optional resolved tenant metadata.</param>
    void Set(string? tenantId, TenantInfo? tenant = null);

    /// <summary>Clears the current tenant from the ambient scope.</summary>
    void Clear();
}
