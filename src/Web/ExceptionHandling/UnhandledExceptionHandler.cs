using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Observability.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>The terminal exception handler — maps any otherwise-unhandled exception to a ProblemDetails via <see cref="IExceptionMapper"/> (a recognized DB/app exception gets its real status; an unknown one falls back to a safe 500). Never leaks the underlying exception message.</summary>
public sealed class UnhandledExceptionHandler(
    IExceptionMapper exceptionMapper,
    IErrorHttpStatusCodeMapper statusMapper,
    IErrorMessageResolver messageResolver,
    IProblemDetailsService problemDetailsService,
    AppErrorObserver observer) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var error = exceptionMapper.Map(exception);

        observer.Record(error, exception);

        var problem = AppErrorProblemDetailsFactory.Create(error, httpContext, statusMapper, messageResolver);

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }
}
