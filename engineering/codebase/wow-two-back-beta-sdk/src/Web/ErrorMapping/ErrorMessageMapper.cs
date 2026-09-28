using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>
/// Maps an <see cref="AppError"/> to its <see cref="AppError.Message"/>, translated into the request's culture when the
/// <c>ErrorTranslation</c> configuration enables it — otherwise the default passthrough.
/// </summary>
/// <param name="translations">The translation service; null keeps every message as authored.</param>
public sealed class ErrorMessageMapper(IErrorTranslationService? translations = null) : IErrorMessageMapper
{
    /// <inheritdoc/>
    public string Map(AppError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);

        return translations is null ? error.Message : translations.Translate(context, error);
    }
}
