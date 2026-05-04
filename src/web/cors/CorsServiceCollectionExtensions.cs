using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Cors;

/// <summary>
/// CORS policy presets.
/// </summary>
public static class CorsServiceCollectionExtensions
{
    /// <summary>Default policy name.</summary>
    public const string DefaultPolicyName = "wow-two-default";

    /// <summary>
    /// Register a default CORS policy with the given allowed origins. No credentials by default.
    /// </summary>
    public static IServiceCollection AddWowTwoCors(this IServiceCollection services, params string[] allowedOrigins)
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
}
