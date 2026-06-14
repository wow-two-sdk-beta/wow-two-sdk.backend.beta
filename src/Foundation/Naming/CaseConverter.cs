namespace WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

/// <summary>
/// Converts identifiers between <see cref="CaseStyle"/>s. Tokenizes the input with
/// <see cref="WordTokenizer"/> (so the source style does not matter), then re-joins.
/// Conversions are style-stable: <c>ToCase(ToCase(x, A), B)</c> equals <c>ToCase(x, B)</c>
/// for any well-formed identifier, because every conversion re-derives words from scratch.
/// </summary>
public static class CaseConverter
{
    /// <summary>Converts <paramref name="value"/> to <paramref name="style"/>. Returns the input unchanged when null/empty.</summary>
    /// <example><c>ToCase("OrderLineItem", CaseStyle.Snake)</c> → <c>"order_line_item"</c>.</example>
    public static string ToCase(string? value, CaseStyle style)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var words = WordTokenizer.Split(value);
        if (words.Count == 0)
            return string.Empty;

        return style switch
        {
            CaseStyle.Snake => string.Join('_', words),
            CaseStyle.ScreamingSnake => string.Join('_', words.Select(WordTokenizer.Upper)),
            CaseStyle.Kebab => string.Join('-', words),
            CaseStyle.Train => string.Join('-', words.Select(WordTokenizer.Capitalize)),
            CaseStyle.Camel => Camel(words),
            CaseStyle.Pascal => string.Concat(words.Select(WordTokenizer.Capitalize)),
            CaseStyle.Sentence => Sentence(words),
            CaseStyle.Title => string.Join(' ', words.Select(WordTokenizer.Title)),
            CaseStyle.Lower => string.Join(' ', words),
            CaseStyle.Upper => string.Join(' ', words.Select(WordTokenizer.Upper)),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown case style."),
        };
    }

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Snake)</c>.</summary>
    public static string ToSnakeCase(string? value) => ToCase(value, CaseStyle.Snake);

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Camel)</c>.</summary>
    public static string ToCamelCase(string? value) => ToCase(value, CaseStyle.Camel);

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Pascal)</c>.</summary>
    public static string ToPascalCase(string? value) => ToCase(value, CaseStyle.Pascal);

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Kebab)</c>.</summary>
    public static string ToKebabCase(string? value) => ToCase(value, CaseStyle.Kebab);

    private static string Camel(IReadOnlyList<string> words)
    {
        var head = words[0];
        return words.Count == 1
            ? head
            : head + string.Concat(words.Skip(1).Select(WordTokenizer.Capitalize));
    }

    private static string Sentence(IReadOnlyList<string> words)
    {
        var first = WordTokenizer.Capitalize(words[0]);
        return words.Count == 1
            ? first
            : first + " " + string.Join(' ', words.Skip(1));
    }
}
