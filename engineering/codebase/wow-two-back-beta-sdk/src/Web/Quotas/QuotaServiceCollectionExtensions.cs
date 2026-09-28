using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Web.Contracts;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Registers usage quotas and gates endpoints on them.</summary>
public static class QuotaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IQuotaService"/> with in-memory counters and the claim-based subject. Quotas stay off until
    /// <see cref="QuotaOptions.Enabled"/>; options come from <paramref name="configure"/>, then the host section <c>Quotas</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Quotas, plans and claim types.</param>
    public static IServiceCollection AddQuotas(this IServiceCollection services, Action<QuotaOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            QuotaOptions.SectionName,
            configure,
            builder => builder.Validate(o => !string.IsNullOrWhiteSpace(o.DefaultPlan), "QuotaOptions.DefaultPlan must not be blank."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IQuotaRepository, InMemoryQuotaRepository>();
        services.TryAddSingleton<IQuotaSubjectService, ClaimQuotaSubjectService>();
        services.TryAddSingleton<IQuotaService, QuotaService>();
        return services;
    }

    /// <summary>Keeps quota counters in Redis, so every host shares them; registers the connection when none is.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Redis connection string, used when no connection is registered.</param>
    public static IServiceCollection AddRedisQuotaRepository(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.Replace(ServiceDescriptor.Singleton<IQuotaRepository>(provider => new RedisQuotaRepository(provider.GetRequiredService<IConnectionMultiplexer>())));
        return services;
    }

    /// <summary>
    /// Uses <paramref name="amount"/> units of <paramref name="quota"/> before the handler runs, refunding them when the
    /// handler fails or answers 4xx/5xx. An exhausted quota answers <c>429</c> with <c>Retry-After</c> at the window's
    /// end; success carries <c>X-Quota-Limit</c>, <c>X-Quota-Remaining</c> and <c>X-Quota-Reset</c>. Inert while quotas are off.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint or group builder.</typeparam>
    /// <param name="builder">The endpoint, such as a conversion route.</param>
    /// <param name="quota">The quota name under <c>Quotas:Definitions</c>.</param>
    /// <param name="amount">The units one call uses. Default 1.</param>
    public static TBuilder RequireQuota<TBuilder>(this TBuilder builder, string quota, long amount = 1)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(quota);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var services = http.RequestServices;
            if (!services.GetRequiredService<IOptionsMonitor<QuotaOptions>>().CurrentValue.Enabled)
                return await next(context);

            var quotas = services.GetRequiredService<IQuotaService>();
            var (subject, plan) = await services.GetRequiredService<IQuotaSubjectService>().ResolveAsync(http, http.RequestAborted);
            var result = await quotas.TryConsumeAsync(quota, subject, plan, amount, http.RequestAborted);
            if (!result.Allowed)
                throw Exhausted(result, services.GetRequiredService<TimeProvider>().GetUtcNow()).ToException();

            object? outcome;
            try
            {
                outcome = await next(context);
            }
            catch
            {
                await quotas.RefundAsync(quota, subject, amount, CancellationToken.None);
                throw;
            }

            if (outcome is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status400BadRequest })
            {
                await quotas.RefundAsync(quota, subject, amount, CancellationToken.None);
                return outcome;
            }

            Stamp(http.Response, result);
            return outcome;
        });
    }

    /// <summary>
    /// Maps <c>GET quotas</c>: the caller's standing on every defined quota (limit, used, remaining, reset), so a client
    /// can show "2 of 3 left today". Answers an empty list while quotas are off.
    /// </summary>
    /// <param name="endpoints">The route builder or group.</param>
    public static RouteHandlerBuilder MapQuotaUsageEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGet("quotas", async (HttpContext http, IOptionsMonitor<QuotaOptions> options, IQuotaService quotas, IQuotaSubjectService subjects, CancellationToken cancellationToken) =>
        {
            var current = options.CurrentValue;
            if (!current.Enabled)
                return Results.Ok(ApiResponse<IReadOnlyList<QuotaResult>>.Ok([]));

            var (subject, plan) = await subjects.ResolveAsync(http, cancellationToken);
            var usage = new List<QuotaResult>();
            foreach (var name in current.Definitions.Keys.Order(StringComparer.Ordinal))
                usage.Add(await quotas.GetUsageAsync(name, subject, plan, cancellationToken));

            return Results.Ok(ApiResponse<IReadOnlyList<QuotaResult>>.Ok(usage));
        });
    }

    private static AppError Exhausted(QuotaResult result, DateTimeOffset now)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["messageKey"] = "QuotaExceeded",
            [AppErrorProblemDetailsFactory.ExtensionsMetadataKey] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["quota"] = result.Quota,
                ["limit"] = result.Limit,
                ["used"] = result.Used,
                ["resetsAt"] = result.ResetsAt,
            },
        };
        if (result.ResetsAt is { } resetsAt)
            metadata["retryAfter"] = Math.Max((long)Math.Ceiling((resetsAt - now).TotalSeconds), 1).ToString(CultureInfo.InvariantCulture);

        return AppError.Of(AppErrorType.TooManyRequests, $"The {result.Quota} allowance is used up for now.", metadata);
    }

    private static void Stamp(HttpResponse response, QuotaResult result)
    {
        if (response.HasStarted || result.Limit is not { } limit)
            return;

        response.Headers["X-Quota-Limit"] = limit.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Quota-Remaining"] = (result.Remaining ?? 0).ToString(CultureInfo.InvariantCulture);
        if (result.ResetsAt is { } resetsAt)
            response.Headers["X-Quota-Reset"] = resetsAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }
}
