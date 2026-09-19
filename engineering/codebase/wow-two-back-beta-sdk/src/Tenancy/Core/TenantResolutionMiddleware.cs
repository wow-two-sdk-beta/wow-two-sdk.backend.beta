using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Resolves the tenant for each request and publishes it to the ambient <see cref="ISettableTenantContext"/>.</summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public TenantResolutionMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Resolves and publishes the tenant, then invokes the rest of the pipeline.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="resolver">The tenant resolver.</param>
    /// <param name="tenantContext">The ambient tenant context to publish into.</param>
    /// <param name="tenantStore">The store used to enrich the tenant with metadata.</param>
    public async Task InvokeAsync(HttpContext httpContext, ITenantIdService resolver, ISettableTenantContext tenantContext, ITenantRepository tenantStore)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        tenantContext.Clear();
        try
        {
            var tenantId = await resolver.ResolveAsync(httpContext);
            if (tenantId is not null)
            {
                var tenant = await tenantStore.FindAsync(tenantId, httpContext.RequestAborted);
                tenantContext.Set(tenantId, tenant);
            }

            await _next(httpContext);
        }
        finally
        {
            tenantContext.Clear();
        }
    }
}
