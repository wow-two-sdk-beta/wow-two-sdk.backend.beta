namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>A file attached to an outgoing message.</summary>
/// <param name="FileName">File name shown to the recipient.</param>
/// <param name="Content">Raw file bytes.</param>
/// <param name="ContentType">MIME type (e.g. <c>application/pdf</c>).</param>
public sealed record EmailAttachment(string FileName, ReadOnlyMemory<byte> Content, string ContentType);
