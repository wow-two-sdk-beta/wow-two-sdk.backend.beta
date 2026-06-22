using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Integrations;

/// <summary>Supplies the access token an integration client authorizes its calls with; swap the implementation to control which token each call carries.</summary>
public interface IAccessTokenProvider
{
    /// <summary>Resolves the current access token, or <c>null</c> when none is available.</summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The access token, or <c>null</c>.</returns>
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>Provides the access token by lifting the <c>access_token</c> saved on the current request's auth ticket (<c>SaveTokens = true</c>); yields <c>null</c> with no active context or saved token.</summary>
public sealed class HttpContextAccessTokenProvider(IHttpContextAccessor httpContextAccessor) : IAccessTokenProvider
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

/// <summary>Access-token-provider registration helpers.</summary>
public static class AccessTokenProviderServiceCollectionExtensions
{
    /// <summary>Registers <see cref="HttpContextAccessTokenProvider"/> as the <see cref="IAccessTokenProvider"/>, adding <see cref="IHttpContextAccessor"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddHttpContextAccessTokenProvider(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();
        return services;
    }
}
