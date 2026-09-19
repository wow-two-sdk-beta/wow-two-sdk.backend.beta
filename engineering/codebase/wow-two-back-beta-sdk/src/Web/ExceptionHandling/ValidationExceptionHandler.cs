using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Handles a <see cref="ValidationException"/> as validation ProblemDetails.</summary>
public sealed class ValidationExceptionHandler(
    IAppErrorProblemDetailsFactory problemDetailsFactory,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var problem = problemDetailsFactory.Create(validationException.ValidationError, httpContext);
        problem.Title = "One or more validation errors occurred.";

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = validationException,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }
}
