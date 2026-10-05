using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

internal sealed class HttpProblemDetailsRegistration : IPostConfigureOptions<ProblemDetailsOptions>
{
    private static readonly object CustomizedProblemsKey = new();

    public void PostConfigure(string? name, ProblemDetailsOptions options)
    {
        var customize = options.CustomizeProblemDetails;
        if (customize is null) return;
        options.CustomizeProblemDetails = context =>
        {
            if (!context.HttpContext.Items.TryGetValue(CustomizedProblemsKey, out var value))
            {
                value = new HashSet<Microsoft.AspNetCore.Mvc.ProblemDetails>(ReferenceEqualityComparer.Instance);
                context.HttpContext.Items[CustomizedProblemsKey] = value;
            }
            // MVC's factory can customize a problem before the HTTP writer receives it.
            if (((HashSet<Microsoft.AspNetCore.Mvc.ProblemDetails>)value!).Add(context.ProblemDetails))
                customize(context);
        };
    }
}
