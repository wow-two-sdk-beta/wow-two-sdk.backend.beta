using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Translates a thrown <see cref="ValidationException"/> into a 400 validation ProblemDetails carrying <c>code</c> and <c>errors[]</c>.</summary>
public sealed class ValidationExceptionHandler(
    IErrorHttpStatusCodeMapper statusMapper,
    IErrorMessageResolver messageResolver,
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

        var problem = AppErrorProblemDetailsFactory.Create(
            validationException.ValidationError, httpContext, statusMapper, messageResolver);
        problem.Title = "One or more validation errors occurred.";

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = validationException,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }
}
