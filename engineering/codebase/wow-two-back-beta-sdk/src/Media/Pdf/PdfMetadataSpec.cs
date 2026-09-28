namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents document information; on update a null field is left as it is, an empty one is cleared.</summary>
public sealed record PdfMetadataSpec
{
    /// <summary>Gets the title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the author.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the subject.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the keywords.</summary>
    public string? Keywords { get; init; }

    /// <summary>Gets the creating application.</summary>
    public string? Creator { get; init; }

    /// <summary>Gets the producing library; read only.</summary>
    public string? Producer { get; init; }
}
