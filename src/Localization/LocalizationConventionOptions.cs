namespace WoW.Two.Sdk.Backend.Beta.Localization;

/// <summary>
/// Conventional request-localization tunables applied by
/// <see cref="LocalizationServiceCollectionExtensions.AddRequestLocalizationConventions"/>. Supply at
/// least one supported culture; the resolution order is query string → cookie → <c>Accept-Language</c>.
/// </summary>
public sealed class LocalizationConventionOptions
{
    /// <summary>Gets or sets the fallback culture used when no provider resolves one. Default <c>"en"</c>.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Gets the BCP-47 culture names the app supports (e.g. <c>"en"</c>, <c>"uz"</c>, <c>"ru"</c>). Must be non-empty.</summary>
    public IList<string> SupportedCultures { get; } = [];

    /// <summary>Gets or sets whether the <c>?culture=</c>/<c>?ui-culture=</c> query-string provider is enabled. Default <see langword="true"/>.</summary>
    public bool EnableQueryStringProvider { get; set; } = true;

    /// <summary>Gets or sets whether the culture-cookie provider is enabled. Default <see langword="true"/>.</summary>
    public bool EnableCookieProvider { get; set; } = true;

    /// <summary>Gets or sets whether the <c>Accept-Language</c> header provider is enabled. Default <see langword="true"/>.</summary>
    public bool EnableAcceptLanguageProvider { get; set; } = true;
}
