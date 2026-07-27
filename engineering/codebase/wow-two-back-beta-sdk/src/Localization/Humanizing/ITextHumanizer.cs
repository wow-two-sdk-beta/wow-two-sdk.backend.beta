namespace WoW.Two.Sdk.Backend.Beta.Localization.Humanizing;

/// <summary>
/// Turns values into human-friendly text — ordinals, counted quantities, and English inflection — for
/// user-facing strings. An injectable, testable facade over Humanizer's static extensions; methods honour
/// the ambient request culture where the underlying operation is culture-sensitive.
/// </summary>
public interface ITextHumanizer
{
    /// <summary>Returns the ordinal form of a number (e.g. <c>1</c> → <c>"1st"</c>).</summary>
    /// <param name="number">The number to ordinalize.</param>
    /// <returns>The ordinal string.</returns>
    string Ordinalize(int number);

    /// <summary>Prefixes a word with a count and pluralizes it as needed (e.g. <c>("item", 3)</c> → <c>"3 items"</c>).</summary>
    /// <param name="word">The singular noun.</param>
    /// <param name="count">The quantity.</param>
    /// <returns>The counted, correctly-inflected phrase.</returns>
    string Quantity(string word, int count);

    /// <summary>Returns the plural form of an English word (e.g. <c>"item"</c> → <c>"items"</c>).</summary>
    /// <param name="word">The word to pluralize.</param>
    /// <returns>The plural form.</returns>
    string Pluralize(string word);

    /// <summary>Returns the singular form of an English word (e.g. <c>"items"</c> → <c>"item"</c>).</summary>
    /// <param name="word">The word to singularize.</param>
    /// <returns>The singular form.</returns>
    string Singularize(string word);
}
