namespace WoW.Two.Sdk.Backend.Beta.Web.ConditionalRequests;

/// <summary>
/// Buffers a response body up to a limit so it can be hashed; on overflow or a streaming content type it writes the
/// buffer through and passes every later write straight to the real body.
/// </summary>
internal sealed class EntityTagBufferStream(Stream inner, int limit, Func<bool> isStreaming) : Stream
{
    private readonly MemoryStream _buffer = new();

    /// <summary>Whether the body went straight to the client, so no tag can be computed.</summary>
    public bool PassedThrough { get; private set; }

    /// <summary>The buffered body.</summary>
    public MemoryStream Buffer => _buffer;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!PassedThrough && (isStreaming() || _buffer.Length + buffer.Length > limit))
            await PassThroughAsync(cancellationToken);

        if (PassedThrough)
            await inner.WriteAsync(buffer, cancellationToken);
        else
            _buffer.Write(buffer.Span);
    }

    public override void Flush()
    {
    }

    /// <remarks>Serializers flush after every write, so a flush alone never ends buffering; streaming types do.</remarks>
    public override Task FlushAsync(CancellationToken cancellationToken)
        => PassedThrough ? inner.FlushAsync(cancellationToken) : Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _buffer.Dispose();
        base.Dispose(disposing);
    }

    private async Task PassThroughAsync(CancellationToken cancellationToken)
    {
        if (PassedThrough)
            return;

        PassedThrough = true;
        _buffer.Position = 0;
        await _buffer.CopyToAsync(inner, cancellationToken);
        _buffer.SetLength(0);
    }
}
