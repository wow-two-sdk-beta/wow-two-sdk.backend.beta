using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Application-pipeline helpers for tenant resolution.</summary>
public static class TenancyApplicationBuilderExtensions
{
    /// <summary>Adds the tenant-resolution middleware. Place it after authentication (so claim resolution sees the user) and before endpoints/data access.</summary>
    /// <param name="app">The application pipeline to extend.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
