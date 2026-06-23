using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the contract for resolving the display message of an <see cref="AppError"/> for the current request.</summary>
public interface IErrorMessageResolver
{
    /// <summary>Resolves the message for <paramref name="error"/>, defaulting to <see cref="AppError.Message"/>.</summary>
    /// <param name="error">The error to resolve.</param>
    /// <param name="context">The current request context (carries the culture).</param>
    string Resolve(AppError error, HttpContext context);
}

/// <summary>Provides the default message resolution — a passthrough to <see cref="AppError.Message"/>.</summary>
public sealed class DefaultErrorMessageResolver : IErrorMessageResolver
{
    /// <inheritdoc/>
    public string Resolve(AppError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Message;
    }
}
