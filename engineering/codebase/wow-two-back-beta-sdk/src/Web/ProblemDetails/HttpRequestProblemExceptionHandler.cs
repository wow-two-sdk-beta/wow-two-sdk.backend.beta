using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

internal sealed class HttpRequestProblemExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException && !HttpMethods.IsHead(context.Request.Method)) return false;
        if (context.Response.HasStarted || cancellationToken.IsCancellationRequested) return false;
        await HttpProblemDetailsResponse.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = HttpProblemDetailsResponse.FromException(context, exception),
            Exception = exception,
        }).ConfigureAwait(false);
        return true;
    }
}
