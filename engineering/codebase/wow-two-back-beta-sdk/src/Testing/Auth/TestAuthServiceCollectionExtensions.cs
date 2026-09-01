using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Auth;

/// <summary>
/// Opt-in registration for <see cref="TestAuthHandler"/> — wires a deterministic test identity into a
/// <c>WebApiTestHost{TEntryPoint}</c> so end-to-end tests authenticate without real OAuth.
/// </summary>
public static class TestAuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the test-auth scheme and makes it the default authenticate / challenge scheme, so requests
    /// resolve to the identity configured in <paramref name="configure"/>. Intended to run inside a test host's
    /// service-configuration step (e.g. <c>WebApiTestHost.ConfigureServicesHook</c>), AFTER the app's own auth has
    /// been registered — calling <see cref="AuthenticationBuilder"/> again here overrides the default scheme.
    /// </summary>
    /// <remarks>
    ///   - every request authenticates by default
    ///   - set <see cref="TestAuthOptions.RequiredHeader"/> in <paramref name="configure"/> to gate on that header
    ///   - gated, a request omitting that header stays anonymous, so <c>[Authorize]</c> returns 401
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">Optional callback to set the test identity (user id, email, roles, …) and, optionally, <see cref="TestAuthOptions.RequiredHeader"/>.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddTestAuth(this IServiceCollection services, Action<TestAuthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<TestAuthOptions, TestAuthHandler>(TestAuthHandler.SchemeName, configure);

        return services;
    }

    /// <summary>
    /// Builds a service-configuration delegate that calls <see cref="AddTestAuth"/>, shaped for direct assignment to
    /// a test host's <c>ConfigureServicesHook</c>.
    /// </summary>
    /// <remarks>Compose with other hook logic by invoking the returned delegate inside your own hook.</remarks>
    /// <param name="configure">Optional callback to set the test identity and, optionally, <see cref="TestAuthOptions.RequiredHeader"/>.</param>
    /// <returns>An <see cref="Action{T}"/> over <see cref="IServiceCollection"/> that registers test auth.</returns>
    public static Action<IServiceCollection> UseTestUser(Action<TestAuthOptions>? configure = null)
        => services => services.AddTestAuth(configure);
}
