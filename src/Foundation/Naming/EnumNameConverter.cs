using System.Collections.Concurrent;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

/// <summary>
/// Converts enum values to and from case-styled string labels, reversibly.
/// The reverse lookup is exact (built from the enum's own members), so a value that round-trips
/// through <see cref="ToLabel"/> + <see cref="Parse"/> always returns the original member — no
/// underscore-stripping heuristics required. Lookups are cached per (enum, style).
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public static class EnumNameConverter<TEnum> where TEnum : struct, Enum
{
    private static readonly ConcurrentDictionary<CaseStyle, (Dictionary<TEnum, string> Forward, Dictionary<string, TEnum> Reverse)> Maps = new();

    /// <summary>Returns the case-styled label for <paramref name="value"/>.</summary>
    /// <example><c>ToLabel(AiProvider.OpenAi, CaseStyle.Snake)</c> → <c>"open_ai"</c>.</example>
    public static string ToLabel(TEnum value, CaseStyle style) => GetMaps(style).Forward[value];

    /// <summary>Parses a case-styled <paramref name="label"/> back to its enum member. Case-insensitive on the label.</summary>
    /// <exception cref="ArgumentException">No member of <typeparamref name="TEnum"/> maps to <paramref name="label"/> under <paramref name="style"/>.</exception>
    public static TEnum Parse(string label, CaseStyle style)
    {
        if (TryParse(label, style, out var value))
            return value;

        throw new ArgumentException(
            $"'{label}' does not map to any member of enum '{typeof(TEnum).Name}' under {style} casing.", nameof(label));
    }

    /// <summary>Attempts to parse a case-styled <paramref name="label"/> back to its enum member.</summary>
    public static bool TryParse(string? label, CaseStyle style, out TEnum value)
    {
        if (!string.IsNullOrEmpty(label) && GetMaps(style).Reverse.TryGetValue(label, out value))
            return true;

        value = default;
        return false;
    }

    private static (Dictionary<TEnum, string> Forward, Dictionary<string, TEnum> Reverse) GetMaps(CaseStyle style) =>
        Maps.GetOrAdd(style, static s =>
        {
            var forward = new Dictionary<TEnum, string>();
            var reverse = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);

            foreach (var member in Enum.GetValues<TEnum>())
            {
                var label = CaseConverter.ToCase(member.ToString(), s);
                forward.TryAdd(member, label);
                reverse.TryAdd(label, member);
            }

            return (forward, reverse);
        });
}
