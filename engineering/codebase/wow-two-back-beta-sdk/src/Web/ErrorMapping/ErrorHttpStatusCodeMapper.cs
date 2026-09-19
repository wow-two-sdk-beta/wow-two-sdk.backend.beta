using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Maps an <see cref="AppErrorType"/> to its default HTTP status code.</summary>
public sealed class ErrorHttpStatusCodeMapper : IErrorHttpStatusCodeMapper
{
    /// <inheritdoc/>
    public int ToStatusCode(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Type switch
        {
            AppErrorType.Validation => StatusCodes.Status400BadRequest,
            AppErrorType.Unauthorized or AppErrorType.ExternalUnauthorized => StatusCodes.Status401Unauthorized,
            AppErrorType.Forbidden => StatusCodes.Status403Forbidden,
            AppErrorType.NotFound or AppErrorType.FileNotFound => StatusCodes.Status404NotFound,
            AppErrorType.Conflict => StatusCodes.Status409Conflict,
            AppErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            AppErrorType.PaymentRequired => StatusCodes.Status402PaymentRequired,
            AppErrorType.Gone => StatusCodes.Status410Gone,
            AppErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            AppErrorType.ExternalUnavailable => StatusCodes.Status503ServiceUnavailable,
            AppErrorType.DbTimeout or AppErrorType.OperationTimeout => StatusCodes.Status504GatewayTimeout,
            AppErrorType.Canceled => 499,
            AppErrorType.DataIntegrity => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
