using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Renders a thrown <see cref="ValidationException"/> as a 400 ProblemDetails carrying <c>code</c> and <c>errors[]</c> from inside the MVC action invoker.</summary>
public sealed class ValidationExceptionFilter(
    IAppErrorProblemDetailsFactory problemDetailsFactory) : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Exception is not ValidationException validationException)
        {
            return;
        }

        var problem = problemDetailsFactory.Create(validationException.ValidationError, context.HttpContext);
        problem.Title = "One or more validation errors occurred.";

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
        };
        context.ExceptionHandled = true;
    }
}
