using System.Buffers.Binary;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Builds an unambiguous canonical byte payload by writing a type tag, a length, then the value bytes for every appended field.</summary>
/// <remarks>Wire format per field — one tag byte, a 4-byte big-endian length, then that many value bytes; scalars use a fixed length and write their value big-endian, so the byte stream is self-delimiting and injective over the field tuple. Not thread-safe — build one per entry. Calling code reaches the bytes through <see cref="IHashChainSealer{TEntry}"/> / <see cref="IHashChainVerifier{TEntry}"/>, not directly.</remarks>
public sealed class CanonicalPayloadBuilder : ICanonicalPayloadBuilder
{
    private readonly ArrayBufferWriter _buffer = new();

    /// <inheritdoc />
    public ICanonicalPayloadBuilder Append(string? value)
    {
        if (value is null)
        {
            return AppendNull();
        }

        // Write the UTF-8 bytes under a length prefix so any embedded delimiter is inert.
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteField(FieldTag.String, bytes);
        return this;
    }

    /// <inheritdoc />
    public ICanonicalPayloadBuilder Append(int value)
    {
        Span<byte> scalar = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(scalar, value);
        WriteField(FieldTag.Int32, scalar);
        return this;
    }

    /// <inheritdoc />
    public ICanonicalPayloadBuilder Append(long value)
    {
        Span<byte> scalar = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(scalar, value);
        WriteField(FieldTag.Int64, scalar);
        return this;
    }

    /// <inheritdoc />
    public ICanonicalPayloadBuilder Append(DateTimeOffset value)
    {
        // Normalize to UTC ticks so the same instant hashes the same regardless of the stored offset.
        Span<byte> scalar = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(scalar, value.UtcDateTime.Ticks);
        WriteField(FieldTag.Timestamp, scalar);
        return this;
    }

    /// <inheritdoc />
    public ICanonicalPayloadBuilder Append(byte[]? value)
    {
        if (value is null)
        {
            return AppendNull();
        }

        WriteField(FieldTag.Bytes, value);
        return this;
    }

    /// <inheritdoc />
    public ICanonicalPayloadBuilder Append(bool value)
    {
        Span<byte> scalar = [value ? (byte)1 : (byte)0];
        WriteField(FieldTag.Boolean, scalar);
        return this;
    }

    /// <inheritdoc />
    public ICanonicalPayloadBuilder AppendNull()
    {
        // A null carries a tag and a zero length — distinct from an empty string or empty byte array, which carry their own tags.
        WriteField(FieldTag.Null, ReadOnlySpan<byte>.Empty);
        return this;
    }

    /// <summary>Returns the canonical payload accumulated so far as a contiguous byte span.</summary>
    internal ReadOnlySpan<byte> AsSpan()
    {
        return _buffer.WrittenSpan;
    }

    private void WriteField(FieldTag tag, ReadOnlySpan<byte> value)
    {
        var header = _buffer.GetSpan(1 + sizeof(int));
        header[0] = (byte)tag;
        BinaryPrimitives.WriteInt32BigEndian(header[1..], value.Length);
        _buffer.Advance(1 + sizeof(int));

        if (value.IsEmpty)
        {
            return;
        }

        value.CopyTo(_buffer.GetSpan(value.Length));
        _buffer.Advance(value.Length);
    }

    /// <summary>The per-field type tag — distinguishes scalar kinds and the null marker so two fields of different types never share an encoding.</summary>
    private enum FieldTag : byte
    {
        Null = 0,
        String = 1,
        Int32 = 2,
        Int64 = 3,
        Timestamp = 4,
        Bytes = 5,
        Boolean = 6,
    }

    /// <summary>A minimal growable byte buffer — avoids a per-entry <c>MemoryStream</c> allocation and exposes the written bytes as a span.</summary>
    private sealed class ArrayBufferWriter
    {
        private byte[] _array = new byte[256];
        private int _written;

        public ReadOnlySpan<byte> WrittenSpan => _array.AsSpan(0, _written);

        public Span<byte> GetSpan(int sizeHint)
        {
            EnsureCapacity(sizeHint);
            return _array.AsSpan(_written);
        }

        public void Advance(int count)
        {
            _written += count;
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (_written + sizeHint <= _array.Length)
            {
                return;
            }

            var newSize = Math.Max(_array.Length * 2, _written + sizeHint);
            Array.Resize(ref _array, newSize);
        }
    }
}
