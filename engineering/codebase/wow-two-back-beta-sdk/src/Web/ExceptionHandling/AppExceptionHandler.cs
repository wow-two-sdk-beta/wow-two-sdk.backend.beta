using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Observability.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Maps a thrown <see cref="AppException"/> to an RFC 9457 ProblemDetails response via the shared factory.</summary>
public sealed class AppExceptionHandler(
    IErrorHttpStatusCodeMapper statusMapper,
    IErrorMessageResolver messageResolver,
    IProblemDetailsService problemDetailsService,
    AppErrorObserver observer) : IExceptionHandler
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

        var problem = AppErrorProblemDetailsFactory.Create(error, httpContext, statusMapper, messageResolver);

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = appException,
            ProblemDetails = problem,
        }).ConfigureAwait(false);
    }
}
