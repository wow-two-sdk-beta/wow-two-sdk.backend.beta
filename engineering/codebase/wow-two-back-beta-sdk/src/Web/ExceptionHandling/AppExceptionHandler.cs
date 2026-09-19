using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Observability.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Handles an <see cref="AppException"/> as an RFC 9457 ProblemDetails response.</summary>
public sealed class AppExceptionHandler(
    IAppErrorProblemDetailsFactory problemDetailsFactory,
    IProblemDetailsService problemDetailsService,
    ErrorRecordingService observer) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not AppException appException)
        {
            return false;
        }

        var error = appException.Error;

        observer.Record(error, appException);

        var problem = problemDetailsFactory.Create(error, httpContext);

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = appException,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }
}
