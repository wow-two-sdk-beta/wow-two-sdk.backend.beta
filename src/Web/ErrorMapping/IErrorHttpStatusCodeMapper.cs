using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Defines the contract for mapping an <see cref="AppError"/> to an HTTP status code.</summary>
public interface IErrorHttpStatusCodeMapper
{
    /// <summary>Maps <paramref name="error"/> to an HTTP status code.</summary>
    /// <param name="error">The error to map.</param>
    int ToStatusCode(AppError error);
}
