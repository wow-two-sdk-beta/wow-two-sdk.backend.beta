using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

/// <summary>Defines translating error messages into the request's culture; every member keeps authored text while translation is off.</summary>
public interface IErrorTranslationService
{
    /// <summary>The culture this request's messages translate into, or null when translation is off or the default culture applies.</summary>
    /// <param name="context">The current request.</param>
    CultureInfo? ResolveCulture(HttpContext context);

    /// <summary>Looks <paramref name="key"/> up in the host catalog for the request's culture and fills its placeholders.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="key">The catalog key.</param>
    /// <param name="arguments">Placeholder values by name.</param>
    /// <param name="message">The translated message.</param>
    /// <returns>Whether a translation applied.</returns>
    bool TryTranslate(HttpContext context, string key, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? message);

    /// <summary>The top-level message of <paramref name="error"/> for the request's culture.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="error">The error.</param>
    string Translate(HttpContext context, AppError error);

    /// <summary>The message of <paramref name="error"/> for the request's culture.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="error">The field error, validation warning or suggestion.</param>
    string Translate(HttpContext context, FieldError error);
}
