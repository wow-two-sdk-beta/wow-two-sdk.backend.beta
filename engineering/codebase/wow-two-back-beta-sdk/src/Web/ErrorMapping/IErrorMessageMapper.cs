using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Defines behavior that maps an <see cref="AppError"/> to the display message shown for the current request.</summary>
public interface IErrorMessageMapper
{
    /// <summary>Maps the message for <paramref name="error"/>, defaulting to <see cref="AppError.Message"/>.</summary>
    /// <param name="error">The error to resolve.</param>
    /// <param name="context">The current request context (carries the culture).</param>
    string Map(AppError error, HttpContext context);
}
