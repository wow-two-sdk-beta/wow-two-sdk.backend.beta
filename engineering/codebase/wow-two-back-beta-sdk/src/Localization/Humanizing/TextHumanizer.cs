using System.Globalization;
using Humanizer;

namespace WoW.Two.Sdk.Backend.Beta.Localization.Humanizing;

/// <summary>Default <see cref="ITextHumanizer"/> over Humanizer's extensions. Stateless and thread-safe.</summary>
public sealed class TextHumanizer : ITextHumanizer
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
