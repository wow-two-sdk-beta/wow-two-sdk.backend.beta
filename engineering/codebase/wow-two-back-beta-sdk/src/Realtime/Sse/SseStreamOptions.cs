using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.Sse;

/// <summary>
/// Tunables for streaming an <see cref="IAsyncEnumerable{T}"/> as Server-Sent Events via
/// <see cref="SseEndpointExtensions"/>. Defaults suit LLM token streaming — a 15-second heartbeat
/// keeps proxies from closing an idle connection while a model is still producing.
/// </summary>
public sealed record SseStreamOptions
{
    /// <summary>Gets the interval between comment-only keep-alive frames sent while the source is idle. <see langword="null"/> or non-positive disables heartbeats. Default 15 seconds.</summary>
    public TimeSpan? HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets the SSE event name applied to each streamed item. <see langword="null"/> emits the default <c>message</c> event.</summary>
    public string? EventName { get; init; }

    /// <summary>Gets an optional event name emitted once after the source completes (e.g. <c>done</c>), letting clients distinguish graceful end from disconnect. <see langword="null"/> sends nothing.</summary>
    public string? CompletionEventName { get; init; }

    /// <summary>Gets the data payload of the completion event when <see cref="CompletionEventName"/> is set. Defaults to empty.</summary>
    public string? CompletionData { get; init; }

    /// <summary>Gets the JSON serializer options used to encode each item. Defaults to the SDK wire preset (camelCase) when omitted.</summary>
    public JsonSerializerOptions? JsonOptions { get; init; }
}
