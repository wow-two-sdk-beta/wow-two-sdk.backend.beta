namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Refers to the shape extracted text takes.</summary>
public enum WordTextFormat
{
    /// <summary>Refers to plain text: paragraphs on lines, table cells tab-separated.</summary>
    Plain,

    /// <summary>Refers to Markdown: headings, lists, emphasis, links and pipe tables kept.</summary>
    Markdown,
}
