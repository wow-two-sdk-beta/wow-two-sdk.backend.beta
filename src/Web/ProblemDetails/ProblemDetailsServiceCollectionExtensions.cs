using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

/// <summary>
/// Conventional ProblemDetails registration — builds on the built-in `AddProblemDetails` and
/// enriches with `traceId` automatically.
/// </summary>
public static class ProblemDetailsServiceCollectionExtensions
{
    /// <summary>
    /// Register ProblemDetails with `traceId` enrichment.
    /// </summary>
    public static IServiceCollection AddTraceAwareProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                var activity = System.Diagnostics.Activity.Current;
                if (activity is not null)
                    ctx.ProblemDetails.Extensions["traceId"] = activity.Id;
                ctx.ProblemDetails.Extensions["requestId"] = ctx.HttpContext.TraceIdentifier;
            };
        });

        return services;
    }
}
