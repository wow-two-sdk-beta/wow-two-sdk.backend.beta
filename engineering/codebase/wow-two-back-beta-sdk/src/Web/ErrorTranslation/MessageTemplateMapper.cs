using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

/// <summary>
/// Maps a message template and its arguments to text: <c>{Name}</c>, <c>{Name:format}</c> and the ICU-style
/// <c>{Name, plural, =0 {…} one {…} few {…} many {…} other {…}}</c> with CLDR cardinal rules and <c>#</c> for the number.
/// Also maps text to its pseudo-localized form.
/// </summary>
public static class MessageTemplateMapper
{
    private const string PluralKeyword = "plural";

    /// <summary>Fills <paramref name="template"/>; fails when a placeholder has no argument or a plural value is not a number.</summary>
    /// <param name="template">The template.</param>
    /// <param name="arguments">Values by placeholder name.</param>
    /// <param name="culture">The culture numbers, formats and plural rules follow.</param>
    /// <param name="message">The filled text.</param>
    public static bool TryFormat(string template, IReadOnlyDictionary<string, object?>? arguments, CultureInfo culture, [NotNullWhen(true)] out string? message)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(culture);

        var builder = new StringBuilder(template.Length);
        var index = 0;
        while (index < template.Length)
        {
            var open = template.IndexOf('{', index);
            if (open < 0)
            {
                builder.Append(template, index, template.Length - index);
                break;
            }

            var close = MatchingBrace(template, open);
            if (close < 0)
            {
                builder.Append(template, index, template.Length - index);
                break;
            }

            builder.Append(template, index, open - index);
            if (!TryPlaceholder(template.Substring(open + 1, close - open - 1), arguments, culture, out var text))
            {
                message = null;
                return false;
            }

            builder.Append(text);
            index = close + 1;
        }

        message = builder.ToString();
        return true;
    }

    /// <summary>The CLDR cardinal plural category of <paramref name="number"/> in <paramref name="culture"/>'s language.</summary>
    /// <param name="number">The number.</param>
    /// <param name="culture">The culture whose language decides the rule.</param>
    /// <returns><c>zero</c>, <c>one</c>, <c>two</c>, <c>few</c>, <c>many</c> or <c>other</c>.</returns>
    public static string PluralCategory(decimal number, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var integer = decimal.Truncate(number) == number;
        var n = integer ? (long)decimal.Truncate(Math.Abs(number)) : -1;

        return culture.TwoLetterISOLanguageName switch
        {
            "ru" or "uk" or "be" when integer => (n % 10, n % 100) switch
            {
                (1, not 11) => "one",
                (>= 2 and <= 4, < 12 or > 14) => "few",
                _ => "many",
            },
            "ru" or "uk" or "be" => "other",
            "zh" or "ja" or "ko" or "vi" or "th" or "id" or "ms" => "other",
            "fr" when integer && n <= 1 => "one",
            _ => integer && n == 1 ? "one" : "other",
        };
    }

    /// <summary>Accents every Latin letter and brackets the text, so untranslated or truncated strings stand out in a UI.</summary>
    /// <param name="text">The text.</param>
    public static string Pseudo(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        const string plain = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string accented = "åƀçđéƒĝĥîĵķļɱñöþǫŕšţûṽŵẋýžÅƁÇĐÉƑĜĤÎĴĶĻṀÑÖÞǪŔŠŢÛṼŴẊÝŽ";
        var builder = new StringBuilder(text.Length + 8).Append("[!! ");
        foreach (var character in text)
        {
            var position = plain.IndexOf(character, StringComparison.Ordinal);
            builder.Append(position < 0 ? character : accented[position]);
        }

        return builder.Append(" !!]").ToString();
    }

    private static bool TryPlaceholder(string token, IReadOnlyDictionary<string, object?>? arguments, CultureInfo culture, [NotNullWhen(true)] out string? text)
    {
        text = null;
        var comma = token.IndexOf(',', StringComparison.Ordinal);
        if (comma >= 0)
        {
            var kind = token[(comma + 1)..].TrimStart();
            if (kind.StartsWith(PluralKeyword, StringComparison.Ordinal))
            {
                var branches = kind[PluralKeyword.Length..].TrimStart().TrimStart(',');
                return TryPlural(token[..comma].Trim(), branches, arguments, culture, out text);
            }
        }

        var colon = token.IndexOf(':', StringComparison.Ordinal);
        var name = (colon < 0 ? token : token[..colon]).Trim();
        var format = colon < 0 ? null : token[(colon + 1)..];
        if (arguments is null || !arguments.TryGetValue(name, out var value))
            return false;

        text = value is IFormattable formattable ? formattable.ToString(format, culture) : Convert.ToString(value, culture) ?? string.Empty;
        return true;
    }

    private static bool TryPlural(string name, string branches, IReadOnlyDictionary<string, object?>? arguments, CultureInfo culture, [NotNullWhen(true)] out string? text)
    {
        text = null;
        if (arguments is null || !arguments.TryGetValue(name, out var value) || !TryNumber(value, out var number))
            return false;

        var choices = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index < branches.Length)
        {
            var open = branches.IndexOf('{', index);
            var close = open < 0 ? -1 : MatchingBrace(branches, open);
            if (open < 0 || close < 0)
                break;

            choices[branches[index..open].Trim()] = branches.Substring(open + 1, close - open - 1);
            index = close + 1;
        }

        var exact = "=" + number.ToString(CultureInfo.InvariantCulture);
        if (!choices.TryGetValue(exact, out var branch)
            && !choices.TryGetValue(PluralCategory(number, culture), out branch)
            && !choices.TryGetValue("other", out branch))
            return false;

        return TryFormat(branch.Replace("#", number.ToString(culture), StringComparison.Ordinal), arguments, culture, out text);
    }

    private static bool TryNumber(object? value, out decimal number)
    {
        try
        {
            number = value is IConvertible convertible ? convertible.ToDecimal(CultureInfo.InvariantCulture) : 0;
            return value is IConvertible;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            number = 0;
            return false;
        }
    }

    private static int MatchingBrace(string text, int open)
    {
        var depth = 0;
        for (var index = open; index < text.Length; index++)
        {
            if (text[index] == '{')
                depth++;
            else if (text[index] == '}' && --depth == 0)
                return index;
        }

        return -1;
    }
}
