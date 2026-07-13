using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Resolves the tenant id for an incoming request.</summary>
public interface ITenantResolver
{
    /// <summary>Resolves the tenant id from the request, or <see langword="null"/> when none applies.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <returns>The resolved tenant id, or <see langword="null"/>.</returns>
    ValueTask<string?> ResolveAsync(HttpContext httpContext);
}

/// <summary>
/// Default <see cref="ITenantResolver"/> — tries the providers enabled in
/// <see cref="TenancyConventionOptions"/> in the order header → route → claim → subdomain and returns
/// the first non-empty tenant id.
/// </summary>
public sealed class RequestTenantResolver : ITenantResolver
{
    private readonly TenancyConventionOptions _options;

    /// <summary>Creates the resolver from the tenancy options.</summary>
    /// <param name="options">The tenancy convention options.</param>
    public RequestTenantResolver(IOptions<TenancyConventionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public ValueTask<string?> ResolveAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string? tenantId = null;
        if (_options.UseHeader) tenantId ??= FromHeader(httpContext);
        if (_options.UseRoute) tenantId ??= FromRoute(httpContext);
        if (_options.UseClaim) tenantId ??= FromClaim(httpContext);
        if (_options.UseSubdomain) tenantId ??= FromSubdomain(httpContext);

        return ValueTask.FromResult(string.IsNullOrWhiteSpace(tenantId) ? null : tenantId);
    }

    private string? FromHeader(HttpContext httpContext)
        => httpContext.Request.Headers.TryGetValue(_options.HeaderName, out var value) ? value.ToString() : null;

    private string? FromRoute(HttpContext httpContext)
        => httpContext.Request.RouteValues.TryGetValue(_options.RouteValueKey, out var value) ? value?.ToString() : null;

    private string? FromClaim(HttpContext httpContext)
        => httpContext.User.FindFirst(_options.ClaimType)?.Value;

    private string? FromSubdomain(HttpContext httpContext)
    {
        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrEmpty(host) || IPAddress.TryParse(host, out _)) return null;

        var labels = host.Split('.');
        return labels.Length > _options.SubdomainBaseLabels ? labels[0] : null;
    }
}
