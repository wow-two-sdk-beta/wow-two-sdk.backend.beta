namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Represents a Word document produced by an operation.</summary>
public sealed record WordResult
{
    /// <summary>Holds the media type of a .docx file.</summary>
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Gets the file bytes.</summary>
    public required byte[] Content { get; init; }

    /// <summary>Gets the media type.</summary>
    public string ContentType => DocxContentType;
}
