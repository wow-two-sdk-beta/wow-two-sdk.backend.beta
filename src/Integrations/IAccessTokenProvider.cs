using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Integrations;

/// <summary>Supplies the access token an integration client authorizes its calls with (Bearer / Basic). The parameterized token source — swap the impl to control which token each call carries.</summary>
public interface IAccessTokenProvider
{
    /// <summary>Resolves the current access token, or <c>null</c> when none is available.</summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The access token, or <c>null</c>.</returns>
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}

/// <summary>Default <see cref="IAccessTokenProvider"/> that lifts the <c>access_token</c> saved on the current request's auth ticket (<c>SaveTokens = true</c>). Yields <c>null</c> when there is no active context or no saved token.</summary>
public sealed class HttpContextAccessTokenProvider(IHttpContextAccessor httpContextAccessor) : IAccessTokenProvider
{
    /// <inheritdoc />
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
            return null;

        // The token is saved on the auth ticket (SaveTokens = true on the OAuth handler).
        var token = await context.GetTokenAsync("access_token");
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}

/// <summary>Access-token-provider registration helpers.</summary>
public static class AccessTokenProviderServiceCollectionExtensions
{
    /// <summary>Registers <see cref="HttpContextAccessTokenProvider"/> as the <see cref="IAccessTokenProvider"/> (adds <see cref="IHttpContextAccessor"/>). Reads the signed-in user's saved <c>access_token</c> off the current request.</summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddHttpContextAccessTokenProvider(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();
        return services;
    }
}
