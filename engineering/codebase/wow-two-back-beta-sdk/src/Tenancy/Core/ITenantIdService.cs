using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Resolves the tenant id for an incoming request.</summary>
public interface ITenantIdService
{
    /// <summary>Resolves the tenant id from the request, or <see langword="null"/> when none applies.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <returns>The resolved tenant id, or <see langword="null"/>.</returns>
    ValueTask<string?> ResolveAsync(HttpContext httpContext);
}
