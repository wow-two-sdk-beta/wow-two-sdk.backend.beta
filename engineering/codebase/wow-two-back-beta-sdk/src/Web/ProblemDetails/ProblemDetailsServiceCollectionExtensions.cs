using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

/// <summary>Provides ProblemDetails registration enriched with <c>traceId</c>, <c>requestId</c>, and a status-derived <c>code</c>.</summary>
public static class ProblemDetailsServiceCollectionExtensions
{
    /// <summary>Adds ProblemDetails with <c>traceId</c>/<c>requestId</c> enrichment and a <c>code</c> backfilled from the status for framework errors.</summary>
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
                {
                    ctx.ProblemDetails.Extensions["traceId"] = activity.Id;
                }

                ctx.ProblemDetails.Extensions["requestId"] = ctx.HttpContext.TraceIdentifier;

                BackfillCodeFromStatus(ctx.ProblemDetails);
            };
        });

        return services;
    }

    private static void BackfillCodeFromStatus(Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails)
    {
        if (problemDetails.Extensions.ContainsKey("code"))
        {
            return;
        }

        var code = problemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "Validation",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "NotFound",
            StatusCodes.Status405MethodNotAllowed => "MethodNotAllowed",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status415UnsupportedMediaType => "UnsupportedMediaType",
            StatusCodes.Status429TooManyRequests => "TooManyRequests",
            >= 500 => "Unexpected",
            _ => null,
        };

        if (code is not null)
        {
            problemDetails.Extensions["code"] = code;
        }
    }
}
