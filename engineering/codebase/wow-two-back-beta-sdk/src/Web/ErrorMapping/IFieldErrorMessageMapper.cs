using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Defines behavior that maps a <see cref="FieldError"/> to the display message shown for the current request.</summary>
/// <remarks>
///   - the field-level counterpart to <see cref="IErrorMessageMapper"/>, which resolves the top-level message only
///   - the default implementation translates by <see cref="FieldError.Code"/> once the <c>ErrorTranslation</c> configuration enables it
///   - a product's own implementation replaces that behavior; it may call <c>IErrorTranslationService</c> itself
/// </remarks>
public interface IFieldErrorMessageMapper
{
    /// <summary>Maps the message for <paramref name="error"/>, defaulting to <see cref="FieldError.Message"/>.</summary>
    /// <param name="error">The field failure to resolve.</param>
    /// <param name="context">The current request context (carries the culture).</param>
    string Map(FieldError error, HttpContext context);
}
