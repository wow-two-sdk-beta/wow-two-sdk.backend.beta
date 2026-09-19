using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Integrations;

/// <summary>Defines behavior that supplies the access token an integration client authorizes its calls with; swap the implementation to control which token each call carries.</summary>
public interface IAccessTokenService
{
    /// <summary>Resolves the current access token, or <c>null</c> when none is available.</summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The access token, or <c>null</c>.</returns>
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}
