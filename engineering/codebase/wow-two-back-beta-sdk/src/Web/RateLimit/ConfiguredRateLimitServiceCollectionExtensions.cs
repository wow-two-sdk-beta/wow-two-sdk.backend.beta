using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.RateLimit;

/// <summary>Registers rate-limit policies declared in configuration, with problem-details rejections.</summary>
public static class ConfiguredRateLimitServiceCollectionExtensions
{
    /// <summary>
    /// Adds every policy of the <c>RateLimits</c> configuration section (and its optional global policy) to the rate
    /// limiter, and rejects with <c>429</c>, <c>Retry-After</c> and a problem-details body. Policies are read once, at
    /// startup; <c>AddApiDefaults</c> calls this already.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The host configuration.</param>
    public static IServiceCollection AddConfiguredRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = new RateLimitSettings();
        configuration.GetSection(RateLimitSettings.SectionName).Bind(settings);
        if (settings.GlobalPolicy is { } global && !settings.Policies.ContainsKey(global))
            throw new InvalidOperationException($"RateLimits:GlobalPolicy '{global}' names no configured policy.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RejectAsync;
            foreach (var (name, policy) in settings.Policies)
                options.AddPolicy(name, context => Partition(context, name, policy));
            if (settings.GlobalPolicy is { } globalName)
            {
                var policy = settings.Policies[globalName];
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => Partition(context, globalName, policy));
            }
        });
        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext context, string name, RateLimitPolicySettings policy)
    {
        var key = $"{name}:{PartitionKey(context, policy.PartitionBy)}";
        return policy.Algorithm.ToUpperInvariant() switch
        {
            "FIXEDWINDOW" => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit, Window = policy.Window, QueueLimit = policy.QueueLimit,
            }),
            "TOKENBUCKET" => RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = policy.PermitLimit, TokensPerPeriod = policy.PermitLimit, ReplenishmentPeriod = policy.Window, QueueLimit = policy.QueueLimit,
            }),
            "CONCURRENCY" => RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = policy.PermitLimit, QueueLimit = policy.QueueLimit,
            }),
            _ => RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit, Window = policy.Window, SegmentsPerWindow = policy.SegmentsPerWindow, QueueLimit = policy.QueueLimit,
            }),
        };
    }

    private static string PartitionKey(HttpContext context, string partitionBy)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return partitionBy.ToUpperInvariant() switch
        {
            "USER" => context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value ?? ip,
            "TENANT" => context.RequestServices.GetService<ITenantContext>()?.TenantId ?? ip,
            _ => ip,
        };
    }

    private static async ValueTask RejectAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var http = rejected.HttpContext;
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            metadata["retryAfter"] = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        var error = AppError.Of(AppErrorType.TooManyRequests, "Too many requests. Please try again later.", metadata);
        if (http.RequestServices.GetService<IAppErrorProblemDetailsFactory>() is { } problems)
        {
            var problem = problems.Create(error, http);
            await http.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
            return;
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    }
}
