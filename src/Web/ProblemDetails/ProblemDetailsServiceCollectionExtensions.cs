using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

/// <summary>Provides ProblemDetails registration enriched with <c>traceId</c>.</summary>
public static class ProblemDetailsServiceCollectionExtensions
{
    /// <summary>Adds ProblemDetails with <c>traceId</c> enrichment.</summary>
    /// <param name="services">The service collection to configure.</param>
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
