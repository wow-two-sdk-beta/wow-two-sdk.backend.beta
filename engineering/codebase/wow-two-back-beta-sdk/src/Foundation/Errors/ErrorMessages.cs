namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Contains the safe, default-culture message for each <see cref="AppErrorType"/>; never leaks an underlying exception message.</summary>
public static class ErrorMessages
{
    /// <summary>Returns the safe generic message for <paramref name="type"/>.</summary>
    /// <param name="type">The failure kind whose default message is requested.</param>
    public static string For(AppErrorType type)
    {
        return type switch
        {
            AppErrorType.Validation => "One or more validation errors occurred.",
            AppErrorType.NotFound => "The requested resource was not found.",
            AppErrorType.Conflict => "The request conflicts with the current state.",
            AppErrorType.BusinessRule => "The request could not be processed due to a business rule.",
            AppErrorType.PaymentRequired => "Payment or a higher plan is required for this action.",
            AppErrorType.Gone => "The requested resource is no longer available.",
            AppErrorType.Unauthorized => "Authentication is required.",
            AppErrorType.Forbidden => "You do not have permission to perform this action.",
            AppErrorType.TooManyRequests => "Too many requests. Please try again later.",
            AppErrorType.DbTimeout => "The database operation timed out.",
            AppErrorType.OperationTimeout => "The operation timed out.",
            AppErrorType.ExternalUnauthorized => "An external dependency rejected our credentials.",
            AppErrorType.ExternalUnavailable => "An external dependency is currently unavailable.",
            AppErrorType.FileNotFound => "The requested file was not found.",
            AppErrorType.SerializationFailed => "The response could not be processed.",
            AppErrorType.DataIntegrity => "A data integrity error occurred.",
            AppErrorType.Canceled => "The operation was canceled.",
            _ => "An unexpected error occurred.",
        };
    }
}
