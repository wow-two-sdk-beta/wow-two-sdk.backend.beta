namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents an encoded image and its shape.</summary>
public sealed record ImageResult
{
    /// <summary>Gets the encoded bytes.</summary>
    public required byte[] Content { get; init; }

    /// <summary>Gets the format the bytes are in.</summary>
    public required ImageFormat Format { get; init; }

    /// <summary>Gets the width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Gets the height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Gets the media type, such as <c>image/webp</c>.</summary>
    public string ContentType => ImageFormatMapper.ToContentType(Format);

    /// <summary>Gets the usual file extension without the dot, such as <c>jpg</c>.</summary>
    public string Extension => ImageFormatMapper.ToExtension(Format);
}
