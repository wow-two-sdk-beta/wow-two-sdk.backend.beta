using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Web;

/// <summary>Extends test hosts with clients that behave like the single-page app in a browser.</summary>
public static class SpaClientExtensions
{
    /// <summary>Creates a client that follows redirects, keeps cookies and echoes the SPA antiforgery token.</summary>
    /// <typeparam name="TEntryPoint">The host's entry point.</typeparam>
    /// <param name="factory">The test host.</param>
    /// <param name="tokenPath">A path whose GET passes through the antiforgery middleware, such as <c>/health</c>.</param>
    /// <remarks>Matches <c>CreateClient()</c> defaults, so a suite adopting <c>UseSpaAntiforgery</c> swaps one call.</remarks>
    public static HttpClient CreateSpaClient<TEntryPoint>(this WebApplicationFactory<TEntryPoint> factory, string tokenPath = "/")
        where TEntryPoint : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory.CreateDefaultClient(
            factory.ClientOptions.BaseAddress,
            new RedirectHandler(),
            new CookieContainerHandler(),
            new SpaAntiforgeryHandler(tokenPath));
    }
}
