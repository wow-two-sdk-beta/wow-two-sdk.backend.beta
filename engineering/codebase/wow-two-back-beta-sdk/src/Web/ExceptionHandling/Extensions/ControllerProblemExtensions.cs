using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Extensions;

/// <summary>Extends controllers with the registered application-error response policy.</summary>
public static class ControllerProblemExtensions
{
    /// <summary>Creates a ProblemDetails result through the request's registered factory.</summary>
    /// <param name="controller">Controller handling the request.</param>
    /// <param name="error">Application error to render.</param>
    public static ObjectResult ToProblem(this ControllerBase controller, AppError error)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(error);
        var http = controller.HttpContext;
        var factory = http.RequestServices.GetRequiredService<IAppErrorProblemDetailsFactory>();
        var problem = factory.Create(error, http);
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
