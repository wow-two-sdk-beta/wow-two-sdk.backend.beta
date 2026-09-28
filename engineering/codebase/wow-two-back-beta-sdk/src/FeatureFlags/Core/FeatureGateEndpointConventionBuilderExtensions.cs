using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.FeatureFlags.Core;

/// <summary>Gates minimal-API endpoints on feature flags.</summary>
public static class FeatureGateEndpointConventionBuilderExtensions
{
    /// <summary>Answer <c>404</c> while any of <paramref name="features"/> is disabled, as if the endpoint did not exist.</summary>
    /// <typeparam name="TBuilder">The endpoint or group builder.</typeparam>
    /// <param name="builder">The builder to gate.</param>
    /// <param name="features">The feature flags that must all be enabled.</param>
    public static TBuilder RequireFeatures<TBuilder>(this TBuilder builder, params string[] features)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(features);
        if (features.Length == 0)
            throw new ArgumentException("Name at least one feature.", nameof(features));

        return builder.AddEndpointFilter(async (context, next) =>
        {
            var flags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlags>();
            foreach (var feature in features)
            {
                if (!await flags.IsEnabledAsync(feature, context.HttpContext.RequestAborted))
                    return Results.NotFound();
            }

            return await next(context);
        });
    }
}
