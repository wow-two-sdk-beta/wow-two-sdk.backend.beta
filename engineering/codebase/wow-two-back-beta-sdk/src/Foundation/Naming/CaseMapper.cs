namespace WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

/// <summary>Maps identifiers between <see cref="CaseStyle"/> values.</summary>
public static class CaseMapper
{
    /// <summary>Converts <paramref name="value"/> to <paramref name="style"/>. Returns the input unchanged when null/empty.</summary>
    /// <param name="value">The identifier to convert.</param>
    /// <param name="style">The target casing style.</param>
    public static string ToCase(string? value, CaseStyle style)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var words = WordMapper.Split(value);
        if (words.Count == 0)
            return string.Empty;

        return style switch
        {
            CaseStyle.Snake => string.Join('_', words),
            CaseStyle.ScreamingSnake => string.Join('_', words.Select(WordMapper.Upper)),
            CaseStyle.Kebab => string.Join('-', words),
            CaseStyle.Train => string.Join('-', words.Select(WordMapper.Capitalize)),
            CaseStyle.Camel => Camel(words),
            CaseStyle.Pascal => string.Concat(words.Select(WordMapper.Capitalize)),
            CaseStyle.Sentence => Sentence(words),
            CaseStyle.Title => string.Join(' ', words.Select(WordMapper.Title)),
            CaseStyle.Lower => string.Join(' ', words),
            CaseStyle.Upper => string.Join(' ', words.Select(WordMapper.Upper)),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown case style."),
        };
    }

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Snake)</c>.</summary>
    /// <param name="value">The identifier to convert.</param>
    public static string ToSnakeCase(string? value) => ToCase(value, CaseStyle.Snake);

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Camel)</c>.</summary>
    /// <param name="value">The identifier to convert.</param>
    public static string ToCamelCase(string? value) => ToCase(value, CaseStyle.Camel);

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Pascal)</c>.</summary>
    /// <param name="value">The identifier to convert.</param>
    public static string ToPascalCase(string? value) => ToCase(value, CaseStyle.Pascal);

    /// <summary>Shorthand for <c>ToCase(value, CaseStyle.Kebab)</c>.</summary>
    /// <param name="value">The identifier to convert.</param>
    public static string ToKebabCase(string? value) => ToCase(value, CaseStyle.Kebab);

    private static string Camel(IReadOnlyList<string> words)
    {
        var head = words[0];
        return words.Count == 1
            ? head
            : head + string.Concat(words.Skip(1).Select(WordMapper.Capitalize));
    }

    private static string Sentence(IReadOnlyList<string> words)
    {
        var first = WordMapper.Capitalize(words[0]);
        return words.Count == 1
            ? first
            : first + " " + string.Join(' ', words.Skip(1));
    }
}
