using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace WoW.Two.Sdk.Backend.Beta.Web.ConditionalRequests;

/// <summary>
/// Tags successful GET responses with a weak entity tag (a hash of the body, unless the endpoint set its own tag) and
/// answers a matching <c>If-None-Match</c> with <c>304 Not Modified</c> and no body. Inert until
/// <see cref="ConditionalRequestSettings.Enabled"/>; streaming (SSE, NDJSON) and oversized responses pass through untagged.
/// </summary>
/// <param name="next">The rest of the pipeline.</param>
/// <param name="settings">The live <c>ConditionalRequests</c> settings.</param>
public sealed class ConditionalRequestMiddleware(RequestDelegate next, IOptionsMonitor<ConditionalRequestSettings> settings)
{
    /// <summary>Runs the pipeline, tagging and revalidating the response when enabled.</summary>
    /// <param name="context">The current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = settings.CurrentValue;
        if (!current.Enabled || !HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        var body = context.Response.Body;
        await using var buffer = new EntityTagBufferStream(
            body,
            current.MaxBufferBytes,
            () => IsStreaming(context.Response.ContentType));
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = body;
        }

        if (buffer.PassedThrough)
            return;

        if (context.Response.StatusCode == StatusCodes.Status200OK)
        {
            var tag = context.Response.Headers.ETag.ToString();
            if (string.IsNullOrEmpty(tag))
            {
                tag = $"W/\"{Convert.ToBase64String(SHA256.HashData(buffer.Buffer.GetBuffer().AsSpan(0, (int)buffer.Buffer.Length)), 0, 16)}\"";
                context.Response.Headers.ETag = tag;
            }

            if (Matches(context.Request, tag))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = null;
                context.Response.Headers.ContentType = default;
                return;
            }
        }

        buffer.Buffer.Position = 0;
        await buffer.Buffer.CopyToAsync(body, context.RequestAborted);
    }

    /// <summary>Content types that stream by design: server-sent events and newline-delimited JSON.</summary>
    private static bool IsStreaming(string? contentType)
        => contentType is not null
            && (contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("application/x-ndjson", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("application/stream+json", StringComparison.OrdinalIgnoreCase));

    /// <summary>Weak comparison of <c>If-None-Match</c> against <paramref name="tag"/>; <c>*</c> matches any tag.</summary>
    private static bool Matches(HttpRequest request, string tag)
    {
        var candidates = request.GetTypedHeaders().IfNoneMatch;
        if (candidates.Count == 0 || !EntityTagHeaderValue.TryParse(tag, out var current))
            return false;

        return candidates.Any(candidate => candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(current, useStrongComparison: false));
    }
}
