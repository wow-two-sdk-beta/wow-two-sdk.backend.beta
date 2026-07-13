using System.Text;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.Sse;

/// <summary>
/// Encodes <see cref="SseEvent"/> frames onto a stream in the <c>text/event-stream</c> wire format and
/// flushes each frame immediately so clients receive events as they are produced. Multi-line data values
/// are split into repeated <c>data:</c> lines per the EventSource specification. Not thread-safe — one
/// writer serves one response stream, written to by a single logical producer at a time.
/// </summary>
public sealed class SseWriter
{
    private readonly Stream _stream;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>Creates a writer over the given output stream (typically <c>HttpResponse.Body</c>).</summary>
    /// <param name="stream">The destination stream; frames are flushed to it as they are written.</param>
    /// <param name="jsonOptions">Serializer options for the JSON <c>WriteData</c> overloads; defaults to the SDK wire preset (camelCase) when omitted.</param>
    public SseWriter(Stream stream, JsonSerializerOptions? jsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _jsonOptions = jsonOptions ?? JsonOptionsPresets.Default;
    }

    /// <summary>Writes a fully-formed SSE frame and flushes it.</summary>
    /// <param name="sseEvent">The frame to encode.</param>
    /// <param name="cancellationToken">Token to abort the write.</param>
    public async Task WriteAsync(SseEvent sseEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sseEvent);

        var frame = Encode(sseEvent);
        var bytes = Encoding.UTF8.GetBytes(frame);
        await _stream.WriteAsync(bytes, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    /// <summary>Serializes <paramref name="value"/> to JSON and writes it as the payload of a default <c>message</c> event.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="value">The value to serialize and stream.</param>
    /// <param name="cancellationToken">Token to abort the write.</param>
    public Task WriteDataAsync<T>(T value, CancellationToken cancellationToken = default)
        => WriteAsync(SseEvent.FromData(JsonSerializer.Serialize(value, _jsonOptions)), cancellationToken);

    /// <summary>Serializes <paramref name="value"/> to JSON and writes it under the given event name.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="eventName">The event name dispatched to the client listener.</param>
    /// <param name="value">The value to serialize and stream.</param>
    /// <param name="cancellationToken">Token to abort the write.</param>
    public Task WriteDataAsync<T>(string eventName, T value, CancellationToken cancellationToken = default)
        => WriteAsync(SseEvent.Named(eventName, JsonSerializer.Serialize(value, _jsonOptions)), cancellationToken);

    /// <summary>Writes a comment-only keep-alive frame to hold the connection open during idle gaps.</summary>
    /// <param name="comment">The comment text (clients ignore it).</param>
    /// <param name="cancellationToken">Token to abort the write.</param>
    public Task WriteCommentAsync(string comment = "keep-alive", CancellationToken cancellationToken = default)
        => WriteAsync(SseEvent.KeepAlive(comment), cancellationToken);

    private static string Encode(SseEvent e)
    {
        var sb = new StringBuilder();

        if (e.Comment is not null)
            sb.Append(": ").Append(StripNewlines(e.Comment)).Append('\n');

        if (e.EventName is not null)
            sb.Append("event: ").Append(StripNewlines(e.EventName)).Append('\n');

        if (e.Id is not null)
            sb.Append("id: ").Append(StripNewlines(e.Id)).Append('\n');

        if (e.Retry is { } retry)
            sb.Append("retry: ").Append(retry.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');

        // A data value spanning multiple lines is emitted as one `data:` line per physical line.
        if (e.Data.Length > 0 || (e.Comment is null && e.EventName is null && e.Id is null))
        {
            foreach (var line in e.Data.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                sb.Append("data: ").Append(line).Append('\n');
        }

        // Blank line terminates the frame.
        sb.Append('\n');
        return sb.ToString();
    }

    private static string StripNewlines(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ');
}
