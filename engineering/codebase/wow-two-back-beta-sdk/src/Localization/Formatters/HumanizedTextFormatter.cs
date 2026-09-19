using System.Globalization;
using Humanizer;

namespace WoW.Two.Sdk.Backend.Beta.Localization.Formatters;

/// <summary>Formats numbers and words as ordinals, counted quantities and English inflections using Humanizer.</summary>
public sealed class HumanizedTextFormatter : IHumanizedTextFormatter
{
    /// <inheritdoc />
    public string Ordinalize(int number) => number.Ordinalize(CultureInfo.CurrentCulture);

    /// <inheritdoc />
    public string Quantity(string word, int count)
    {
        ArgumentNullException.ThrowIfNull(word);
        return word.ToQuantity(count);
    }

    /// <inheritdoc />
    public string Pluralize(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return word.Pluralize();
    }

    /// <inheritdoc />
    public string Singularize(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return word.Singularize();
    }
}
