namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>
/// Holds request tenant-resolution configuration. Providers are tried in the order
    /// claim → route → header → subdomain; enable the ones your app trusts. Populate
/// <see cref="KnownTenants"/> to back the default in-memory <see cref="ITenantRepository"/>.
/// </summary>
public sealed record TenancyConventionOptions
{
    /// <summary>Gets or sets whether to read the tenant from a request header. Default <see langword="false"/>; enable only behind a trusted gateway that overwrites the header.</summary>
    public bool UseHeader { get; set; }

    /// <summary>Gets or sets whether to read the tenant from a route value. Default <see langword="false"/>.</summary>
    public bool UseRoute { get; set; }

    /// <summary>Gets or sets whether to read the tenant from an authenticated user claim. Default <see langword="true"/>.</summary>
    public bool UseClaim { get; set; } = true;

    /// <summary>Gets or sets whether to read the tenant from the host's leading sub-domain label. Default <see langword="false"/>; enable only with host validation.</summary>
    public bool UseSubdomain { get; set; }

    /// <summary>Gets or sets the header carrying the tenant id. Default <c>X-Tenant-Id</c>.</summary>
    public string HeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>Gets or sets the route value key carrying the tenant id. Default <c>tenant</c>.</summary>
    public string RouteValueKey { get; set; } = "tenant";

    /// <summary>Gets or sets the claim type carrying the tenant id. Default <c>tenant_id</c>.</summary>
    public string ClaimType { get; set; } = "tenant_id";

    /// <summary>Gets or sets the number of trailing host labels that form the base domain (e.g. <c>example.com</c> = 2, so <c>acme.example.com</c> → <c>acme</c>). Default 2.</summary>
    public int SubdomainBaseLabels { get; set; } = 2;

    /// <summary>Gets the tenants indexed by the default in-memory <see cref="ITenantRepository"/>. Leave empty to supply a custom store.</summary>
    public IList<TenantInfo> KnownTenants { get; } = [];
}
