using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

/// <summary>
/// Splits an identifier into its constituent words, independent of the input casing style.
/// Word boundaries are detected at separators (<c>_</c>, <c>-</c>, space), at lower→upper
/// transitions, at acronym-run→word transitions (<c>HTTPServer</c> → <c>HTTP</c> + <c>Server</c>),
/// and at letter↔digit transitions (<c>area2</c> → <c>area</c> + <c>2</c>).
/// </summary>
public static class WordTokenizer
{
    /// <summary>Splits <paramref name="value"/> into lowercased words. Returns an empty array for null/empty/separator-only input.</summary>
    /// <example><c>"HTTPStatusCode"</c> → <c>["http", "status", "code"]</c>; <c>"order_line2"</c> → <c>["order", "line", "2"]</c>.</example>
    public static IReadOnlyList<string> Split(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return [];

        var words = new List<string>();
        var current = new System.Text.StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (IsSeparator(c))
            {
                Flush(words, current);
                continue;
            }

            if (current.Length > 0 && IsBoundary(value, i))
                Flush(words, current);

            current.Append(char.ToLowerInvariant(c));
        }

        Flush(words, current);
        return words;
    }

    /// <summary>Determines whether a word boundary falls immediately before index <paramref name="i"/>.</summary>
    private static bool IsBoundary(string value, int i)
    {
        var prev = value[i - 1];
        var curr = value[i];

        // digit ↔ letter transition in either direction
        if (char.IsDigit(prev) != char.IsDigit(curr))
            return true;

        // lower/digit → upper  (orderLine → order|Line)
        if (!char.IsUpper(prev) && char.IsUpper(curr))
            return true;

        // acronym run → word start: UPPER followed by upper-then-lower (HTTPServer → HTTP|Server)
        if (char.IsUpper(prev) && char.IsUpper(curr)
            && i + 1 < value.Length && char.IsLower(value[i + 1]))
            return true;

        return false;
    }

    private static bool IsSeparator(char c) =>
        c is '_' or '-' or ' ' || char.IsWhiteSpace(c);

    private static void Flush(List<string> words, System.Text.StringBuilder current)
    {
        if (current.Length == 0)
            return;

        words.Add(current.ToString());
        current.Clear();
    }

    /// <summary>Capitalizes the first character of <paramref name="word"/>, leaving the remainder unchanged.</summary>
    internal static string Capitalize(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>Uppercases <paramref name="word"/> using the invariant culture.</summary>
    internal static string Upper(string word) => word.ToUpperInvariant();

    /// <summary>Title-cases <paramref name="word"/> using the invariant culture's title-case rules.</summary>
    internal static string Title(string word) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word);
}
