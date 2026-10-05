using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

internal static class HttpProblemDetailsResponse
{
    internal static async Task WriteExceptionAsync(HttpContext context)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) return;
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var problem = exception is null
            ? new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = StatusCodes.Status500InternalServerError }
            : FromException(context, exception);
        await WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context, ProblemDetails = problem, Exception = exception,
        }).ConfigureAwait(false);
    }

    internal static Microsoft.AspNetCore.Mvc.ProblemDetails FromException(HttpContext context, Exception exception)
    {
        if (exception is BadHttpRequestException request)
            return new() { Status = request.StatusCode, Detail = "The request could not be processed." };
        var error = context.RequestServices.GetRequiredService<IExceptionMapper>().Map(exception);
        return context.RequestServices.GetRequiredService<IAppErrorProblemDetailsFactory>().Create(error, context);
    }

    private static void Enrich(ProblemDetailsContext problemContext)
    {
        var context = problemContext.HttpContext;
        var problem = problemContext.ProblemDetails;
        problem.Status ??= context.Response.StatusCode;
        problem.Type ??= "about:blank";
        problem.Title ??= ReasonPhrases.GetReasonPhrase(problem.Status.Value);
        problem.Instance ??= context.Request.Path;
        context.RequestServices.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails?.Invoke(
            problemContext);
        problem.Extensions.TryAdd("traceId", System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier);
        problem.Extensions.TryAdd("requestId", context.TraceIdentifier);
    }

    internal static Task WriteAsync(HttpContext context, Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        return WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }

    internal static async Task WriteAsync(ProblemDetailsContext problemContext)
    {
        var context = problemContext.HttpContext;
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) return;
        Enrich(problemContext);
        var problem = problemContext.ProblemDetails;
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.ContentType = "application/problem+json";
            return;
        }
        if (problem is Microsoft.AspNetCore.Mvc.ValidationProblemDetails validation)
        {
            var copy = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = problem.Status, Type = problem.Type, Title = problem.Title,
                Detail = problem.Detail, Instance = problem.Instance,
            };
            foreach (var pair in problem.Extensions) copy.Extensions[pair.Key] = pair.Value;
            copy.Extensions["errors"] = validation.Errors;
            problem = copy;
        }
        // Explicit JSON bypasses MVC negotiation, including ReturnHttpNotAcceptable=true.
        await Results.Json(problem, statusCode: problem.Status, contentType: "application/problem+json")
            .ExecuteAsync(context).ConfigureAwait(false);
    }
}
