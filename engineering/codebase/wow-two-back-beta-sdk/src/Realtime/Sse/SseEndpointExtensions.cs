using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.Sse;

/// <summary>
/// <see cref="HttpContext"/> helpers for Server-Sent Events endpoints — the low-ceremony path for
/// streaming incremental results (LLM tokens, progress, live feeds) to a browser <c>EventSource</c>
/// over a single long-lived HTTP response. Handles the <c>text/event-stream</c> headers, response
/// un-buffering, per-item flushing, idle heartbeats, and client-disconnect cancellation.
/// </summary>
public static class SseEndpointExtensions
{
    /// <summary>
    /// Writes the SSE response headers, disables response buffering, and returns an <see cref="SseWriter"/>
    /// bound to the response body for manual frame-by-frame control. Use this when the streaming loop is
    /// bespoke; use <see cref="WriteServerSentEventsAsync{T}(HttpContext, IAsyncEnumerable{T}, SseStreamOptions?, CancellationToken)"/>
    /// for the common enumerate-and-flush case.
    /// </summary>
    /// <param name="httpContext">The current request context.</param>
    /// <param name="jsonOptions">Serializer options for the writer's JSON overloads; SDK wire preset when omitted.</param>
    /// <returns>A writer over the response body.</returns>
    public static SseWriter StartServerSentEventStream(this HttpContext httpContext, JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var response = httpContext.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache,no-transform";
        response.Headers["X-Accel-Buffering"] = "no"; // stop nginx/proxy response buffering

        // Flush frames as written rather than accumulating them in the server's output buffer.
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        return new SseWriter(response.Body, jsonOptions);
    }

    /// <summary>
    /// Streams each item of <paramref name="source"/> as a JSON-encoded SSE frame, flushing per item,
    /// sending idle heartbeats, and stopping cleanly when the client disconnects.
    /// </summary>
    /// <typeparam name="T">The item type; each item is JSON-serialized into the frame's <c>data</c>.</typeparam>
    /// <param name="httpContext">The current request context.</param>
    /// <param name="source">The asynchronous sequence to stream.</param>
    /// <param name="options">Streaming tunables; defaults suit LLM token streaming.</param>
    /// <param name="cancellationToken">Token to stop streaming; linked with the request-aborted token.</param>
    public static Task WriteServerSentEventsAsync<T>(
        this HttpContext httpContext,
        IAsyncEnumerable<T> source,
        SseStreamOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(source);
        options ??= new SseStreamOptions();

        var serializerOptions = options.JsonOptions;
        var eventName = options.EventName;
        SseEvent Project(T item)
        {
            var json = JsonSerializer.Serialize(item, serializerOptions ?? Foundation.Serialization.JsonOptionsPresets.Default);
            return eventName is null ? SseEvent.FromData(json) : SseEvent.Named(eventName, json);
        }

        return StreamCoreAsync(httpContext, source, Project, options, cancellationToken);
    }

    /// <summary>
    /// Streams pre-built <see cref="SseEvent"/> frames — maximum control over event name, id, and raw
    /// (non-JSON) payloads such as LLM token strings. Applies the same headers, flushing, heartbeats,
    /// and disconnect handling as the generic overload.
    /// </summary>
    /// <param name="httpContext">The current request context.</param>
    /// <param name="events">The asynchronous sequence of frames to stream.</param>
    /// <param name="options">Streaming tunables (heartbeat, completion event).</param>
    /// <param name="cancellationToken">Token to stop streaming; linked with the request-aborted token.</param>
    public static Task WriteServerSentEventsAsync(
        this HttpContext httpContext,
        IAsyncEnumerable<SseEvent> events,
        SseStreamOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(events);
        return StreamCoreAsync(httpContext, events, static e => e, options ?? new SseStreamOptions(), cancellationToken);
    }

    private static async Task StreamCoreAsync<T>(
        HttpContext httpContext,
        IAsyncEnumerable<T> source,
        Func<T, SseEvent> project,
        SseStreamOptions options,
        CancellationToken cancellationToken)
    {
        var writer = httpContext.StartServerSentEventStream(options.JsonOptions);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted, cancellationToken);
        var token = linked.Token;

        try
        {
            await using var enumerator = source.GetAsyncEnumerator(token);
            var moveNext = enumerator.MoveNextAsync().AsTask();

            if (options.HeartbeatInterval is { } interval && interval > TimeSpan.Zero)
            {
                using var heartbeat = new PeriodicTimer(interval);
                var beat = heartbeat.WaitForNextTickAsync(token).AsTask();
                while (true)
                {
                    var winner = await Task.WhenAny(moveNext, beat);
                    if (winner == moveNext)
                    {
                        if (!await moveNext) break;
                        await writer.WriteAsync(project(enumerator.Current), token);
                        moveNext = enumerator.MoveNextAsync().AsTask();
                    }
                    else
                    {
                        await beat;
                        await writer.WriteCommentAsync("heartbeat", token);
                        beat = heartbeat.WaitForNextTickAsync(token).AsTask();
                    }
                }
            }
            else
            {
                while (await moveNext)
                {
                    await writer.WriteAsync(project(enumerator.Current), token);
                    moveNext = enumerator.MoveNextAsync().AsTask();
                }
            }

            if (options.CompletionEventName is { } completion)
                await writer.WriteAsync(SseEvent.Named(completion, options.CompletionData ?? string.Empty), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Client disconnected or the caller cancelled — end the stream quietly, no error response.
        }
    }
}
