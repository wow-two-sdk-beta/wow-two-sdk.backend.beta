namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>A file attached to an outgoing message.</summary>
public sealed record EmailAttachment
{
    /// <summary>File name shown to the recipient.</summary>
    public required string FileName { get; init; }

    /// <summary>Raw file bytes.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>MIME type (e.g. <c>application/pdf</c>).</summary>
    public required string ContentType { get; init; }
}
