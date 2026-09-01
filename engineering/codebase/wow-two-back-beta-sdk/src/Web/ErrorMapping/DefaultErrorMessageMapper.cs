using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Maps an <see cref="AppError"/> to its own <see cref="AppError.Message"/> — the default passthrough.</summary>
public sealed class DefaultErrorMessageMapper : IErrorMessageMapper
{
    /// <inheritdoc/>
    public string Map(AppError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Message;
    }
}
