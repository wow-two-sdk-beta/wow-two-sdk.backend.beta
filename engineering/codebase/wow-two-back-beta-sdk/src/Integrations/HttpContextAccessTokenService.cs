using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Integrations;

/// <summary>Provides the access token by lifting the <c>access_token</c> saved on the current request's auth ticket (<c>SaveTokens = true</c>); yields <c>null</c> with no active context or saved token.</summary>
public sealed class HttpContextAccessTokenService(IHttpContextAccessor httpContextAccessor) : IAccessTokenService
{
    /// <inheritdoc />
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
            return null;

        var token = await context.GetTokenAsync("access_token");
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
