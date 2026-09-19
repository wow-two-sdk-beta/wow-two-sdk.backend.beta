using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Observability.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Handles otherwise-unhandled exceptions as safe ProblemDetails responses.</summary>
public sealed class UnhandledExceptionHandler(
    IExceptionMapper exceptionMapper,
    IAppErrorProblemDetailsFactory problemDetailsFactory,
    IProblemDetailsService problemDetailsService,
    ErrorRecordingService observer) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var error = exceptionMapper.Map(exception);

        observer.Record(error, exception);

        var problem = problemDetailsFactory.Create(error, httpContext);

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }
}
