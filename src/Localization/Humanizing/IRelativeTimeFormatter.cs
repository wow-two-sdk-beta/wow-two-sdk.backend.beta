using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Localization.Humanizing;

/// <summary>Formats an instant as a human relative-time phrase (e.g. "3 hours ago", "in 2 days"), relative to now.</summary>
public interface IRelativeTimeFormatter
{
    /// <summary>Formats <paramref name="instant"/> relative to the current time.</summary>
    /// <param name="instant">The instant to describe.</param>
    /// <param name="culture">The culture for the phrase; <see langword="null"/> uses the ambient request culture.</param>
    /// <returns>A localized relative-time phrase.</returns>
    string Format(DateTimeOffset instant, CultureInfo? culture = null);
}
