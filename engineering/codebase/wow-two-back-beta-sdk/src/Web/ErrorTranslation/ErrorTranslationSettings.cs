namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

/// <summary>
/// Holds the <c>ErrorTranslation</c> configuration section: whether error messages are translated, for which cultures,
/// and the host's own message catalog. A missing section leaves every message as authored.
/// </summary>
public sealed record ErrorTranslationSettings
{
    /// <summary>The configuration section the settings bind from.</summary>
    public const string SectionName = "ErrorTranslation";

    /// <summary>Translate error messages. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>The culture messages are authored in; requests for it keep the authored text. Default <c>en</c>.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Cultures a request may select; empty accepts any culture a translation exists for.</summary>
    public List<string> SupportedCultures { get; } = [];

    /// <summary>Replace a top-level message by its error type's text when no specific key matches. Default true.</summary>
    public bool TranslateByErrorType { get; set; } = true;

    /// <summary>Use FluentValidation's language packs for validator codes the catalog lacks. Default true.</summary>
    public bool UseValidatorTranslations { get; set; } = true;

    /// <summary>The host's catalog: culture → key (error type, field-error code or <c>messageKey</c>) → template with <c>{Name}</c> placeholders.</summary>
    public Dictionary<string, Dictionary<string, string>> Messages { get; } = new(StringComparer.OrdinalIgnoreCase);
}
