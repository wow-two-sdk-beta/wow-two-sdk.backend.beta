using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

internal sealed class HttpProblemDetailsWriter : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context)
    {
        return !context.HttpContext.Response.HasStarted && !context.HttpContext.RequestAborted.IsCancellationRequested
            && (context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode) is >= 400 and <= 599;
    }

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        return new ValueTask(HttpProblemDetailsResponse.WriteAsync(context));
    }
}
