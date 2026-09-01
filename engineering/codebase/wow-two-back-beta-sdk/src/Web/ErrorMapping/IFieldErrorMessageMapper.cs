using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Maps a <see cref="FieldError"/> to the display message shown for the current request.</summary>
/// <remarks>
///   - the field-level counterpart to <see cref="IErrorMessageMapper"/>, which resolves the top-level message only
///   - to localize, register a resolver that formats the <c>IStringLocalizer</c> entry for <see cref="FieldError.Code"/> with <see cref="FieldError.Params"/>
///   - the request culture is already on <c>CultureInfo.CurrentUICulture</c> under <c>UseRequestLocalizationConventions</c>
/// </remarks>
public interface IFieldErrorMessageMapper
{
    /// <summary>Maps the message for <paramref name="error"/>, defaulting to <see cref="FieldError.Message"/>.</summary>
    /// <param name="error">The field failure to resolve.</param>
    /// <param name="context">The current request context (carries the culture).</param>
    string Map(FieldError error, HttpContext context);
}
