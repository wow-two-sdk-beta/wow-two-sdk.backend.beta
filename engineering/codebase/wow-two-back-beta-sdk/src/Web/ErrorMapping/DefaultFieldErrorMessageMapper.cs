using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

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
