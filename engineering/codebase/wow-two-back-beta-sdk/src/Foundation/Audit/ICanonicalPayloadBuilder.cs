namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Defines an unambiguous, type-tagged and length-prefixed encoder for the fields hashed into a chain entry.</summary>
/// <remarks>Each append writes a type tag, then a length, then the raw bytes — so no field value can ever shift a field boundary or collide with an adjacent one. This is what makes the hash injective over the field tuple, fixing the delimiter ambiguity of a naive <c>'|'</c>-joined string (where <c>"a|b" + "c"</c> and <c>"a" + "b|c"</c> hash identically).</remarks>
public interface ICanonicalPayloadBuilder
{
    /// <summary>Appends a string field, or a distinct null marker when <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="value">The string to encode, or <see langword="null"/>.</param>
    ICanonicalPayloadBuilder Append(string? value);

    /// <summary>Appends a 32-bit integer field.</summary>
    /// <param name="value">The integer to encode.</param>
    ICanonicalPayloadBuilder Append(int value);

    /// <summary>Appends a 64-bit integer field.</summary>
    /// <param name="value">The long to encode.</param>
    ICanonicalPayloadBuilder Append(long value);

    /// <summary>Appends a timestamp field, normalized to UTC and encoded as round-trip ticks so encoding is offset-stable.</summary>
    /// <param name="value">The timestamp to encode.</param>
    ICanonicalPayloadBuilder Append(DateTimeOffset value);

    /// <summary>Appends a raw byte field, or a distinct null marker when <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="value">The bytes to encode, or <see langword="null"/>.</param>
    ICanonicalPayloadBuilder Append(byte[]? value);

    /// <summary>Appends a boolean field.</summary>
    /// <param name="value">The boolean to encode.</param>
    ICanonicalPayloadBuilder Append(bool value);

    /// <summary>Appends an explicit null marker, distinct from any present value of any type.</summary>
    ICanonicalPayloadBuilder AppendNull();
}
