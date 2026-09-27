using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;

namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Guards the configured paths: this machine passes, everyone else presents a live key.</summary>
/// <remarks>
/// A presented key is always checked — a revoked or unknown key is 401, even from this machine. Local-only paths
/// answer 403 to a key. Open paths pass untouched. Failures render as ProblemDetails.
/// </remarks>
/// <param name="next">The next middleware.</param>
/// <param name="options">The gate's paths and local pass.</param>
/// <param name="reader">The reader of a presented secret.</param>
public sealed class ApiKeyAccessGateMiddleware(
    RequestDelegate next,
    IOptions<ApiKeyAccessGateOptions> options,
    ApiKeySecretReader reader)
{
    /// <summary>Lets the request through, or answers 401 or 403 with a problem.</summary>
    /// <param name="context">The request context.</param>
    /// <returns>A task that completes when the request is handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var gate = options.Value;
        var path = context.Request.Path;
        if (!gate.GuardedPaths.Any(path.StartsWithSegments) || gate.OpenPaths.Any(path.StartsWithSegments))
        {
            await next(context);
            return;
        }

        if (reader.Read(context.Request) is not null)
        {
            var result = await context.AuthenticateAsync(ApiKeyAuthenticationDefaults.Scheme);
            if (!result.Succeeded)
            {
                await WriteProblemAsync(context, AppErrorFactory.Unauthorized("The API key is not valid or was revoked."));
                return;
            }

            if (gate.LocalOnlyPaths.Any(path.StartsWithSegments))
            {
                await WriteProblemAsync(context, AppErrorFactory.Forbidden("This is managed from the app's own machine, not with a key."));
                return;
            }

            context.User = result.Principal;
            await next(context);
            return;
        }

        if ((gate.AllowLocalWithoutKey && IsLocal(context.Connection)) || context.User.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        await WriteProblemAsync(
            context,
            AppErrorFactory.Unauthorized("An API key is required; send it as a Bearer token or in the key header."));
    }

    /// <summary>Checks whether a request comes from this machine — loopback, or an in-process test server.</summary>
    /// <param name="connection">The request's connection.</param>
    /// <returns><c>true</c> for a loopback or absent remote address.</returns>
    private static bool IsLocal(ConnectionInfo connection)
    {
        return connection.RemoteIpAddress is null || IPAddress.IsLoopback(connection.RemoteIpAddress);
    }

    /// <summary>Answers with the SDK's ProblemDetails rendering of an error, or a plain problem without the SDK web floor.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="error">The error to render.</param>
    /// <returns>A task that completes when the response is written.</returns>
    private static async Task WriteProblemAsync(HttpContext context, AppError error)
    {
        var factory = context.RequestServices.GetService<IAppErrorProblemDetailsFactory>();
        var problem = factory?.Create(error, context) ?? new ProblemDetails
        {
            Status = error.Type == AppErrorType.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized,
            Title = error.Type.ToString(),
            Detail = error.Message,
        };
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status401Unauthorized;

        var writer = context.RequestServices.GetService<IProblemDetailsService>();
        if (writer is not null && await writer.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
            return;

        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}
