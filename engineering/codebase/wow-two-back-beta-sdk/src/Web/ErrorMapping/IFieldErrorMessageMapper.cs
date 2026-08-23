using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Maps a <see cref="FieldError"/> to the display message shown for the current request.</summary>
/// <remarks>
/// <para>
/// The field-level counterpart to <see cref="IErrorMessageMapper"/>, which resolves only the top-level message. Without this seam
/// a validation response is localized in its <c>detail</c> and English in every one of its <c>errors[]</c> entries, because a FluentValidation
/// message is authored in the validator and never passes through the localization stack.
/// </para>
/// <para>
/// The default implementation is a passthrough, so wiring it changes nothing until an app registers its own. That is deliberate: this is the
/// PLUG, not the translation. An app that wants localized field messages registers a resolver that looks up
/// <see cref="FieldError.Code"/> in an <c>IStringLocalizer</c> and formats it with <see cref="FieldError.Params"/> — the request culture is
/// already on <c>CultureInfo.CurrentUICulture</c> when <c>UseRequestLocalizationConventions</c> is in the pipeline.
/// </para>
/// <para>
/// A frontend consuming the SDK's message catalogue does not need this at all — it renders from <see cref="FieldError.Code"/> in its own
/// language. This exists for every other consumer: another service, a mobile client, a direct API caller.
/// </para>
/// </remarks>
public interface IFieldErrorMessageMapper
{
    /// <summary>Maps the message for <paramref name="error"/>, defaulting to <see cref="FieldError.Message"/>.</summary>
    /// <param name="error">The field failure to resolve.</param>
    /// <param name="context">The current request context (carries the culture).</param>
    string Map(FieldError error, HttpContext context);
}

/// <summary>Maps a field failure to its default message — a passthrough to <see cref="FieldError.Message"/>.</summary>
public sealed class DefaultFieldErrorMessageMapper : IFieldErrorMessageMapper
{
    /// <inheritdoc/>
    public string Map(FieldError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Message;
    }
}
