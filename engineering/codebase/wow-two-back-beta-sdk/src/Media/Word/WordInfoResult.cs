namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Represents what a Word document states about itself and what it holds.</summary>
public sealed record WordInfoResult
{
    /// <summary>Gets the title property.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the subject property.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the author (creator) property.</summary>
    public string? Author { get; init; }

    /// <summary>Gets who saved it last.</summary>
    public string? LastModifiedBy { get; init; }

    /// <summary>Gets the keywords property.</summary>
    public string? Keywords { get; init; }

    /// <summary>Gets the description (comments) property.</summary>
    public string? Description { get; init; }

    /// <summary>Gets when it was created, as stated.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets when it was last modified, as stated.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>Gets the page count the saving application recorded; null when none was recorded.</summary>
    public int? ReportedPages { get; init; }

    /// <summary>Gets the words in the body text, counted.</summary>
    public required int Words { get; init; }

    /// <summary>Gets the body paragraphs, table cells included, counted.</summary>
    public required int Paragraphs { get; init; }

    /// <summary>Gets the tables, counted.</summary>
    public required int Tables { get; init; }

    /// <summary>Gets the embedded images, counted.</summary>
    public required int Images { get; init; }
}
