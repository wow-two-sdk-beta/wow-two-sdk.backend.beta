using Microsoft.AspNetCore.Mvc;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

internal sealed class HttpProblemDetailsActionResult(Microsoft.AspNetCore.Mvc.ProblemDetails problem) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context)
    {
        return HttpProblemDetailsResponse.WriteAsync(context.HttpContext, problem);
    }
}
