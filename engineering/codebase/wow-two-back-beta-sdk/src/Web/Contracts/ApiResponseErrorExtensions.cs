using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts;

/// <summary>Builds the failure envelope of any response model from an <see cref="AppError"/>.</summary>
public static class ApiResponseErrorExtensions
{
    /// <summary>
    /// The <see cref="ApiResponse{T}.Failure"/> for <paramref name="error"/>: the mapped status and the message the
    /// registered <see cref="IErrorMessageMapper"/> gives this request, translated when error translation is enabled.
    /// </summary>
    /// <typeparam name="T">The response model the success envelope would carry.</typeparam>
    /// <param name="error">The failure.</param>
    /// <param name="context">The current request; its services supply the mappers, or the defaults apply.</param>
    public static ApiResponse<T>.Failure ToApiFailure<T>(this AppError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);

        var services = context.RequestServices;
        var status = (services?.GetService<IErrorHttpStatusCodeMapper>() ?? new ErrorHttpStatusCodeMapper()).ToStatusCode(error);
        var message = (services?.GetService<IErrorMessageMapper>() ?? new ErrorMessageMapper()).Map(error, context);
        return new ApiResponse<T>.Failure { StatusCode = (HttpStatusCode)status, Error = message };
    }
}
