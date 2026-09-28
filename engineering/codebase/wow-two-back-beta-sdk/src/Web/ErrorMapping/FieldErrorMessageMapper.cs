using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>
/// Maps a field failure to its <see cref="FieldError.Message"/>, translated by its code into the request's culture when
/// the <c>ErrorTranslation</c> configuration enables it — otherwise the default passthrough.
/// </summary>
/// <param name="translations">The translation service; null keeps every message as authored.</param>
public sealed class FieldErrorMessageMapper(IErrorTranslationService? translations = null) : IFieldErrorMessageMapper
{
    /// <inheritdoc/>
    public string Map(FieldError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);

        return translations is null ? error.Message : translations.Translate(context, error);
    }
}
