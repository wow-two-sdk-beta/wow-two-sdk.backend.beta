using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

/// <summary>Defines ProblemDetails creation from application errors.</summary>
public interface IAppErrorProblemDetailsFactory
{
    /// <summary>Creates the ProblemDetails for an application error.</summary>
    /// <param name="error">The error to render.</param>
    /// <param name="httpContext">The current request context.</param>
    /// <returns>The rendered ProblemDetails.</returns>
    Microsoft.AspNetCore.Mvc.ProblemDetails Create(AppError error, HttpContext httpContext);
}
