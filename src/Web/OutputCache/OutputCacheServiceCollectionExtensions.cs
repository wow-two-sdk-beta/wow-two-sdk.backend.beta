using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.OutputCache;

/// <summary>Provides <c>Microsoft.AspNetCore.OutputCaching</c> registration with conventional policies.</summary>
public static class OutputCacheServiceCollectionExtensions
{
    /// <summary>Default named policy: 60-second cache.</summary>
    public const string DefaultPolicyName = "default";

    /// <summary>Adds output caching with a default 60-second policy. Add policies via the returned builder.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddDefaultOutputCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOutputCache(options =>
        {
            options.AddPolicy(DefaultPolicyName, b => b.Expire(TimeSpan.FromSeconds(60)));
        });

        return services;
    }
}
