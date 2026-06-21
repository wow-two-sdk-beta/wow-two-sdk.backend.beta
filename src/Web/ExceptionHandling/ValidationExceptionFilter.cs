using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Renders a thrown <see cref="ValidationException"/> as a 400 validation ProblemDetails from inside the MVC action invoker.</summary>
public sealed class ValidationExceptionFilter(ProblemDetailsFactory problemDetailsFactory) : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Exception is not ValidationException validationException)
            return;

        var modelState = new ModelStateDictionary();
        foreach (var error in validationException.Errors)
            modelState.AddModelError(error.Property, error.Message);

        var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
            context.HttpContext, modelState, StatusCodes.Status400BadRequest);

        context.Result = new BadRequestObjectResult(problemDetails);
        context.ExceptionHandled = true;
    }
}
