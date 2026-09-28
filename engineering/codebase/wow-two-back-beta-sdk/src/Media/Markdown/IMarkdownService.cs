namespace WoW.Two.Sdk.Backend.Beta.Media.Markdown;

/// <summary>Defines behavior that renders Markdown safely for display and summarizes it.</summary>
public interface IMarkdownService
{
    /// <summary>Renders <paramref name="markdown"/> to HTML with its plain text, headings, front matter and reading time.</summary>
    /// <param name="markdown">The Markdown source, GitHub-flavoured (tables, task lists, footnotes, …).</param>
    MarkdownResult Render(string markdown);

    /// <summary>The text of <paramref name="markdown"/> without markup.</summary>
    /// <param name="markdown">The Markdown source.</param>
    string ToPlainText(string markdown);
}
