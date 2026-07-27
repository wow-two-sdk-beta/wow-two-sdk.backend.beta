using System.Runtime.CompilerServices;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Provides factory helpers for the SDK's base <see cref="AppError"/> kinds.</summary>
public static class AppErrors
{
    /// <summary>Creates an <see cref="AppErrorType.NotFound"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError NotFound(
        string message = "The requested resource was not found.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.NotFound, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.Conflict"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Conflict(
        string message = "The request conflicts with the current state.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Conflict, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.Forbidden"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Forbidden(
        string message = "You do not have permission to perform this action.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Forbidden, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.Unauthorized"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Unauthorized(
        string message = "Authentication is required.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Unauthorized, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.Validation"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Validation(
        string message = "One or more validation errors occurred.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Validation, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.TooManyRequests"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError TooManyRequests(
        string message = "Too many requests. Please try again later.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.TooManyRequests, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.DbTimeout"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError DbTimeout(
        string message = "The database operation timed out.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.DbTimeout, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.OperationTimeout"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError OperationTimeout(
        string message = "The operation timed out.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.OperationTimeout, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.ExternalUnavailable"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError ExternalUnavailable(
        string message = "An external dependency is currently unavailable.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.ExternalUnavailable, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.ExternalUnauthorized"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError ExternalUnauthorized(
        string message = "An external dependency rejected our credentials.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.ExternalUnauthorized, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.FileNotFound"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError FileNotFound(
        string message = "The requested file was not found.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.FileNotFound, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.SerializationFailed"/> error; when an <paramref name="inner"/> exception is supplied, folds in its throw site and cause.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="inner">Optional caught exception whose stack and type are folded in.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError SerializationFailed(
        string message = "The response could not be processed.",
        Exception? inner = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (inner is not null)
        {
            return AppError.FromException(AppErrorType.SerializationFailed, message, inner);
        }

        return AppError.Of(AppErrorType.SerializationFailed, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.DataIntegrity"/> error; when an <paramref name="inner"/> exception is supplied, folds in its throw site and cause.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="inner">Optional caught exception whose stack and type are folded in.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError DataIntegrity(
        string message = "A data integrity error occurred.",
        Exception? inner = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (inner is not null)
        {
            return AppError.FromException(AppErrorType.DataIntegrity, message, inner);
        }

        return AppError.Of(AppErrorType.DataIntegrity, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.BusinessRule"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError BusinessRule(
        string message = "The request could not be processed due to a business rule.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.BusinessRule, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.PaymentRequired"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError PaymentRequired(
        string message = "Payment or a higher plan is required for this action.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.PaymentRequired, message, null, member, file, line);
    }

    /// <summary>Creates a <see cref="AppErrorType.Gone"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Gone(
        string message = "The requested resource is no longer available.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Gone, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.Canceled"/> error.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Canceled(
        string message = "The operation was canceled.",
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Canceled, message, null, member, file, line);
    }

    /// <summary>Creates an <see cref="AppErrorType.Unexpected"/> error; when an <paramref name="inner"/> exception is supplied, folds in its throw site and cause.</summary>
    /// <param name="message">The default-culture message.</param>
    /// <param name="inner">Optional caught exception whose stack and type are folded in.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Unexpected(
        string message = "An unexpected error occurred.",
        Exception? inner = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (inner is not null)
        {
            return AppError.FromException(AppErrorType.Unexpected, message, inner);
        }

        return AppError.Of(AppErrorType.Unexpected, message, null, member, file, line);
    }
}
