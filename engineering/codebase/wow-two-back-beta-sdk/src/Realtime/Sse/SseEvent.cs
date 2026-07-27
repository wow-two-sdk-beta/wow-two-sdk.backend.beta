namespace WoW.Two.Sdk.Backend.Beta.Realtime.Sse;

/// <summary>
/// A single Server-Sent Events frame as defined by the WHATWG EventSource specification: an optional
/// event name, one or more <c>data</c> lines, an optional id (for <c>Last-Event-ID</c> resumption),
/// and an optional reconnection <c>retry</c> hint. A frame carrying only a <see cref="Comment"/> is a
/// keep-alive ping that clients ignore.
/// </summary>
public sealed record SseEvent
{
    /// <summary>Gets the payload written as one or more <c>data:</c> lines. Multi-line values are split per the spec. May be empty for a comment-only keep-alive.</summary>
    public string Data { get; init; } = string.Empty;

    /// <summary>Gets the optional event name written as <c>event:</c> — dispatched to a matching client listener. <see langword="null"/> emits the default <c>message</c> event.</summary>
    public string? EventName { get; init; }

    /// <summary>Gets the optional event id written as <c>id:</c>. The browser echoes the last id in the <c>Last-Event-ID</c> header on reconnect, enabling resumable streams.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the optional reconnection delay written as <c>retry:</c> (milliseconds) — instructs the client how long to wait before reconnecting after a drop.</summary>
    public int? Retry { get; init; }

    /// <summary>Gets an optional comment written as a <c>:</c> line. Comment-only frames are keep-alive pings ignored by clients.</summary>
    public string? Comment { get; init; }

    /// <summary>Creates a data-only event carrying the given payload under the default <c>message</c> event.</summary>
    /// <param name="data">The frame payload.</param>
    /// <returns>A new <see cref="SseEvent"/>.</returns>
    public static SseEvent FromData(string data) => new() { Data = data };

    /// <summary>Creates a named event carrying the given payload.</summary>
    /// <param name="eventName">The event name dispatched to the client listener.</param>
    /// <param name="data">The frame payload.</param>
    /// <returns>A new <see cref="SseEvent"/>.</returns>
    public static SseEvent Named(string eventName, string data) => new() { EventName = eventName, Data = data };

    /// <summary>Creates a comment-only keep-alive frame.</summary>
    /// <param name="comment">The comment text (clients ignore it).</param>
    /// <returns>A new <see cref="SseEvent"/>.</returns>
    public static SseEvent KeepAlive(string comment = "keep-alive") => new() { Comment = comment };
}
