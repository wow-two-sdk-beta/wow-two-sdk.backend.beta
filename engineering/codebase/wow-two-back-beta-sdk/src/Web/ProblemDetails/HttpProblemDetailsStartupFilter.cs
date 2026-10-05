using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

/// <summary>Places the opt-in boundary before framework host filtering and other startup middleware.</summary>
internal sealed class HttpProblemDetailsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseHttpProblemDetails();
            next(app);
        };
    }
}
