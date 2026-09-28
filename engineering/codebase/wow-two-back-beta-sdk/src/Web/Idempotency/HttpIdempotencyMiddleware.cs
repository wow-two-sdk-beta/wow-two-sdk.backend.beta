using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using WoW.Two.Sdk.Backend.Beta.Web.Buffering;

namespace WoW.Two.Sdk.Backend.Beta.Web.Idempotency;

/// <summary>
/// Executes an unsafe request carrying an <c>Idempotency-Key</c> once per key and caller, storing its response through
/// <see cref="IIdempotencyRepository"/> and replaying it (with <c>Idempotent-Replayed: true</c>) on retries. A retry with
/// another body answers <c>422</c>; one racing an unfinished original answers <c>409</c>. Server errors, exceptions and
/// oversized or streamed responses are not stored, so their retries execute again. Inert until enabled by configuration.
/// </summary>
/// <param name="next">The rest of the pipeline.</param>
/// <param name="settings">The live <c>HttpIdempotency</c> settings.</param>
public sealed class HttpIdempotencyMiddleware(RequestDelegate next, IOptionsMonitor<HttpIdempotencySettings> settings)
{
    /// <summary>The header marking a replayed response.</summary>
    public const string ReplayedHeader = "Idempotent-Replayed";

    /// <summary>Runs the request once per key, or replays its stored response.</summary>
    /// <param name="context">The current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = settings.CurrentValue;
        if (!current.Enabled
            || !current.Methods.Exists(method => string.Equals(method, context.Request.Method, StringComparison.OrdinalIgnoreCase))
            || context.Request.Headers[current.HeaderName].ToString() is not { Length: > 0 } header)
        {
            await next(context);
            return;
        }

        if (header.Length > 255)
            throw AppErrorFactory.Validation($"The {current.HeaderName} header must be at most 255 characters.").ToException();

        var repository = context.RequestServices.GetRequiredService<IIdempotencyRepository>();
        var fingerprint = await FingerprintAsync(context.Request);
        var user = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value ?? string.Empty;
        var key = string.Join('\n', "http", context.Request.Method.ToUpperInvariant(), context.Request.Path.Value, user, header.Trim());

        var (acquired, cached, ownership) = await repository.TryAcquireAsync(key, typeof(IdempotentHttpResponseModel), context.RequestAborted);
        if (!acquired)
        {
            await ReplayAsync(context, cached as IdempotentHttpResponseModel, fingerprint, current.HeaderName);
            return;
        }

        await ExecuteAsync(context, repository, key, ownership, fingerprint, current);
    }

    private async Task ExecuteAsync(HttpContext context, IIdempotencyRepository repository, string key, Guid ownership, string fingerprint, HttpIdempotencySettings current)
    {
        var body = context.Response.Body;
        await using var buffer = new BoundedResponseBufferStream(
            body,
            current.MaxBodyBytes,
            () => BoundedResponseBufferStream.IsStreamingContentType(context.Response.ContentType));
        context.Response.Body = buffer;
        var stored = false;
        try
        {
            await next(context);
            if (!buffer.PassedThrough && context.Response.StatusCode < StatusCodes.Status500InternalServerError)
            {
                var response = new IdempotentHttpResponseModel
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType,
                    Body = buffer.Buffer.ToArray(),
                    Fingerprint = fingerprint,
                };
                await repository.StoreAsync(key, ownership, response, current.Ttl, CancellationToken.None);
                stored = true;
            }
        }
        finally
        {
            context.Response.Body = body;
            if (!stored)
                await repository.ReleaseAsync(key, ownership, CancellationToken.None);
        }

        if (!buffer.PassedThrough)
        {
            buffer.Buffer.Position = 0;
            await buffer.Buffer.CopyToAsync(body, context.RequestAborted);
        }
    }

    private static async Task ReplayAsync(HttpContext context, IdempotentHttpResponseModel? stored, string fingerprint, string headerName)
    {
        if (stored is null)
            throw AppErrorFactory.Conflict("An operation with this idempotency key is still in progress.").ToException();
        if (!string.Equals(stored.Fingerprint, fingerprint, StringComparison.Ordinal))
            throw AppError.Of(
                AppErrorType.BusinessRule,
                $"The {headerName} was already used with a different request body.",
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["messageKey"] = "IdempotencyKeyReused" }).ToException();

        context.Response.StatusCode = stored.StatusCode;
        if (stored.ContentType is not null)
            context.Response.ContentType = stored.ContentType;
        context.Response.Headers[ReplayedHeader] = "true";
        await context.Response.Body.WriteAsync(stored.Body, context.RequestAborted);
    }

    private static async Task<string> FingerprintAsync(HttpRequest request)
    {
        request.EnableBuffering();
        var digest = await SHA256.HashDataAsync(request.Body, request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        return Convert.ToHexString(digest);
    }
}
