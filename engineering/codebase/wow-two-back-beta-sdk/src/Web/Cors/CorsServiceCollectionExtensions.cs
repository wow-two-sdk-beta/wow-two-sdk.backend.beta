using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Cors;

/// <summary>Provides CORS policy presets.</summary>
public static class CorsServiceCollectionExtensions
{
    /// <summary>Default policy name.</summary>
    public const string DefaultPolicyName = "default";

    /// <summary>Credentialed policy name.</summary>
    public const string CredentialedPolicyName = "credentialed";

    /// <summary>Adds a default CORS policy for the given origins (empty list allows any origin); no credentials.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="allowedOrigins">Exact origins permitted (scheme, host, and port).</param>
    public static IServiceCollection AddDefaultCorsPolicy(this IServiceCollection services, params string[] allowedOrigins)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(allowedOrigins);

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicyName, b =>
            {
                if (allowedOrigins.Length == 0)
                    b.AllowAnyOrigin();
                else
                    b.WithOrigins(allowedOrigins);

                b.AllowAnyHeader().AllowAnyMethod();
            });
        });

        return services;
    }

    /// <summary>Adds a credentialed CORS policy (cookies, <c>Authorization</c>, client certs) for a cookie-auth SPA on a separate origin.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="allowedOrigins">Exact origins permitted (scheme, host, and port).</param>
    /// <exception cref="ArgumentException"><paramref name="allowedOrigins"/> is empty.</exception>
    public static IServiceCollection AddCredentialedCorsPolicy(this IServiceCollection services, params string[] allowedOrigins)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(allowedOrigins);

        if (allowedOrigins.Length == 0)
            throw new ArgumentException(
                "A credentialed CORS policy requires at least one explicit origin: the CORS spec forbids " +
                "combining AllowAnyOrigin() with AllowCredentials(), so there is no safe wildcard fallback.",
                nameof(allowedOrigins));

        services.AddCors(options =>
        {
            options.AddPolicy(CredentialedPolicyName, b =>
                b.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        return services;
    }
}
