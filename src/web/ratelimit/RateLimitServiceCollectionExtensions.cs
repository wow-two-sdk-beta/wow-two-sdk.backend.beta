using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.RateLimit;

/// <summary>
/// Conventional rate-limit policies. The "default" policy partitions per-IP with a sliding window of 100/min.
/// </summary>
public static class RateLimitServiceCollectionExtensions
{
    /// <summary>Policy name applied to every endpoint by default.</summary>
    public const string DefaultPolicyName = "wow-two-default";

    /// <summary>
    /// Register a default sliding-window per-IP rate limiter (100 requests / 60 seconds).
    /// </summary>
    public static IServiceCollection AddWowTwoRateLimit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(DefaultPolicyName, ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromSeconds(60),
                        SegmentsPerWindow = 6,
                        PermitLimit = 100,
                        QueueLimit = 0,
                    }));
        });

        return services;
    }
}
