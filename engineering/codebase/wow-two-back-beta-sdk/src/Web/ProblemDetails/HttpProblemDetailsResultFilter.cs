using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

internal sealed class HttpProblemDetailsResultFilter : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        int? status = context.Result switch
        {
            ObjectResult result => result.StatusCode ?? (result.Value as Microsoft.AspNetCore.Mvc.ProblemDetails)?.Status
                ?? context.HttpContext.Response.StatusCode,
            JsonResult result => result.StatusCode ?? (result.Value as Microsoft.AspNetCore.Mvc.ProblemDetails)?.Status
                ?? context.HttpContext.Response.StatusCode,
            ContentResult result => result.StatusCode ?? context.HttpContext.Response.StatusCode,
            StatusCodeResult result => result.StatusCode,
            _ => null,
        };
        if (status is >= 400 and <= 599 && !context.HttpContext.Response.HasStarted)
        {
            var problem = context.Result switch
            {
                ObjectResult { Value: Microsoft.AspNetCore.Mvc.ProblemDetails existing } => existing,
                JsonResult { Value: Microsoft.AspNetCore.Mvc.ProblemDetails existing } => existing,
                _ => new Microsoft.AspNetCore.Mvc.ProblemDetails(),
            };
            problem.Status = status;
            context.Result = new HttpProblemDetailsActionResult(problem);
        }
        await next().ConfigureAwait(false);
    }
}
