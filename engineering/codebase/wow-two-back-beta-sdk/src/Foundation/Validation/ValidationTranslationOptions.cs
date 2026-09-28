namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>
/// Holds how validation and error messages are translated into the request's culture. Off by default: every message
/// renders as authored until <see cref="Enabled"/> is set in code or in the host section <c>Validation:Translation</c>.
/// </summary>
public sealed record ValidationTranslationOptions
{
    /// <summary>Translate messages. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>The culture messages are authored in; requests for it keep the authored text. Default <c>en</c>.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Cultures a request may select; empty accepts any culture a translation exists for.</summary>
    public List<string> SupportedCultures { get; } = [];

    /// <summary>Replace a top-level message by its error type's text when no specific key matches. Default true.</summary>
    public bool TranslateByErrorType { get; set; } = true;

    /// <summary>Use FluentValidation's language packs for validator codes the catalog lacks. Default true.</summary>
    public bool UseValidatorTranslations { get; set; } = true;

    /// <summary>Pseudo-localize every message (accented, bracketed) to spot untranslated text; development only. Default false.</summary>
    public bool PseudoLocalization { get; set; }

    /// <summary>The catalog: culture → key (error type, field-error code or <c>messageKey</c>) → template.</summary>
    public Dictionary<string, Dictionary<string, string>> Messages { get; } = new(StringComparer.OrdinalIgnoreCase);
}
